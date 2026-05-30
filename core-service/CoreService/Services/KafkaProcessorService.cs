using System.Text.Json;
using Confluent.Kafka;
using CoreService.Models;

namespace CoreService.Services;

public class KafkaProcessorService : BackgroundService
{
    private readonly ILogger<KafkaProcessorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly string _consumeTopic;
    private readonly string _produceTopic;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public KafkaProcessorService(ILogger<KafkaProcessorService> logger, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        
        _consumeTopic = _configuration["Kafka:RawEventsTopic"] ?? "raw_events";
        _produceTopic = _configuration["Kafka:ReplyCommandsTopic"] ?? "reply_commands";
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        
        return Task.Run(() => ProcessLoop(bootstrapServers, stoppingToken), stoppingToken);
    }

    private void ProcessLoop(string bootstrapServers, CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "core-service-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        consumer.Subscribe(_consumeTopic);
        _logger.LogInformation("Core Service listening on topic {Topic} at {Servers}", _consumeTopic, bootstrapServers);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = consumer.Consume(stoppingToken);
                if (consumeResult?.Message?.Value == null) continue;

                try
                {
                    var ev = JsonSerializer.Deserialize<NormalizedFacebookEvent>(consumeResult.Message.Value, _jsonOptions);
                    if (ev == null) continue;

                    using var scope = _serviceProvider.CreateScope();
                    var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();
                    var ruleEngine = scope.ServiceProvider.GetRequiredService<RuleEngineService>();

                    ReplyCommand command;
                    if (ev.IsPendingReview)
                    {
                        _logger.LogWarning("Event {EventId} is pending review due to rate limiting. Skipping AI and rules.", ev.EventId);
                        command = new ReplyCommand
                        {
                            CommandId = $"{ev.EventId}_pending_review",
                            EventId = ev.EventId,
                            Action = "pending_review",
                            Target = new ReplyCommand.TargetInfo { PageId = ev.PageId, CommentId = ev.CommentId },
                            OriginalMessage = ev.Message ?? string.Empty
                        };
                    }
                    else
                    {
                        // 1. Phân tích AI
                        var aiResult = aiService.AnalyzeMessageAsync(ev.Message ?? "", stoppingToken).GetAwaiter().GetResult();

                        // 2. Rule Engine ra quyết định
                        command = ruleEngine.Process(ev, aiResult);
                    }

                    // 3. Publish vào reply_commands
                    var commandJson = JsonSerializer.Serialize(command);
                    producer.Produce(_produceTopic, new Message<string, string>
                    {
                        Key = command.Target.PageId,
                        Value = commandJson
                    });

                    _logger.LogInformation("Processed Event {EventId} -> Action: {Action}", ev.EventId, command.Action);
                    
                    consumer.Commit(consumeResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from topic {Topic}", _consumeTopic);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka processor stopped.");
        }
        finally
        {
            consumer.Close();
        }
    }
}
