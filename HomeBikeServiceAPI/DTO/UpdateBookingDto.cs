public class UpdateBookingDto
{
    public int BookingId { get; set; }  // Added BookingId to update existing booking
    public int UserId { get; set; }
    public int BikeId { get; set; }
    public string BikeChasisNumber { get; set; }
    public string BikeDescription { get; set; }
    public string BookingDate { get; set; }
    public string BookingTime { get; set; }
    public decimal? Total { get; set; }
    public string BikeNumber { get; set; }
    public string BookingAddress { get; set; }
}