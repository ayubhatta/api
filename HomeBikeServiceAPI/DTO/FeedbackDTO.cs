using System.ComponentModel.DataAnnotations;

public class FeedbackDTO
{
    public int UserId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Subject { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
