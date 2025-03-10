using HomeBikeServiceAPI.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

public class Mechanic
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string PhoneNumber { get; set; }

    // Store as JSON string in database
    public string? IsAssignedToJson { get; set; }

    [NotMapped] // Not mapped directly to the database
    public ICollection<int>? IsAssignedTo
    {
        get => string.IsNullOrEmpty(IsAssignedToJson) ? new List<int>()
               : JsonSerializer.Deserialize<List<int>>(IsAssignedToJson);
        set => IsAssignedToJson = value != null ? JsonSerializer.Serialize(value) : null;
    }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    [ForeignKey("UserId")]
    public int? UserId { get; set; }
    public User User { get; set; }
}
