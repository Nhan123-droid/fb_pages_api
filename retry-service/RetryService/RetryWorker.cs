using System.Text.Json;
using Confluent.Kafka;
using RetryService.Models;

namespace RetryService;

public class RetryWorker : BackgroundService
{
    private readonly ILogger<RetryWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly int _maxRetries;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public RetryWorker(ILogger<RetryWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _maxRetries = _configuration.GetValue<int>("Retry:MaxRetries", 3);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var consumeTopic = _configuration["Kafka:SendFailedTopic"] ?? "send_failed";
        var retryTopic = _configuration["Kafka:SendRetryTopic"] ?? "send_retry";
        var dlqTopic = _configuration["Kafka:DeadLetterTopic"] ?? "dead_letter";

        return Task.Run(() => ProcessLoop(bootstrapServers, consumeTopic, retryTopic, dlqTopic, stoppingToken), stoppingToken);
    }

    private async Task ProcessLoop(string bootstrapServers, string consumeTopic, string retryTopic, string dlqTopic, CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "retry-service-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        consumer.Subscribe(consumeTopic);
        _logger.LogInformation("Retry Service listening on topic {Topic} at {Servers}", consumeTopic, bootstrapServers);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = consumer.Consume(stoppingToken);
                if (consumeResult?.Message?.Value == null) continue;

                try
                {
                    var msg = JsonSerializer.Deserialize<SendFailedMessage>(consumeResult.Message.Value, _jsonOptions);
                    if (msg == null) continue;

                    // 1. Exponential Backoff Delay
                    var delayMs = (int)(1000 * Math.Pow(2, msg.RetryCount));
                    _logger.LogInformation("Waiting {DelayMs}ms before retrying command {CommandId}", delayMs, msg.CommandId);
                    await Task.Delay(delayMs, stoppingToken);

                    // 2. Logic điều phối
                    msg.RetryCount++; // Tăng biến đếm

                    if (msg.RetryCount < _maxRetries)
                    {
                        // Gửi vào topic send_retry để Backend API tiêu thụ lại
                        msg.Payload.RetryCount = msg.RetryCount;
                        var retryPayloadJson = JsonSerializer.Serialize(msg.Payload, _jsonOptions);
                        
                        await producer.ProduceAsync(retryTopic, new Message<string, string>
                        {
                            Key = msg.CommandId,
                            Value = retryPayloadJson
                        }, stoppingToken);
                        
                        _logger.LogInformation("Sent command {CommandId} to {Topic} (Retry {RetryCount}/{MaxRetries})", msg.CommandId, retryTopic, msg.RetryCount, _maxRetries);
                    }
                    else
                    {
                        // Gửi vào DLQ
                        var dlqMsg = new DeadLetterMessage
                        {
                            CommandId = msg.CommandId,
                            EventId = msg.EventId,
                            RetryCount = msg.RetryCount,
                            FinalError = $"Max retries ({_maxRetries}) exceeded. Last error: {msg.LastError}",
                            Payload = msg.Payload
                        };
                        var dlqJson = JsonSerializer.Serialize(dlqMsg, _jsonOptions);
                        
                        await producer.ProduceAsync(dlqTopic, new Message<string, string>
                        {
                            Key = msg.CommandId,
                            Value = dlqJson
                        }, stoppingToken);
                        
                        _logger.LogError("Command {CommandId} failed permanently. Sent to {Topic}", msg.CommandId, dlqTopic);
                    }

                    consumer.Commit(consumeResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing failed message.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Retry Service stopping.");
        }
        finally
        {
            consumer.Close();
        }
    }
}
