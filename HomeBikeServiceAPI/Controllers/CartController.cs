using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
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

        public CartController(CartService cartService, BikePartsService bikePartsService)
        {
            _cartService = cartService;
            _bikePartsService = bikePartsService;
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
                        var imageUrl = bikePart.PartImage != null
                            ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/BikeParts/{bikePart.PartImage}"
                            : null;

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
