using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace HomeBikeServiceAPI.Models
{
    public class Mechanic
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        [ForeignKey("Booking")]
        public int? IsAssignedTo { get; set; } // Nullable, defaults to null
                                               // A mechanic can be assigned to multiple bookings
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        [ForeignKey("UserId")]
        public int? UserId { get; set; } // Nullable, foreign key from Users table
        public User User { get; set; } // Navigation property to User    }
    }
}
