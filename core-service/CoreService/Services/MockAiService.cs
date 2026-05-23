using CoreService.Models;

namespace CoreService.Services;

public interface IAiService
{
    Task<AiAnalysisResult> AnalyzeMessageAsync(string message, CancellationToken cancellationToken = default);
}

public class MockAiService : IAiService
{
    private readonly ILogger<MockAiService> _logger;

    public MockAiService(ILogger<MockAiService> logger)
    {
        _logger = logger;
    }

    public Task<AiAnalysisResult> AnalyzeMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        // Giả lập độ trễ gọi API
        Thread.Sleep(500);

        var msgLower = message.ToLowerInvariant();
        var result = new AiAnalysisResult();

        if (msgLower.Contains("giá") || msgLower.Contains("bao nhiêu") || msgLower.Contains("inbox"))
        {
            result.Intent = "ask_price";
            result.Sentiment = "neutral";
        }
        else if (msgLower.Contains("lỗi") || msgLower.Contains("tệ") || msgLower.Contains("chán") || msgLower.Contains("buồn"))
        {
            result.Intent = "complaint";
            result.Sentiment = "negative";
        }
        else if (msgLower.Contains("tuyệt") || msgLower.Contains("đẹp") || msgLower.Contains("tốt") || msgLower.Contains("ok"))
        {
            result.Intent = "compliment";
            result.Sentiment = "positive";
        }
        else if (msgLower.Contains("http") || msgLower.Contains("mua ngay tại") || msgLower.Contains("link"))
        {
            result.Intent = "spam";
            result.Sentiment = "neutral";
        }
        else
        {
            result.Intent = "unknown";
            result.Sentiment = "neutral";
        }

        _logger.LogInformation("AI Analyzed: [{Message}] -> Intent: {Intent}, Sentiment: {Sentiment}", message, result.Intent, result.Sentiment);
        return Task.FromResult(result);
    }
}
