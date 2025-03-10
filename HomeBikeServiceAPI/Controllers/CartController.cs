using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "User")]
    public class CartController : ControllerBase
    {
        private readonly CartService _cartService;
        private readonly BikePartsService _bikePartsService;
        private readonly ILogger<CartController> _logger;
        private const string ImageFolder = "Images/BikeParts";
        private const int MaxImageWidth = 1024;  // Max width for resizing images

        public CartController(CartService cartService, BikePartsService bikePartsService, ILogger<CartController> logger)
        {
            _cartService = cartService;
            _bikePartsService = bikePartsService;
            _logger = logger;
        }



        public class ImgBBResponse
        {
            public ImgBBData Data { get; set; }
            public bool Success { get; set; }
            public int Status { get; set; }
        }

        public class ImgBBData
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string UrlViewer { get; set; }
            [JsonPropertyName("url")]
            public string Url { get; set; }
            public string DisplayUrl { get; set; }
            public string Width { get; set; }
            public string Height { get; set; }
            public string Size { get; set; }
            public string Time { get; set; }
            public string Expiration { get; set; }
            public ImgBBImage Image { get; set; }
            public ImgBBImage Thumb { get; set; }
            public ImgBBImage Medium { get; set; }
            public string DeleteUrl { get; set; }
        }

        public class ImgBBImage
        {
            public string Filename { get; set; }
            public string Name { get; set; }
            public string Mime { get; set; }
            public string Extension { get; set; }
            public string Url { get; set; }
        }


        // Method to resize images if they exceed a certain size
        private async Task<byte[]> ResizeImage(IFormFile image, int maxWidth = MaxImageWidth)
        {
            using var imageStream = image.OpenReadStream();
            using var imageToResize = Image.Load(imageStream);

            // Resize the image to the max width while maintaining the aspect ratio
            if (imageToResize.Width > maxWidth)
            {
                imageToResize.Mutate(x => x.Resize(maxWidth, 0)); // Resize keeping aspect ratio
            }

            using var outputStream = new MemoryStream();
            imageToResize.Save(outputStream, new JpegEncoder()); // Save as JPEG
            return outputStream.ToArray();
        }

        // Upload image to ImgBB and get URL
        private async Task<string> UploadToImgBB(IFormFile file)
        {
            string apiKey = "d0c73e0ae1562c672259e39238e3d36f";  // Replace with your actual API key
            string url = $"https://api.imgbb.com/1/upload?key={apiKey}";

            using var client = new HttpClient();
            using var content = new MultipartFormDataContent();

            // Create a StreamContent for the file
            var imageContent = new StreamContent(file.OpenReadStream());
            content.Add(imageContent, "image", file.FileName);

            // Post the request to ImgBB API
            var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"ImgBB upload failed: {await response.Content.ReadAsStringAsync()}");
                return null;
            }

            // Deserialize the response body
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("ImgBB Response: {ResponseBody}", responseBody);

            try
            {
                // Manually parse the response body using JsonDocument
                var jsonResponse = JsonDocument.Parse(responseBody);

                // Extract the URL from the response
                var imgUrl = jsonResponse.RootElement
                    .GetProperty("data")
                    .GetProperty("url")
                    .GetString();

                // Check if the URL exists and return it
                if (!string.IsNullOrEmpty(imgUrl))
                {
                    _logger.LogInformation("ImgBB Image URL: {ImgUrl}", imgUrl);
                    return imgUrl;
                }
                else
                {
                    _logger.LogError("ImgBB response does not contain a valid URL.");
                    return null;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError($"Failed to parse ImgBB response: {ex.Message}");
                return null;
            }
        }


        private async Task<string> GetImgBBUrlAsync(string partImage)
        {
            // Assuming partImage contains the ImgBB URL, if not, replace with logic to fetch from ImgBB
            if (!string.IsNullOrEmpty(partImage))
            {
                return partImage;
            }
            else
            {
                // Handle the case where no ImgBB URL is available, possibly return a placeholder image URL.
                return "https://default-image-url.com";
            }
        }


        private int GetUserIdFromToken()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }
        


        // Add to Cart or Update Existing Cart Item
        [HttpPost("add")]
        public async Task<IActionResult> AddToCart(CartRequest cartRequest)
        {
            if (cartRequest == null || cartRequest.BikePartsId <= 0 || cartRequest.Quantity <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid cart details." });
            }

            int userId = GetUserIdFromToken();
            if (userId == 0) return Unauthorized("User not identified.");

            // Check if the BikePart exists and get the price
            var bikePart = await _bikePartsService.GetBikePartById(cartRequest.BikePartsId);
            if (bikePart == null)
            {
                return NotFound(new { success = false, message = "Bike part not found." });
            }

            if (cartRequest.Quantity > bikePart.Quantity)
            {
                return BadRequest(new { success = false, message = "Insufficient stock for the requested quantity." });
            }

            // Calculate the total price
            decimal totalPrice = cartRequest.Quantity * bikePart.Price;

            // Check if the user already has this bike part in their cart
            var existingCartItem = await _cartService.GetCartItemsByUser(userId);
            var existingCart = existingCartItem.FirstOrDefault(c => c.BikePartsId == cartRequest.BikePartsId);

            if (existingCart != null)
            {
                // If the part exists in the cart, update the quantity and total price
                existingCart.Quantity += cartRequest.Quantity;
                existingCart.TotalPrice = existingCart.Quantity * bikePart.Price; // Recalculate the total price

                try
                {
                    var result = await _cartService.UpdateCartItem(existingCart);
                    if (result)
                    {
                        return Ok(new { success = true, message = "Cart item updated successfully." });
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "Failed to update cart item." });
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
                }
            }
            else
            {
                // If the part does not exist in the cart, create a new cart item
                var cart = new Cart
                {
                    UserId = userId,
                    BikePartsId = cartRequest.BikePartsId,
                    Quantity = cartRequest.Quantity,
                    TotalPrice = totalPrice,
                    DateAdded = DateTime.Now,
                    IsPaymentDone = false
                };

                try
                {
                    var result = await _cartService.AddToCart(cart);
                    if (result)
                    {
                        return Ok(new { success = true, message = "Item added to cart successfully." });
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "Failed to add item to cart." });
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
                }
            }
        }


        // Get all carts by user ID with part details and part image
        [HttpGet("user")]
        public async Task<IActionResult> GetCartsByUserId()
        {
            int userId = GetUserIdFromToken();
            if (userId == 0) return Unauthorized("User not identified.");

            try
            {
                // Fetch the cart items for the user
                var carts = await _cartService.GetCartItemsByUser(userId);

                // For each cart item, fetch the bike part details including the image URL
                var cartWithPartDetails = new List<object>(); // You can use a custom DTO or anonymous object

                foreach (var cart in carts)
                {
                    var bikePart = await _bikePartsService.GetBikePartById(cart.BikePartsId);
                    if (bikePart != null)
                    {
                        // Assuming the bike part image is stored under "wwwroot/BikePartImages" directory or similar
                        /*var imageUrl = bikePart.PartImage != null
                            ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/BikeParts/{bikePart.PartImage}"
                            : null;*/

                        var imageUrl = await GetImgBBUrlAsync(bikePart.PartImage);

                        cartWithPartDetails.Add(new
                        {
                            cart.Id,
                            cart.BikePartsId,
                            cart.Quantity,
                            cart.TotalPrice,
                            cart.DateAdded,
                            cart.IsPaymentDone,
                            BikePartDetails = new
                            {
                                bikePart.PartName,
                                bikePart.Description,
                                bikePart.Price,
                                bikePart.Quantity,
                                ImageUrl = imageUrl
                            }
                        });
                    }
                }

                return Ok(new { success = true, carts = cartWithPartDetails });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }


        // Get cart by user ID
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetCartsByUserId(int userId)
        {
            if (userId <= 0) return BadRequest("Invalid User ID.");

            try
            {
                // Fetch the cart items for the user
                var carts = await _cartService.GetCartItemsByUser(userId);

                if (carts == null || !carts.Any()) return NotFound("No cart items found for this user.");

                var cartWithPartDetails = new List<object>();

                foreach (var cart in carts)
                {
                    var bikePart = await _bikePartsService.GetBikePartById(cart.BikePartsId);
                    if (bikePart != null)
                    {
                        var imageUrl = await GetImgBBUrlAsync(bikePart.PartImage);

                        cartWithPartDetails.Add(new
                        {
                            cart.Id,
                            cart.BikePartsId,
                            cart.Quantity,
                            cart.TotalPrice,
                            cart.DateAdded,
                            cart.IsPaymentDone,
                            BikePartDetails = new
                            {
                                bikePart.PartName,
                                bikePart.Description,
                                bikePart.Price,
                                bikePart.Quantity,
                                ImageUrl = imageUrl
                            }
                        });
                    }
                }

                return Ok(new { success = true, carts = cartWithPartDetails });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }



        // Update cart item or add new item if it does not exist
        [HttpPut("{cartId}")]
        public async Task<IActionResult> UpdateCartItem(int cartId, CartRequest cartRequest)
        {
            if (cartRequest == null || cartRequest.Quantity <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid cart update details." });
            }

            int userId = GetUserIdFromToken();
            if (userId == 0) return Unauthorized("User not identified.");

            // Fetch the cart items for the user
            var cartItems = await _cartService.GetCartItemsByUser(userId);
            var existingCart = cartItems.FirstOrDefault(c => c.Id == cartId);

            // If the cart item doesn't exist, try to add a new one
            if (existingCart == null)
            {
                var bikePart = await _bikePartsService.GetBikePartById(cartRequest.BikePartsId);
                if (bikePart == null)
                {
                    return NotFound(new { success = false, message = "Bike part not found." });
                }

                if (cartRequest.Quantity > bikePart.Quantity)
                {
                    return BadRequest(new { success = false, message = "Insufficient stock for the requested quantity." });
                }

                decimal totalPrice = cartRequest.Quantity * bikePart.Price;

                // Check if the user already has this bike part in their cart
                var existingCartItem = cartItems.FirstOrDefault(c => c.BikePartsId == cartRequest.BikePartsId);

                if (existingCartItem != null)
                {
                    // If part exists, update the quantity and price
                    existingCartItem.Quantity += cartRequest.Quantity;
                    existingCartItem.TotalPrice = existingCartItem.Quantity * bikePart.Price;

                    // Update the cart item in the database
                    try
                    {
                        var result = await _cartService.UpdateCartItem(existingCartItem);
                        if (result)
                        {
                            return Ok(new { success = true, message = "Cart item updated successfully." });
                        }
                        else
                        {
                            return BadRequest(new { success = false, message = "Failed to update cart item." });
                        }
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
                    }
                }
                else
                {
                    // If the part doesn't exist in the cart, create a new cart item
                    var newCartItem = new Cart
                    {
                        UserId = userId,
                        BikePartsId = cartRequest.BikePartsId,
                        Quantity = cartRequest.Quantity,
                        TotalPrice = totalPrice,
                        DateAdded = DateTime.Now,
                        IsPaymentDone = false
                    };

                    try
                    {
                        var result = await _cartService.AddToCart(newCartItem);
                        if (result)
                        {
                            return Ok(new { success = true, message = "Item added to cart successfully." });
                        }
                        else
                        {
                            return BadRequest(new { success = false, message = "Failed to add item to cart." });
                        }
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
                    }
                }
            }
            else
            {
                // If the cart item exists, update the quantity and price
                var bikePart = await _bikePartsService.GetBikePartById(existingCart.BikePartsId);
                if (bikePart == null)
                {
                    return NotFound(new { success = false, message = "Bike part not found." });
                }

                if (cartRequest.Quantity > bikePart.Quantity)
                {
                    return BadRequest(new { success = false, message = "Insufficient stock for the requested quantity." });
                }

                // Update the cart item with the new quantity
                existingCart.Quantity = cartRequest.Quantity;
                existingCart.TotalPrice = existingCart.Quantity * bikePart.Price; // Recalculate the total price

                try
                {
                    var result = await _cartService.UpdateCartItem(existingCart);
                    if (result)
                    {
                        return Ok(new { success = true, message = "Cart item updated successfully." });
                    }
                    else
                    {
                        return BadRequest(new { success = false, message = "Failed to update cart item." });
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
                }
            }
        }



        // Delete Cart Items by User ID
        [Authorize(Roles = "User")]
        [HttpDelete("user/{userId}")]
        public async Task<IActionResult> DeleteCartItemByUserId(int userId)
        {
            int loggedInUserId = GetUserIdFromToken();
            if (loggedInUserId == 0 || loggedInUserId != userId)
                return Unauthorized("User not identified or mismatched.");

            var result = await _cartService.DeleteAllCartsByUserId(userId);
            if (result)
            {
                return Ok(new { success = true, message = "All carts for the user have been deleted successfully." });
            }
            return NotFound(new { success = false, message = "No carts found for the user." });
        }

        [HttpDelete("{cartId}")]
        public async Task<IActionResult> DeleteCartItemByCartId(int cartId)
        {
            // Get the userId from the token
            int userId = GetUserIdFromToken();
            if (userId == 0) return Unauthorized("User not identified.");

            try
            {
                // Fetch the cart item by cartId and userId
                var cartItem = await _cartService.GetCartItemsByUser(userId);
                var cart = cartItem.FirstOrDefault(c => c.Id == cartId);

                if (cart == null)
                {
                    return NotFound(new { success = false, message = "Cart item not found or does not belong to this user." });
                }

                // Proceed to delete the cart item
                var result = await _cartService.DeleteCartItemById(cartId);
                if (result)
                {
                    return Ok(new { success = true, message = "Cart item deleted successfully." });
                }

                return BadRequest(new { success = false, message = "Failed to delete cart item." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }




        // Admin: Delete all carts from the carts table
        [HttpDelete("admin/all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAllCarts()
        {
            var result = await _cartService.DeleteAllCarts();

            if (result)
            {
                return Ok(new { success = true, message = "All carts have been deleted successfully." });
            }

            return BadRequest(new { success = false, message = "Failed to delete carts." });
        }

    }

    public class CartRequest
    {
        public int BikePartsId { get; set; }
        public int Quantity { get; set; }
    }
}
