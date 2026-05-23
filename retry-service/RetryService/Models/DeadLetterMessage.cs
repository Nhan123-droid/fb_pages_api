namespace RetryService.Models;

public class DeadLetterMessage
{
    public int SchemaVersion { get; set; } = 1;
    public string CommandId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public int RetryCount { get; set; } = 0;
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
    public string FinalError { get; set; } = string.Empty;
    public string OriginalTopic { get; set; } = "send_failed";
    public BackendCommandMessage Payload { get; set; } = new();
}
