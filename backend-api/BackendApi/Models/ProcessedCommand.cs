using System.ComponentModel.DataAnnotations;

namespace Page_API.Models
{
    public class ProcessedCommand
    {
        [Key]
        public string CommandId { get; set; } = string.Empty;

        public string EventId { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
