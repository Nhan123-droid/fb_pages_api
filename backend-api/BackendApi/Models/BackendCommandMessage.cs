namespace Page_API.Models
{
    public class BackendCommandMessage
    {
        public int SchemaVersion { get; set; } = 1;
        public string CommandId { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public TargetInfo Target { get; set; } = new();
        public string ReplyText { get; set; } = string.Empty;
        
        // Dùng cho retry (nếu gửi thẳng từ retry_service thì đọc trường này)
        public int RetryCount { get; set; } = 0;
    }

    public class TargetInfo
    {
        public string PageId { get; set; } = string.Empty;
        public string CommentId { get; set; } = string.Empty;
    }
}
