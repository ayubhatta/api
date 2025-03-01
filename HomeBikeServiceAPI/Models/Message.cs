using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeBikeServiceAPI.Models
{
    public class Message
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; } 

        [Required]
        [MaxLength(1000)] 
        public string Content { get; set; } 

        [Required]
        [ForeignKey("Sender")]
        public int SenderId { get; set; } 

        [Required]
        [ForeignKey("Receiver")]
        public int ReceiverId { get; set; } 

        public string Type { get; set; } = "text"; 

        [Required]
        public DateTime Timestamp { get; set; }
    }
}
