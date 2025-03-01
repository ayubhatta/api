using HomeBikeServiceAPI.DTO;
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

        public BikePartsController(BikePartsService bikePartsService, IWebHostEnvironment hostEnvironment, ILogger<BikePartsController> logger)
        {
            _bikePartsService = bikePartsService;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
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
                // Define directory for storing images
                var rootPath = _hostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var imagesDirectory = Path.Combine(rootPath, "BikeParts");
                Directory.CreateDirectory(imagesDirectory);

                // Save the image
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(bikePart.PartImage.FileName)}";
                var filePath = Path.Combine(imagesDirectory, fileName);
                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await bikePart.PartImage.CopyToAsync(stream);
                }

                // Create a new BikePart entity (Id will be auto-generated)
                var newBikePart = new BikeParts
                {
                    PartName = bikePart.PartName,
                    Price = bikePart.Price,
                    Description = bikePart.Description,
                    Quantity = bikePart.Quantity,
                    PartImage = fileName // Store only the filename
                };

                var result = await _bikePartsService.CreateBikePart(newBikePart);
                if (result)
                {
                    return CreatedAtAction(nameof(GetById), new { id = newBikePart.Id }, new
                    {
                        success = true,
                        message = "Bike part created successfully.",
                        bikePart = new
                        {
                            newBikePart.Id, // Including Id since it's now created
                            newBikePart.PartName,
                            newBikePart.Price,
                            newBikePart.Description,
                            newBikePart.Quantity,
                            newBikePart.PartImage
                        }
                    });
                }

                return BadRequest(new { success = false, message = "Failed to create bike part." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating BikePart.");
                return StatusCode(500, new { success = false, message = "Internal server error.", error = ex.Message });
            }
        }



        // Get All Bike Parts
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

                // Convert to list before using ForEach
                var bikePartsList = bikeParts.ToList();
                var rootUrl = $"{Request.Scheme}://{Request.Host}/BikeParts/";
                bikePartsList.ForEach(bp => bp.PartImage = $"{rootUrl}{bp.PartImage}");

                return Ok(new { success = true, message = "Bike parts retrieved successfully.", bikeParts = bikePartsList });
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
                var rootUrl = $"{Request.Scheme}://{Request.Host}/BikeParts/";
                bikePart.PartImage = $"{rootUrl}{bikePart.PartImage}";

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

                // Update fields from DTO
                existingPart.PartName = updateRequest.PartName;
                existingPart.Price = updateRequest.Price;
                existingPart.Description = updateRequest.Description;
                existingPart.Quantity = updateRequest.Quantity;

                // If a new image is uploaded, replace the old one
                if (updateRequest.PartImage != null)
                {
                    var rootPath = _hostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var imagesDirectory = Path.Combine(rootPath, "BikeParts");
                    Directory.CreateDirectory(imagesDirectory);

                    // Delete old image
                    var oldFilePath = Path.Combine(imagesDirectory, existingPart.PartImage);
                    if (System.IO.File.Exists(oldFilePath))
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
                if (result)
                {
                    return Ok(new { success = true, message = "Bike part updated successfully.", bikePart = existingPart });
                }

                return BadRequest(new { success = false, message = "Failed to update bike part." });
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

                // Delete image file
                var rootPath = _hostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var imagesDirectory = Path.Combine(rootPath, "BikeParts");
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
