namespace Page_API.Models
{
    public class SendFailedMessage
    {
        public int SchemaVersion { get; set; } = 1;
        public string CommandId { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty;
        public int RetryCount { get; set; } = 0;
        public string LastError { get; set; } = string.Empty;
        public DateTime NextRetryAt { get; set; }
        public BackendCommandMessage Payload { get; set; } = new();
    }
}
