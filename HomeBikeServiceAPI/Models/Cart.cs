using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeBikeServiceAPI.Models
{
    public class Cart
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; }

        [Required]
        [ForeignKey("BikeParts")]
        public int BikePartsId { get; set; }

        public BikeParts BikeParts { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public decimal TotalPrice { get; set; }

        [Required]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [Required]
        public bool IsPaymentDone { get; set; } = false;
    }
}
