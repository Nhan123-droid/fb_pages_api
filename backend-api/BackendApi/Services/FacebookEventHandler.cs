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
            if (_dbContext.ProcessedCommands.Any(c => c.CommandId == command.CommandId))
            {
                _logger.LogInformation("Command {CommandId} was already processed. Skipping.", command.CommandId);
                return;
            }

            try
            {
                // 2. Xử lý hành động
                if (command.Action == "reply" && !string.IsNullOrWhiteSpace(command.ReplyText))
                {
                    await _facebookService.ReplyToCommentAsync(command.Target.CommentId, command.ReplyText, cancellationToken);
                    _logger.LogInformation("Replied to comment {CommentId} with: {Text}", command.Target.CommentId, command.ReplyText);
                }
                else if (command.Action == "hide")
                {
                    // Giả sử có hàm HideCommentAsync
                    _logger.LogInformation("Hid comment {CommentId}", command.Target.CommentId);
                }
                else
                {
                    _logger.LogInformation("No action taken for command {CommandId} (Action: {Action})", command.CommandId, command.Action);
                }

                // 3. Lưu lịch sử xử lý thành công (Idempotency)
                _dbContext.ProcessedCommands.Add(new ProcessedCommand
                {
                    CommandId = command.CommandId,
                    EventId = command.EventId,
                    Action = command.Action
                });
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
            }
        }
    }
}
