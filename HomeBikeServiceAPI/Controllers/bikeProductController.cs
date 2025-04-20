using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;

namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BikeProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<BikeProductsController> _logger;
        private const string ImageFolder = "Images/BikeProducts";
        private const int MaxImageWidth = 1024;  // Max width for resizing images
        public BikeProductsController(AppDbContext context, IWebHostEnvironment hostEnvironment, ILogger<BikeProductsController> logger)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
        }

        // Helper method to generate the full URL for bike images
        /*private string GetImageUrl(string imageFileName)
        {
            var rootPath = _hostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var imagePath = Path.Combine("BikeProducts", imageFileName); // Assuming images are stored in wwwroot/BikeProducts
            var imageUrl = $"{Request.Scheme}://{Request.Host}/{imagePath}";
            return imageUrl;
        }*/



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


        private string GetImageUrl(string imageFileName)
        {
            var imagePath = $"Images/BikeProducts/{imageFileName}"; // Ensure correct format with forward slashes
            var imageUrl = $"{Request.Scheme}://{Request.Host}/{imagePath}";
            return imageUrl;
        }


        [Authorize(Roles = "Admin")]
        // POST: api/BikeProducts/create
        [HttpPost("create")]
        public async Task<IActionResult> CreateBikeProduct(BikeProductCreateRequest request)
        {
            if (request == null || request.BikeImage == null)
                return BadRequest(new { message = "Invalid data or missing image." });

            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.BikeName) ||
                    string.IsNullOrWhiteSpace(request.BikeModel) ||
                    request.BikePrice <= 0)
                {
                    return BadRequest(new { message = "Please fill in all required fields." });
                }

                // Resize image before Base64 conversion
                var resizedImageBytes = await ResizeImage(request.BikeImage);

                // Convert the resized image to Base64
                string base64Image = Convert.ToBase64String(resizedImageBytes);
                _logger.LogInformation("Base64 Image Length: {Length} bytes", base64Image.Length);

                // Upload to ImgBB
                var imgbbUrl = await UploadToImgBB(request.BikeImage);
                if (string.IsNullOrEmpty(imgbbUrl))
                {
                    _logger.LogError("Failed to upload image to ImgBB. Image size: {Size} bytes", request.BikeImage.Length);
                    return StatusCode(500, new { success = false, message = "Failed to upload image to ImgBB." });
                }

                    // Save the BikeProduct to the database
                    var bikeProduct = new BikeProduct
                {
                    BikeName = request.BikeName.Trim(),
                    BikeModel = request.BikeModel.Trim(),
                    BikePrice = request.BikePrice,
                    BikeImage = imgbbUrl
                    };

                await _context.BikeProducts.AddAsync(bikeProduct);
                await _context.SaveChangesAsync();

                // Return the full image URL in the response
                return CreatedAtAction(nameof(GetBikeProduct), new { id = bikeProduct.Id }, new
                {
                    message = "Bike product created successfully.",
                    bikeProduct = new
                    {
                        bikeProduct.Id,
                        bikeProduct.BikeName,
                        bikeProduct.BikeModel,
                        bikeProduct.BikePrice,
                        BikeImageUrl = bikeProduct.BikeImage
                    }
                });
            }
            catch (DirectoryNotFoundException dirEx)
            {
                _logger.LogError(dirEx, "Directory not found while saving bike image.");
                return StatusCode(500, new { message = "Server error: Directory not found.", error = dirEx.Message });
            }
            catch (IOException ioEx)
            {
                _logger.LogError(ioEx, "IO error while saving bike image.");
                return StatusCode(500, new { message = "Server error: Unable to save image file.", error = ioEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating BikeProduct.");
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }

        // GET: api/BikeProducts
        [HttpGet("all")]
        public async Task<IActionResult> GetAllBikes()
        {
            try
            {
                var bikes = await _context.BikeProducts.ToListAsync();
                var bikeResponses = bikes.Select(bike => new
                {
                    bike.Id,
                    bike.BikeName,
                    bike.BikeModel,
                    bike.BikePrice,
                    BikeImageUrl = bike.BikeImage
                });

                return Ok(new { success = true, message = "All Bikes Fetched", bikes = bikeResponses });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }

        // GET: api/BikeProducts/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBikeProduct(int id)
        {
            try
            {
                var bike = await _context.BikeProducts.FindAsync(id);
                if (bike == null)
                {
                    return NotFound(new { message = "Bike not found." });
                }

                // Return the full image URL in the response
                return Ok(new
                {
                    success = true,
                    bike = new
                    {
                        bike.Id,
                        bike.BikeName,
                        bike.BikeModel,
                        bike.BikePrice,
                        BikeImageUrl = bike.BikeImage
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }

        // GET: api/BikeProducts/bikeName/{bikeName}
        [HttpGet("bikeName/{bikeName}")]
        public async Task<IActionResult> GetBikesByBikeName(string bikeName)
        {
            try
            {
                // Use EF.Functions.Like for a case-insensitive search
                var bikes = await _context.BikeProducts
                    .Where(b => EF.Functions.Like(b.BikeName, $"{bikeName}%"))
                    .ToListAsync();

                if (bikes == null || bikes.Count == 0)
                {
                    return NotFound(new { message = "No bikes found with the specified bike name." });
                }

                var bikeResponses = bikes.Select(bike => new
                {
                    bike.Id,
                    bike.BikeName,
                    bike.BikeModel,
                    bike.BikePrice,
                    BikeImageUrl = bike.BikeImage
                });

                return Ok(new { success = true, message = "Bikes fetched by bike name", bikes = bikeResponses });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        // PUT: api/BikeProducts/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBikeProduct(int id, [FromForm] BikeProductCreateRequest request)
        {
            try
            {
                var existingBike = await _context.BikeProducts.FindAsync(id);
                if (existingBike == null)
                {
                    return NotFound(new { message = "Bike not found." });
                }

                // Update image if provided
                if (request.BikeImage != null)
                {
                    var resizedImageBytes = await ResizeImage(request.BikeImage);
                    var base64Image = Convert.ToBase64String(resizedImageBytes);

                    var imgbbUrl = await UploadToImgBB(request.BikeImage);
                    if (imgbbUrl == null)
                    {
                        return StatusCode(500, new { success = false, message = "Failed to upload image to ImgBB." });
                    }

                    existingBike.BikeImage = imgbbUrl;
                }

                // Update other fields
                existingBike.BikeName = request.BikeName;
                existingBike.BikeModel = request.BikeModel;
                existingBike.BikePrice = request.BikePrice;

                _context.BikeProducts.Update(existingBike);
                await _context.SaveChangesAsync();

                // Return updated bike with image URL
                return Ok(new
                {
                    success = true,
                    message = "Bike updated successfully.",
                    bike = new
                    {
                        existingBike.Id,
                        existingBike.BikeName,
                        existingBike.BikeModel,
                        existingBike.BikePrice,
                        BikeImageUrl = existingBike.BikeImage
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        // DELETE: api/BikeProducts/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBikeProduct(int id)
        {
            try
            {
                var bike = await _context.BikeProducts.FindAsync(id);
                if (bike == null)
                {
                    return NotFound(new { message = "Bike not found." });
                }

                // Delete the associated image
                //var imagePath = Path.Combine(_hostEnvironment.WebRootPath, "BikeProducts", bike.BikeImage);
                /*var imagePath = Path.Combine("Images", "BikeProducts", bike.BikeImage); // Updated path
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }*/

                _context.BikeProducts.Remove(bike);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Bike deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("delete-all")]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                _context.BikeProducts.RemoveRange(_context.BikeProducts);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "All bike products deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating BikeProduct.");
                return StatusCode(500, new { message = "Internal Server Error", error = ex.InnerException?.Message ?? ex.Message });
            }

        }

    }
}
