using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeBikeServiceAPI.Models
{
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; } 

        [Required]
        [MaxLength(500)] 
        public string Message { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        public bool IsRead { get; set; } = false;
    }
}
