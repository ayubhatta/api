using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        // Keep CompatibleBikes as a Dictionary
        [NotMapped] // We don’t store this directly in the DB
        public Dictionary<string, List<string>> CompatibleBikes { get; set; } = new Dictionary<string, List<string>>();

        // Store CompatibleBikes as a JSON string in the database column
        [Required]
        [Column(TypeName = "NVARCHAR(MAX)")]
        public string CompatibleBikesJson
        {
            get => JsonSerializer.Serialize(CompatibleBikes);
            set => CompatibleBikes = string.IsNullOrEmpty(value)
                ? new Dictionary<string, List<string>>()
                : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(value) ?? new Dictionary<string, List<string>>();
        }
    }
}
