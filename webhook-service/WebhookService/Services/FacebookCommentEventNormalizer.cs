using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using WebhookService.Models;

namespace WebhookService.Services;

public class FacebookCommentEventNormalizer
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<FacebookCommentEventNormalizer> _logger;

    public FacebookCommentEventNormalizer(IMemoryCache cache, ILogger<FacebookCommentEventNormalizer> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public IReadOnlyList<NormalizedFacebookEvent> NormalizeCommentEvents(
        JToken payload,
        string? expectedPageId)
    {
        var result = new List<NormalizedFacebookEvent>();
        if (payload is not JObject root)
        {
            return result;
        }

        var objectType = root.Value<string>("object");
        if (!string.Equals(objectType, "page", StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        var receivedAt = DateTimeOffset.UtcNow;
        var entries = root["entry"] as JArray;
        if (entries is null)
        {
            return result;
        }

        foreach (var entry in entries.OfType<JObject>())
        {
            var pageId = entry.Value<string>("id") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(expectedPageId)
                && !string.Equals(pageId, expectedPageId, StringComparison.Ordinal))
            {
                continue;
            }

            var changes = entry["changes"] as JArray;
            if (changes is null)
            {
                continue;
            }

            foreach (var change in changes.OfType<JObject>())
            {
                if (!string.Equals(change.Value<string>("field"), "feed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = change["value"] as JObject;
                if (value is null)
                {
                    continue;
                }

                if (!string.Equals(value.Value<string>("item"), "comment", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(value.Value<string>("verb"), "add", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var commentId = value.Value<string>("comment_id");
                var postId = value.Value<string>("post_id");
                var actorId = value.Value<string>("sender_id")
                    ?? value["from"]?["id"]?.ToString();
                var createdAt = ParseCreatedAt(value["created_time"]) ?? receivedAt;

                var eventId = string.IsNullOrWhiteSpace(commentId)
                        ? $"facebook:comment:{Guid.NewGuid():N}"
                        : $"facebook:comment:{commentId}";

                bool isPendingReview = false;
                if (!string.IsNullOrWhiteSpace(actorId))
                {
                    var cacheKey = $"rate_limit_{actorId}";
                    var currentCount = _cache.GetOrCreate(cacheKey, entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                        return 0;
                    });

                    currentCount++;
                    _cache.Set(cacheKey, currentCount, TimeSpan.FromMinutes(1));

                    if (currentCount > 20)
                    {
                        isPendingReview = true;
                        _logger.LogWarning("Rate limit exceeded for actor {ActorId} ({Count} comments in 1 min). Event {EventId} marked as pending_review.", actorId, currentCount, eventId);
                    }
                }

                result.Add(new NormalizedFacebookEvent
                {
                    EventId = eventId,
                    PageId = pageId,
                    PostId = postId,
                    CommentId = commentId,
                    ActorId = actorId,
                    Message = value.Value<string>("message"),
                    CreatedAt = createdAt,
                    RawEvent = (JObject)change.DeepClone(),
                    ReceivedAt = receivedAt,
                    IsPendingReview = isPendingReview
                });
            }
        }

        return result;
    }

    private static DateTimeOffset? ParseCreatedAt(JToken? token)
    {
        if (token is null)
        {
            return null;
        }

        if (token.Type == JTokenType.Integer && token.Value<long?>() is long intValue)
        {
            return intValue > 9999999999
                ? DateTimeOffset.FromUnixTimeMilliseconds(intValue)
                : DateTimeOffset.FromUnixTimeSeconds(intValue);
        }

        var text = token.ToString();
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixValue))
        {
            return unixValue > 9999999999
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixValue)
                : DateTimeOffset.FromUnixTimeSeconds(unixValue);
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }
}
