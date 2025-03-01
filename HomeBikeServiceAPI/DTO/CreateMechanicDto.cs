namespace HomeBikeServiceAPI.DTO
{
    public class CreateMechanicDto
    {
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public int? IsAssignedTo { get; set; }  // Nullable to handle default null values
    }

}
