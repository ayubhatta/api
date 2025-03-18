namespace HomeBikeServiceAPI.DTO
{
    public class PaymentRequestModel
    {
        public string PurchaseOrderId { get; set; }
        public string PurchaseOrderName { get; set; }
        public int Amount { get; set; } // Amount in paisa (1 NPR = 100 paisa)
        public string ReturnUrl { get; set; }
    }

}
