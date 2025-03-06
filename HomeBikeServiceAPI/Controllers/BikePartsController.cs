using HomeBikeServiceAPI.DTO;
using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BikePartsController : ControllerBase
    {
        private readonly BikePartsService _bikePartsService;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<BikePartsController> _logger;
        private const string ImageFolder = "Images/BikeParts";
        private const int MaxImageWidth = 1024;  // Max width for resizing images

        public BikePartsController(BikePartsService bikePartsService, IWebHostEnvironment hostEnvironment, ILogger<BikePartsController> logger)
        {
            _bikePartsService = bikePartsService;
            _hostEnvironment = hostEnvironment;
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










        private string GetImageUrl(string fileName)
        {
            return string.IsNullOrEmpty(fileName) ? null : $"{Request.Scheme}://{Request.Host}/Images/BikeParts/{fileName}";
        }




        // Create a Bike Part with Image Upload
        //[Authorize(Roles = "Admin")]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] BikePartCreateRequest bikePart)
        {
            if (bikePart == null || bikePart.PartImage == null)
            {
                return BadRequest(new { success = false, message = "Bike part data and image are required." });
            }

            try
            {
                // Resize image before Base64 conversion
                var resizedImageBytes = await ResizeImage(bikePart.PartImage);

                // Convert the resized image to Base64
                string base64Image = Convert.ToBase64String(resizedImageBytes);
                _logger.LogInformation("Base64 Image Length: {Length} bytes", base64Image.Length);

                // Upload to ImgBB
                var imgbbUrl = await UploadToImgBB(bikePart.PartImage);
                if (string.IsNullOrEmpty(imgbbUrl))
                {
                    _logger.LogError("Failed to upload image to ImgBB. Image size: {Size} bytes", bikePart.PartImage.Length);
                    return StatusCode(500, new { success = false, message = "Failed to upload image to ImgBB." });
                }

                // Create BikePart object
                var newBikePart = new BikeParts
                {
                    PartName = bikePart.PartName,
                    Price = bikePart.Price,
                    Description = bikePart.Description,
                    Quantity = bikePart.Quantity,
                    PartImage = imgbbUrl, // Store ImgBB URL
                    CompatibleBikes = bikePart.CompatibleBikes ?? new List<string>()
                };

                var result = await _bikePartsService.CreateBikePart(newBikePart);
                if (!result)
                {
                    _logger.LogError("Failed to save BikePart to the database: {@BikePart}", newBikePart);
                    return BadRequest(new { success = false, message = "Failed to create bike part." });
                }

                return CreatedAtAction(nameof(GetById), new { id = newBikePart.Id }, new
                {
                    success = true,
                    message = "Bike part created successfully.",
                    data = new
                    {
                        newBikePart.Id,
                        newBikePart.PartName,
                        newBikePart.Price,
                        newBikePart.Description,
                        newBikePart.Quantity,
                        PartImageUrl = newBikePart.PartImage,
                        newBikePart.CompatibleBikes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating BikePart.");
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
            }
        }



        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var bikeParts = await _bikePartsService.GetAllBikeParts();

                if (bikeParts == null || !bikeParts.Any())
                {
                    return NotFound(new { success = false, message = "No bike parts found." });
                }

                var groupedBikeParts = bikeParts
                    .Where(bp => bp.CompatibleBikes != null && bp.CompatibleBikes.Any()) // Ensure no null errors
                    .SelectMany(bp => bp.CompatibleBikes.Select(bike => new { bike, Part = bp }))
                    .GroupBy(x => x.bike)
                    .Select(group => new
                    {
                        BikeName = group.Key,
                        // Await image URL before constructing the response
                        Parts = group.Select(async x => new
                        {
                            x.Part.Id,
                            x.Part.PartName,
                            x.Part.Price,
                            x.Part.Description,
                            x.Part.Quantity,
                            x.Part.CompatibleBikes,
                            PartImageUrl = await GetImgBBUrlAsync(x.Part.PartImage) // Dynamically fetch ImgBB URL if required
                        }).ToList()
                    })
                    .ToList();

                // Wait for all asynchronous operations to complete
                var finalGroupedBikeParts = new List<object>();
                foreach (var group in groupedBikeParts)
                {
                    var parts = await Task.WhenAll(group.Parts); // Ensure all async tasks are awaited
                    finalGroupedBikeParts.Add(new { group.BikeName, Parts = parts });
                }

                return Ok(new { success = true, message = "Bike parts grouped by bike name.", data = finalGroupedBikeParts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bike parts.");
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
            }
        }



        // Get Bike Part by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var bikePart = await _bikePartsService.GetBikePartById(id);
            if (bikePart == null)
            {
                return NotFound(new { success = false, message = $"Bike part with ID {id} not found." });
            }

            return Ok(new
            {
                success = true,
                bikePart = new
                {
                    bikePart.Id,
                    bikePart.PartName,
                    bikePart.Price,
                    bikePart.Description,
                    bikePart.Quantity,
                    PartImageUrl = await GetImgBBUrlAsync(bikePart.PartImage), // Dynamically fetch ImgBB URL
                    bikePart.CompatibleBikes
                }
            });
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



        // Update a Bike Part with Image Upload
        //[Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] BikePartCreateRequest updateRequest)
        {
            try
            {
                var existingPart = await _bikePartsService.GetBikePartById(id);
                if (existingPart == null)
                {
                    return NotFound(new { success = false, message = $"Bike part with ID {id} not found." });
                }

                // Update fields
                existingPart.PartName = updateRequest.PartName;
                existingPart.Price = updateRequest.Price;
                existingPart.Description = updateRequest.Description;
                existingPart.Quantity = updateRequest.Quantity;
                existingPart.CompatibleBikes = updateRequest.CompatibleBikes ?? new List<string>();

                // Handle image update
                if (updateRequest.PartImage != null)
                {
                    var resizedImageBytes = await ResizeImage(updateRequest.PartImage);
                    var base64Image = Convert.ToBase64String(resizedImageBytes);

                    var imgbbUrl = await UploadToImgBB(updateRequest.PartImage);
                    if (imgbbUrl == null)
                    {
                        return StatusCode(500, new { success = false, message = "Failed to upload image to ImgBB." });
                    }

                    existingPart.PartImage = imgbbUrl;
                }

                var result = await _bikePartsService.UpdateBikePart(existingPart);
                if (!result)
                {
                    return BadRequest(new { success = false, message = "Failed to update bike part." });
                }

                return Ok(new
                {
                    success = true,
                    message = "Bike part updated successfully.",
                    bikePart = new
                    {
                        existingPart.Id,
                        existingPart.PartName,
                        existingPart.Price,
                        existingPart.Description,
                        existingPart.Quantity,
                        PartImageUrl = existingPart.PartImage,
                        existingPart.CompatibleBikes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating BikePart.");
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
            }
        }

        // Delete a Bike Part
        //[Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existingPart = await _bikePartsService.GetBikePartById(id);
                if (existingPart == null)
                {
                    return NotFound(new { success = false, message = $"Bike part with ID {id} not found." });
                }

                var result = await _bikePartsService.DeleteBikePart(id);
                if (result)
                {
                    return Ok(new { success = true, message = $"Bike part with ID {id} deleted successfully." });
                }

                return BadRequest(new { success = false, message = "Failed to delete bike part." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting BikePart.");
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
            }
        }
    }
}
