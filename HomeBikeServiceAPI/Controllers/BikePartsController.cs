using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.DTO;
using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        private readonly AppDbContext _context;

        public BikePartsController(BikePartsService bikePartsService, IWebHostEnvironment hostEnvironment, ILogger<BikePartsController> logger, AppDbContext context)
        {
            _bikePartsService = bikePartsService;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _context = context;
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
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError($"Failed to parse ImgBB response: {ex.Message}");
                return null;
            }
        }


        private string? GetImageUrl(string fileName)
        {
            return string.IsNullOrEmpty(fileName) ? null : $"{Request.Scheme}://{Request.Host}/Images/BikeParts/{fileName}";
        }


        //[Authorize(Roles = "Admin")]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] BikePartCreateRequest bikePart)
        {
            if (bikePart == null || bikePart.PartImage == null)
            {
                _logger.LogWarning("Bike part data or image is missing.");
                return BadRequest(new { success = false, message = "Bike part data and image are required." });
            }

            try
            {
                // Deserialize the JSON string (CompatibleBikesJson) into a Dictionary
                Dictionary<string, List<string>> compatibleBikes = new Dictionary<string, List<string>>();

                if (!string.IsNullOrEmpty(bikePart.CompatibleBikesJson))
                {
                    compatibleBikes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(bikePart.CompatibleBikesJson);
                }

                // Resize image
                var resizedImageBytes = await ResizeImage(bikePart.PartImage);

                // Convert image to Base64
                string base64Image = Convert.ToBase64String(resizedImageBytes);

                // Upload to ImgBB
                var imgbbUrl = await UploadToImgBB(bikePart.PartImage);
                if (string.IsNullOrEmpty(imgbbUrl))
                {
                    return StatusCode(500, new { success = false, message = "Failed to upload image to ImgBB." });
                }

                // Create BikeParts object
                var newBikePart = new BikeParts
                {
                    PartName = bikePart.PartName,
                    Price = bikePart.Price,
                    Description = bikePart.Description,
                    Quantity = bikePart.Quantity,
                    PartImage = imgbbUrl, // Store ImgBB URL
                    CompatibleBikesJson = bikePart.CompatibleBikesJson, // Store as string
                };

                // Save to database
                var result = await _bikePartsService.CreateBikePart(newBikePart);

                if (!result)
                {
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
                        CompatibleBikesJson = newBikePart.CompatibleBikesJson, // Return the JSON
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating BikePart. Exception: {ExceptionMessage}", ex.Message);
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetGroupedBikeParts()
        {
            var bikeParts = await _context.BikeParts.ToListAsync(); // Get all bike parts from the database
            var groupedParts = new List<object>(); // To hold the grouped data

            // Group parts by the brand (keys of CompatibleBikesJson)
            var bikeBrands = new Dictionary<string, object>();

            foreach (var part in bikeParts)
            {
                // Deserialize the CompatibleBikesJson into a dictionary
                var compatibleBikes = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(part.CompatibleBikesJson);

                foreach (var brand in compatibleBikes!.Keys)
                {
                    // If the brand is not in the list, add it
                    if (!bikeBrands.ContainsKey(brand))
                    {
                        bikeBrands[brand] = new List<object>();
                    }

                    var brandModels = (List<object>)bikeBrands[brand];

                    // Process each model for the brand
                    foreach (var model in compatibleBikes[brand])
                    {
                        var existingModel = brandModels.FirstOrDefault(m => ((dynamic)m).bikeModel == model);
                        if (existingModel == null)
                        {
                            // If model doesn't exist, create it and add it
                            var newModel = new
                            {
                                bikeModel = model,
                                parts = new List<object>
                        {
                            new
                            {
                                id = part.Id,
                                partName = part.PartName,
                                price = part.Price,
                                description = part.Description,
                                quantity = part.Quantity,
                                compatibleBikes = compatibleBikes,
                                partImageUrl = part.PartImage
                            }
                        }
                            };
                            brandModels.Add(newModel);
                        }
                        else
                        {
                            // If model exists, just add the part
                            var partsList = (List<object>)((dynamic)existingModel).parts;
                            partsList.Add(new
                            {
                                id = part.Id,
                                partName = part.PartName,
                                price = part.Price,
                                description = part.Description,
                                quantity = part.Quantity,
                                compatibleBikes = compatibleBikes,
                                partImageUrl = part.PartImage
                            });
                        }
                    }
                }
            }

            // Convert the dictionary into the final structure
            foreach (var brand in bikeBrands.Keys)
            {
                groupedParts.Add(new
                {
                    bikeBrand = brand,
                    bikeModels = bikeBrands[brand]
                });
            }

            // Return the final result
            return Ok(new
            {
                success = true,
                message = "Bike parts grouped by brand and model.",
                data = groupedParts
            });
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
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromForm] BikePartCreateRequest updateRequest)
        {
            try
            {
                // Fetch the existing bike part from the database
                var existingPart = await _bikePartsService.GetBikePartById(id);
                if (existingPart == null)
                {
                    return NotFound(new { success = false, message = $"Bike part with ID {id} not found." });
                }

                // Update the fields
                existingPart.PartName = updateRequest.PartName;
                existingPart.Price = updateRequest.Price;
                existingPart.Description = updateRequest.Description;
                existingPart.Quantity = updateRequest.Quantity;

                // Handle the update of CompatibleBikesJson field (deserialize and update)
                if (!string.IsNullOrEmpty(updateRequest.CompatibleBikesJson))
                {
                    // Deserialize the JSON string (CompatibleBikesJson) into a Dictionary
                    var compatibleBikes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(updateRequest.CompatibleBikesJson);

                    // Serialize the object back to a compact JSON string (removing spaces and line breaks)
                    existingPart.CompatibleBikesJson = System.Text.Json.JsonSerializer.Serialize(compatibleBikes, new JsonSerializerOptions { WriteIndented = false });
                }

                // Handle image update if a new image is provided
                if (updateRequest.PartImage != null)
                {
                    var resizedImageBytes = await ResizeImage(updateRequest.PartImage);

                    // Convert image to Base64 (optional, depending on use case)
                    string base64Image = Convert.ToBase64String(resizedImageBytes);

                    // Upload to ImgBB
                    var imgbbUrl = await UploadToImgBB(updateRequest.PartImage);
                    if (imgbbUrl == null)
                    {
                        return StatusCode(500, new { success = false, message = "Failed to upload image to ImgBB." });
                    }

                    // Update image URL
                    existingPart.PartImage = imgbbUrl;
                }

                // Save the updated bike part to the database
                var result = await _bikePartsService.UpdateBikePart(existingPart);
                if (!result)
                {
                    return BadRequest(new { success = false, message = "Failed to update bike part." });
                }

                // Return the updated bike part details
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
                        CompatibleBikesJson = existingPart.CompatibleBikesJson // Return the updated JSON
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
        [Authorize(Roles = "Admin")]
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

        [Authorize(Roles = "Admin")]
        [HttpDelete("delete-all")]
        public async Task<IActionResult> DeleteAll()
        {
            try
            {
                _context.BikeParts.RemoveRange(_context.BikeParts);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "All bike parts deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting all bike parts.");
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
            }
        }
    }
}
