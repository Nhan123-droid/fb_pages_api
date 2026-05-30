using System.Text;
using System.Text.Json;
using CoreService.Models;

namespace CoreService.Services;

public class GeminiAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiAiService> _logger;
    private readonly string _apiKey;
    
    private const string GeminiApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=";

    public GeminiAiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"]?.Trim() ?? throw new ArgumentNullException("Gemini:ApiKey is missing in configuration.");
    }

    public async Task<AiAnalysisResult> AnalyzeMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new AiAnalysisResult { Intent = "unknown", Sentiment = "neutral" };
        }

        var prompt = $@"Bạn là một trợ lý AI chuyên phân tích dữ liệu mạng xã hội cho một cửa hàng.
Nhiệm vụ của bạn là đọc bình luận của khách hàng và phân tích mục đích (intent) cũng như cảm xúc (sentiment).

Yêu cầu BẮT BUỘC: 
- Chỉ trả về duy nhất một object JSON, tuyệt đối không giải thích thêm, không bọc trong markdown ```json.
- JSON phải tuân thủ nghiêm ngặt định dạng sau:
{{
  ""intent"": ""<chỉ chọn 1 trong các giá trị: ask_price, complaint, compliment, spam, unknown>"",
  ""sentiment"": ""<chỉ chọn 1 trong các giá trị: positive, neutral, negative>""
}}

Hướng dẫn phân loại:
- ask_price: hỏi giá, phí ship, thời gian giao hàng...
- complaint: phàn nàn, báo lỗi, thất vọng, khiếu nại...
- compliment: khen ngợi, hài lòng, đồng tình...
- spam: chứa link (http), quảng cáo, chửi bậy, lặp từ vô nghĩa...
- unknown: các trường hợp còn lại hoặc không rõ nghĩa.

Bình luận của khách hàng: ""{message.Replace("\"", "\\\"")}""";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{GeminiApiUrl}{_apiKey}", jsonContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            
            using var jsonDocument = JsonDocument.Parse(responseString);
            
            // Extract the text part from Gemini response
            // Response format: { "candidates": [ { "content": { "parts": [ { "text": "{ \"intent\": \"...\", \"sentiment\": \"...\" }" } ] } } ] }
            var candidateText = jsonDocument.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (!string.IsNullOrEmpty(candidateText))
            {
                var result = JsonSerializer.Deserialize<AiAnalysisResult>(candidateText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result != null)
                {
                    _logger.LogInformation("Gemini AI Analyzed: [{Message}] -> Intent: {Intent}, Sentiment: {Sentiment}", message, result.Intent, result.Sentiment);
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze message using Gemini AI API.");
        }

        // Fallback
        return new AiAnalysisResult { Intent = "unknown", Sentiment = "neutral" };
    }
}
