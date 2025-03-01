using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeBikeServiceAPI.Models
{
    public class BikeProduct
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; } 

        [Required]
        [MaxLength(50)] 
        public string BikeName { get; set; }

        [Required]
        [MaxLength(50)]
        public decimal BikePrice { get; set; } 

        [Required]
        [MaxLength(1000)] 
        public string BikeImage { get; set; } 

        [Required]
        [MaxLength(50)] 
        public string BikeModel { get; set; } 
    }
}
