namespace CoreService.Models;

public class NormalizedFacebookEvent
{
    public string SchemaVersion { get; set; } = "1.0";
    public bool IsPendingReview { get; set; } = false;
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = "facebook";
    public string PageId { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string CommentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
