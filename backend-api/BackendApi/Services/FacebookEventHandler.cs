using Confluent.Kafka;
using Page_API.Data;
using Page_API.Models;
using System.Text.Json;

namespace Page_API.Services
{
    public class FacebookEventHandler : IFacebookEventHandler
    {
        private readonly ILogger<FacebookEventHandler> _logger;
        private readonly AppDbContext _dbContext;
        private readonly IFacebookService _facebookService;
        private readonly IProducer<string, string> _kafkaProducer;
        private readonly string _failedTopic;

        public FacebookEventHandler(
            ILogger<FacebookEventHandler> logger, 
            AppDbContext dbContext,
            IFacebookService facebookService,
            IProducer<string, string> kafkaProducer,
            IConfiguration configuration)
        {
            _logger = logger;
            _dbContext = dbContext;
            _facebookService = facebookService;
            _kafkaProducer = kafkaProducer;
            _failedTopic = configuration["KafkaProducer:SendFailedTopic"] ?? "send_failed";
        }

        public async Task HandleAsync(BackendCommandMessage command, CancellationToken cancellationToken)
        {
            // 1. Kiểm tra Idempotency
            var trackedCmd = _dbContext.ProcessedCommands.FirstOrDefault(c => c.CommandId == command.CommandId);
            if (trackedCmd != null && (trackedCmd.Status == "processed" || trackedCmd.Status == "replied"))
            {
                _logger.LogInformation("Command {CommandId} was already processed successfully. Skipping.", command.CommandId);
                return;
            }

            if (trackedCmd == null)
            {
                trackedCmd = new ProcessedCommand
                {
                    CommandId = command.CommandId,
                    EventId = command.EventId,
                    Action = command.Action,
                    Status = "received",
                    PageId = command.Target.PageId,
                    CommentId = command.Target.CommentId,
                    UserMessage = command.OriginalMessage,
                    BotReply = command.ReplyText,
                    Intent = command.Intent,
                    Sentiment = command.Sentiment
                };
                _dbContext.ProcessedCommands.Add(trackedCmd);
            }

            try
            {
                // 2. Xử lý hành động
                if (command.Action == "reply" && !string.IsNullOrWhiteSpace(command.ReplyText))
                {
                    await _facebookService.ReplyToCommentAsync(command.Target.CommentId, command.ReplyText, cancellationToken);
                    _logger.LogInformation("Replied to comment {CommentId} with: {Text}", command.Target.CommentId, command.ReplyText);
                }
                else if (command.Action == "hide" || command.Action == "hide_and_review" || command.Action == "blacklist_block")
                {
                    await _facebookService.HideCommentAsync(command.Target.CommentId, cancellationToken);
                    _logger.LogInformation("Hid comment {CommentId} on Facebook due to Action: {Action}", command.Target.CommentId, command.Action);
                }
                else
                {
                    _logger.LogInformation("No action taken for command {CommandId} (Action: {Action})", command.CommandId, command.Action);
                }

                // 3. Cập nhật lịch sử xử lý thành công
                trackedCmd.Status = command.Action == "reply" ? "replied" : "processed";
                trackedCmd.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute command {CommandId}", command.CommandId);

                // 4. Publish sang topic send_failed nếu có lỗi
                var failedMsg = new SendFailedMessage
                {
                    CommandId = command.CommandId,
                    EventId = command.EventId,
                    RetryCount = command.RetryCount,
                    LastError = ex.Message,
                    NextRetryAt = DateTime.UtcNow,
                    Payload = command
                };

                var json = JsonSerializer.Serialize(failedMsg, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await _kafkaProducer.ProduceAsync(_failedTopic, new Message<string, string>
                {
                    Key = command.CommandId,
                    Value = json
                }, cancellationToken);
                
                
                _logger.LogInformation("Published failed command {CommandId} to {Topic}", command.CommandId, _failedTopic);

                // Cập nhật trạng thái failed
                trackedCmd.Status = "failed";
                trackedCmd.ErrorMessage = ex.Message;
                trackedCmd.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
