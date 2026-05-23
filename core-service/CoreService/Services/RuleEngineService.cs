using CoreService.Models;

namespace CoreService.Services;

public class RuleEngineService
{
    public ReplyCommand Process(NormalizedFacebookEvent ev, AiAnalysisResult aiResult)
    {
        var command = new ReplyCommand
        {
            EventId = ev.EventId,
            Target = new ReplyCommand.TargetInfo
            {
                PageId = ev.PageId,
                CommentId = ev.CommentId
            },
            Intent = aiResult.Intent,
            Sentiment = aiResult.Sentiment
        };

        if (aiResult.Intent == "spam")
        {
            command.Action = "hide";
            command.ReplyText = "";
        }
        else if (aiResult.Sentiment == "negative")
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
            command.ReplyText = "";
        }

        return command;
    }
}
