using System.Text.RegularExpressions;

namespace Server_Study.Modules.Common.MessageFilters;

/// <summary>
/// 채팅 메시지 필터링을 담당하는 클래스
/// </summary>
public static class MessageFilter
{
    private static readonly string[] _profanityWords = {
        "바보", "멍청", "욕설", "나쁜말"
    };

    private static readonly Regex _spamPattern = new Regex(@"(.)\1{4,}", RegexOptions.Compiled);
    private static readonly Regex _urlPattern = new Regex(@"https?://[^\s]+", RegexOptions.Compiled);

    /// <summary>
    /// 메시지의 전반적인 유효성을 검사합니다
    /// </summary>
    public static ValidationResult ValidateMessage(string message, string userId)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new ValidationResult(false, "빈 메시지는 전송할 수 없습니다.");

        if (message.Length > 200)
            return new ValidationResult(false, "메시지가 너무 깁니다. (최대 200자)");

        if (message.Length < 1)
            return new ValidationResult(false, "메시지가 너무 짧습니다.");

        // 스팸 패턴 검사
        if (IsSpamMessage(message))
            return new ValidationResult(false, "스팸성 메시지는 전송할 수 없습니다.");

        return new ValidationResult(true, "");
    }

    /// <summary>
    /// 금지어를 필터링합니다
    /// </summary>
    public static string FilterProfanity(string message)
    {
        foreach (var word in _profanityWords)
        {
            message = message.Replace(word, new string('*', word.Length), StringComparison.OrdinalIgnoreCase);
        }
        return message;
    }

    /// <summary>
    /// URL을 필터링합니다
    /// </summary>
    public static string FilterUrls(string message)
    {
        return _urlPattern.Replace(message, "[링크 차단됨]");
    }

    /// <summary>
    /// 스팸 메시지인지 검사합니다
    /// </summary>
    private static bool IsSpamMessage(string message)
    {
        // 동일한 문자가 5번 이상 반복되는 경우
        return _spamPattern.IsMatch(message);
    }

    /// <summary>
    /// 모든 필터를 적용합니다
    /// </summary>
    public static string ApplyAllFilters(string message)
    {
        message = FilterProfanity(message);
        message = FilterUrls(message);
        return message.Trim();
    }

    /// <summary>
    /// 사용자별 채팅 제한을 검사합니다 (예: 도배 방지)
    /// </summary>
    public static bool CheckRateLimit(string userId, DateTime lastMessageTime)
    {
        var timeDiff = DateTime.UtcNow - lastMessageTime;
        return timeDiff.TotalSeconds >= 1.0; // 1초에 1번만 허용
    }
}

/// <summary>
/// 메시지 검증 결과
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public string ErrorMessage { get; }

    public ValidationResult(bool isValid, string errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
}