public class BookingDto
{
    public int UserId { get; set; }
    public int BikeId { get; set; }
    public string BikeDescription { get; set; }
    public string BookingDate { get; set; }
    public string BookingTime { get; set; }
    public decimal? Total { get; set; }
    public string BikeNumber { get; set; }
    public string BookingAddress { get; set; }
}


public class BookingGetDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BikeId { get; set; }
    public string BikeDescription { get; set; }
    public DateOnly? BookingDate { get; set; }
    public TimeOnly? BookingTime { get; set; }
    public decimal? Total { get; set; }
    public string BikeNumber { get; set; }
    public string BookingAddress { get; set; }
    public string Status { get; set; }
}

