using HomeBikeServiceAPI.Models;
using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using HomeBikeServiceAPI.Services;
using Microsoft.AspNetCore.Hosting;

namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BikeProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<BikeProductsController> _logger;

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


        private string GetImageUrl(string imageFileName)
        {
            var imagePath = $"Images/BikeProducts/{imageFileName}"; // Ensure correct format with forward slashes
            var imageUrl = $"{Request.Scheme}://{Request.Host}/{imagePath}";
            return imageUrl;
        }



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

                // Determine file storage path
                /*var rootPath = _hostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var imagesDirectory = Path.Combine(rootPath, "BikeProducts");*/
                var imagesDirectory = Path.Combine("Images", "BikeProducts");
                Directory.CreateDirectory(imagesDirectory);

                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(request.BikeImage.FileName)}";
                var filePath = Path.Combine(imagesDirectory, fileName);

                // Save the image file
                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.BikeImage.CopyToAsync(stream);
                }

                // Save the BikeProduct to the database
                var bikeProduct = new BikeProduct
                {
                    BikeName = request.BikeName.Trim(),
                    BikeModel = request.BikeModel.Trim(),
                    BikePrice = request.BikePrice,
                    BikeImage = fileName
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
                        BikeImageUrl = GetImageUrl(bikeProduct.BikeImage)
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
                    BikeImageUrl = GetImageUrl(bike.BikeImage)
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
                        BikeImageUrl = GetImageUrl(bike.BikeImage)
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
                    BikeImageUrl = GetImageUrl(bike.BikeImage)
                });

                return Ok(new { success = true, message = "Bikes fetched by bike name", bikes = bikeResponses });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }

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
                    // Delete the old image
                    //var oldImagePath = Path.Combine(_hostEnvironment.WebRootPath, "BikeProducts", existingBike.BikeImage);
                    var oldImagePath = Path.Combine("Images", "BikeProducts", existingBike.BikeImage);
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }

                    // Save the new image
                    var fileName = $"{Guid.NewGuid()}_{request.BikeImage.FileName}";
                    //var filePath = Path.Combine(_hostEnvironment.WebRootPath, "BikeProducts", fileName);
                    var filePath = Path.Combine("Images", "BikeProducts", fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await request.BikeImage.CopyToAsync(stream);
                    }

                    existingBike.BikeImage = fileName;
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
                        BikeImageUrl = GetImageUrl(existingBike.BikeImage)
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }

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
                var imagePath = Path.Combine("Images", "BikeProducts", bike.BikeImage); // Updated path
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }

                _context.BikeProducts.Remove(bike);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Bike deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }
    }
}
