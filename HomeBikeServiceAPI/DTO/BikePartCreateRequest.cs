using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeBikeServiceAPI.DTO
{
    public class BikePartCreateRequest
    {
        [Required]
        [MaxLength(50)]
        public string PartName { get; set; }
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Bike price must be greater than 0.")]
        public decimal Price { get; set; }
        [Required]
        [MaxLength(200)]
        public string? Description { get; set; }
        [Required]
        public int Quantity { get; set; }
        [Required(ErrorMessage = "Bike image is required.")]
        public IFormFile PartImage { get; set; }
    }
}
