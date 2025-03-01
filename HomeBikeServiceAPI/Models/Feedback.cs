using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeBikeServiceAPI.Models
{
    public class Feedback
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [Required]
        [MaxLength(255)] 
        public string Subject { get; set; } 

        [Required]
        [MaxLength(1000)] 
        public string Message { get; set; }

        [Required]
        [Range(1, 5)] 
        public int Rating { get; set; } 

        public DateTime CreatedAt { get; set; } = DateTime.Now; 
    }
}
