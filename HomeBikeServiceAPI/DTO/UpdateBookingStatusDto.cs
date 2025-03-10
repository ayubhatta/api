namespace HomeBikeServiceAPI.DTO
{
    public class UpdateBookingStatusDto
    {
        // Nullable collection of booking Ids that the booking is assigned to
        public ICollection<int> IsAssignedTo { get; set; }
    }
}
