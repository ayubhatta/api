namespace HomeBikeServiceAPI.DTO
{
    public class MechanicUpdateDto
    {
        // Nullable list of booking Ids that a mechanic can be assigned to
        public ICollection<int>? IsAssignedTo { get; set; }
    }
}
