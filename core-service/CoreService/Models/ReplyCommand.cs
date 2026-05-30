namespace CoreService.Models;

public class ReplyCommand
{
    public int SchemaVersion { get; set; } = 1;
    public string CommandId { get; set; } = Guid.NewGuid().ToString();
    public string EventId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public TargetInfo Target { get; set; } = new();
    public string ReplyText { get; set; } = string.Empty;
    public string OriginalMessage { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public class TargetInfo
    {
        public string PageId { get; set; } = string.Empty;
        public string CommentId { get; set; } = string.Empty;
    }
}
