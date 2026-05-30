using CoreService.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace CoreService.Services;

public class RuleEngineService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<RuleEngineService> _logger;

    public RuleEngineService(IMemoryCache cache, ILogger<RuleEngineService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public ReplyCommand Process(NormalizedFacebookEvent ev, AiAnalysisResult aiResult)
    {
        var command = new ReplyCommand
        {
            CommandId = string.Empty,
            EventId = ev.EventId,
            Target = new ReplyCommand.TargetInfo
            {
                PageId = ev.PageId,
                CommentId = ev.CommentId
            },
            Intent = aiResult.Intent,
            Sentiment = aiResult.Sentiment,
            OriginalMessage = ev.Message ?? string.Empty
        };

        var actorId = ev.UserId ?? "unknown";
        var blacklistKey = $"blacklist_{actorId}";

        // 1. Check Blacklist
        if (_cache.TryGetValue(blacklistKey, out _))
        {
            _logger.LogWarning("Actor {ActorId} is in blacklist. Action: blacklist_block.", actorId);
            command.Action = "blacklist_block";
            return command;
        }

        // 2. Check Spam
        bool containsLink = Regex.IsMatch(ev.Message ?? "", @"(http|https)://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?");
        
        if (aiResult.Intent == "spam" || containsLink)
        {
            var spamKey = $"spam_count_{actorId}";
            var spamCount = _cache.GetOrCreate(spamKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                return 0;
            });

            spamCount++;
            _cache.Set(spamKey, spamCount, TimeSpan.FromHours(24));

            if (spamCount >= 3)
            {
                _logger.LogWarning("Actor {ActorId} reached 3 spams in 24h. Adding to blacklist.", actorId);
                _cache.Set(blacklistKey, true, TimeSpan.FromDays(30)); // Blacklist for 30 days
                command.Action = "blacklist_block";
            }
            else if (containsLink)
            {
                command.Action = "hide_and_review";
            }
            else
            {
                command.Action = "hide"; // Spam nhẹ
            }
            return command;
        }

        // 3. Normal Flow
        if (aiResult.Sentiment == "negative")
        {
            command.Action = "reply";
            command.ReplyText = "Xin lỗi bạn vì sự cố. Vui lòng inbox để shop hỗ trợ nhé!";
        }
        else if (aiResult.Intent == "ask_price")
        {
            command.Action = "reply";
            command.ReplyText = "Dạ sản phẩm đang có giá ưu đãi, shop đã gửi chi tiết vào inbox cho bạn ạ.";
        }
        else if (aiResult.Sentiment == "positive")
        {
            command.Action = "reply";
            command.ReplyText = "Cảm ơn bạn đã tin tưởng và ủng hộ shop!";
        }
        else
        {
            command.Action = "manual_review";
        }

        command.CommandId = $"{ev.EventId}_{command.Action}";
        return command;
    }
}
