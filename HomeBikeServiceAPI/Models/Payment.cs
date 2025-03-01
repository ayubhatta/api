using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeBikeServiceAPI.Models
{
    public class Payment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Ensures auto-increment
        public int Id { get; set; } 

        public string TransactionId { get; set; }

        public string Pidx { get; set; }

        [Required]
        public List<int> Bookings { get; set; } = new List<int>();

        public decimal Amount { get; set; }

        public string DataFromVerificationReq { get; set; } 

        public string ApiQueryFromUser { get; set; } 

        [Required]
        [EnumDataType(typeof(PaymentGatewayType))]
        public PaymentGatewayType PaymentGateway { get; set; }

        [Required]
        [EnumDataType(typeof(PaymentStatus))]
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public enum PaymentGatewayType
    {
        Khalti,
        Esewa,
        ConnectIps
    }

    public enum PaymentStatus
    {
        Success,
        Pending,
        Failed
    }
}
