using System.ComponentModel.DataAnnotations;

namespace Page_API.Models
{
    public class ProcessedCommand
    {
        [Key]
        public string CommandId { get; set; } = string.Empty;

        public string EventId { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string Status { get; set; } = "received";

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
