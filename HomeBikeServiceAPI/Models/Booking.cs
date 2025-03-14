﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace HomeBikeServiceAPI.Models
{
    public class Booking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; } 

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }
        public int BikeId { get; set; }
        public virtual BikeProduct Bike { get; set; }

        public int? MechanicId { get; set; }
        [JsonIgnore]
        public virtual Mechanic Mechanic { get; set; }

        [MaxLength(255)] 
        public string BookingAddress { get; set; } 

        [MaxLength(1000)] 
        public string BikeDescription { get; set; } 

        public DateOnly? BookingDate { get; set; } 
        public TimeOnly? BookingTime { get; set; } 

        [Required]
        public string Status { get; set; } = "pending"; 

        public decimal? Total { get; set; } 

        [MaxLength(50)]
        public string BikeNumber { get; set; } 
    }
}
