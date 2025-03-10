namespace HomeBikeServiceAPI.DTO
{
    public class CreateMechanicDto
    {
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public ICollection<int>? IsAssignedTo { get; set; }
    }

}
