/*using HomeBikeServiceAPI.DTO;
using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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


        public BikePartsController(BikePartsService bikePartsService, IWebHostEnvironment hostEnvironment, ILogger<BikePartsController> logger)
        {
            _bikePartsService = bikePartsService;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
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
                *//*// Ensure images directory exists
                var rootPath = _hostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var imagesDirectory = Path.Combine(rootPath, ImageFolder);
                Directory.CreateDirectory(imagesDirectory);

                // Generate unique file name
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(bikePart.PartImage.FileName)}";
                var filePath = Path.Combine(imagesDirectory, fileName);*//*

                var imagesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Images", "BikeParts");
                Directory.CreateDirectory(imagesDirectory);

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(bikePart.PartImage.FileName)}";
                var filePath = Path.Combine(imagesDirectory, fileName);

                // Save image to server
                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await bikePart.PartImage.CopyToAsync(stream);
                }

                // Create new BikeParts object with CompatibleBikes support
                var newBikePart = new BikeParts
                {
                    PartName = bikePart.PartName,
                    Price = bikePart.Price,
                    Description = bikePart.Description,
                    Quantity = bikePart.Quantity,
                    PartImage = fileName,
                    CompatibleBikes = bikePart.CompatibleBikes ?? new List<string>()
                };

                var result = await _bikePartsService.CreateBikePart(newBikePart);
                if (!result)
                {
                    return BadRequest(new { success = false, message = "Failed to create bike part." });
                }

                return CreatedAtAction(nameof(GetById), new { id = newBikePart.Id }, new
                {
                    success = true,
                    message = "Bike part created successfully.",
                    bikePart = new
                    {
                        newBikePart.Id,
                        newBikePart.PartName,
                        newBikePart.Price,
                        newBikePart.Description,
                        newBikePart.Quantity,
                        PartImageUrl = GetImageUrl(newBikePart.PartImage),
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
                    .SelectMany(bp => bp.CompatibleBikes.Select(bike => new { bike, Part = bp }))
                    .GroupBy(x => x.bike)
                    .Select(group => new
                    {
                        BikeName = group.Key,
                        Parts = group.Select(x => new
                        {
                            x.Part.Id,
                            x.Part.PartName,
                            x.Part.Price,
                            x.Part.Description,
                            x.Part.Quantity,
                            x.Part.CompatibleBikes,
                            PartImageUrl = GetImageUrl(x.Part.PartImage)
                        }).ToList()
                    })
                    .ToList();

                return Ok(new { success = true, message = "Bike parts grouped by bike name.", data = groupedBikeParts });
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
            try
            {
                var bikePart = await _bikePartsService.GetBikePartById(id);
                if (bikePart == null)
                {
                    return NotFound(new { success = false, message = $"Bike part with ID {id} not found." });
                }

                // Append full image URL
                *//*var rootUrl = $"{Request.Scheme}://{Request.Host}/BikeParts/";
                bikePart.PartImage = $"{rootUrl}{bikePart.PartImage}";*//*
                bikePart.PartImage = GetImageUrl(bikePart.PartImage);

                return Ok(new { success = true, message = "Bike part retrieved successfully.", bikePart });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bike part.");
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
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
                    *//*var rootPath = _hostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var imagesDirectory = Path.Combine(rootPath, ImageFolder);
                    Directory.CreateDirectory(imagesDirectory);

                    // Delete old image if it exists
                    var oldFilePath = Path.Combine(imagesDirectory, existingPart.PartImage);*//*

                    var imagesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Images", "BikeParts");
                    Directory.CreateDirectory(imagesDirectory);

                    var oldFilePath = Path.Combine(imagesDirectory, existingPart.PartImage);
                    if (!string.IsNullOrEmpty(existingPart.PartImage) && System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }

                    // Save new image
                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(updateRequest.PartImage.FileName)}";
                    var filePath = Path.Combine(imagesDirectory, fileName);
                    await using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await updateRequest.PartImage.CopyToAsync(stream);
                    }

                    existingPart.PartImage = fileName;
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
                        PartImageUrl = GetImageUrl(existingPart.PartImage),
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

                // Delete image file from Images/BikeParts directory
                var imagesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Images", "BikeParts");
                var filePath = Path.Combine(imagesDirectory, existingPart.PartImage);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
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
*/