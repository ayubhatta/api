using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace HomeBikeServiceAPI.DTO
{
    public class BikeProductCreateRequest
    {
        [Required]
        [MaxLength(550)]
        public string BikeName { get; set; }

        [Required]
        [MaxLength(50)]
        public string BikeModel { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Bike price must be greater than 0.")]
        public decimal BikePrice { get; set; }

        [Required(ErrorMessage = "Bike image is required.")]
        public IFormFile BikeImage { get; set; }
    }
}
