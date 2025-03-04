using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeBikeServiceAPI.Models
{
    public class BikeParts
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string PartName { get; set; }

        [Required]
        public decimal Price { get; set; }

        [Required]
        [MaxLength(200)]
        public string? Description { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [MaxLength(1000)]
        public string PartImage { get; set; } 

        [Required]
        public List<string> CompatibleBikes { get; set; } = new List<string>(); // New Field
    }
}
