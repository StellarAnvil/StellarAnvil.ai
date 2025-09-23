using System.Text.RegularExpressions;

namespace StellarAnvil.Application.Services;

/// <summary>
/// Service for parsing human confirmations from chat messages
/// </summary>
public static class ConfirmationParsingService
{
    private static readonly string[] PositivePatterns = new[]
    {
        @"\byes\b",
        @"\byeah\b",
        @"\byep\b",
        @"\byup\b",
        @"\bsure\b",
        @"\bokay\b",
        @"\bok\b",
        @"\bagree\b",
        @"\bconfirm\b",
        @"\bconfirmed\b",
        @"\bapprove\b",
        @"\bapproved\b",
        @"\baccept\b",
        @"\baccepted\b",
        @"\bgo ahead\b",
        @"\bproceed\b",
        @"\bgood to go\b",
        @"\blooks good\b",
        @"\bi am happy\b",
        @"\bhappy to move\b",
        @"\bready to move\b",
        @"\bmove forward\b",
        @"\blet's move\b",
        @"\blet's go\b",
        @"\bcontinue\b",
        @"\bmove on\b",
        @"\bnext step\b",
        @"\bnext phase\b"
    };

    private static readonly string[] NegativePatterns = new[]
    {
        @"\bno\b",
        @"\bnope\b",
        @"\bnah\b",
        @"\bdisagree\b",
        @"\bdeny\b",
        @"\bdenied\b",
        @"\breject\b",
        @"\brejected\b",
        @"\bcancel\b",
        @"\bcancelled\b",
        @"\bstop\b",
        @"\bwait\b",
        @"\bhold on\b",
        @"\bnot ready\b",
        @"\bnot happy\b",
        @"\bneed changes\b",
        @"\bneed more work\b",
        @"\bnot approved\b",
        @"\bnot good\b",
        @"\bnot satisfied\b"
    };

    /// <summary>
    /// Parse a message to determine if it contains positive confirmation
    /// </summary>
    public static bool IsPositiveConfirmation(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var lowerMessage = message.ToLower().Trim();

        // Check for positive patterns
        foreach (var pattern in PositivePatterns)
        {
            if (Regex.IsMatch(lowerMessage, pattern, RegexOptions.IgnoreCase))
            {
                // Make sure it's not negated
                if (!IsNegated(lowerMessage, pattern))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Parse a message to determine if it contains negative confirmation
    /// </summary>
    public static bool IsNegativeConfirmation(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var lowerMessage = message.ToLower().Trim();

        // Check for negative patterns
        foreach (var pattern in NegativePatterns)
        {
            if (Regex.IsMatch(lowerMessage, pattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a positive word is negated (e.g., "not okay", "don't agree")
    /// </summary>
    private static bool IsNegated(string message, string pattern)
    {
        var negationWords = new[] { "not", "don't", "doesn't", "won't", "can't", "never", "no" };

        // Find the position of the pattern match
        var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        var matchIndex = match.Index;

        // Look for negation words in the 20 characters before the match
        var startIndex = Math.Max(0, matchIndex - 20);
        var beforeMatch = message.Substring(startIndex, matchIndex - startIndex);

        return negationWords.Any(negation =>
            beforeMatch.Contains(negation, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get the confirmation type (Positive, Negative, or Unclear)
    /// </summary>
    public static ConfirmationType GetConfirmationType(string message)
    {
        if (IsPositiveConfirmation(message))
            return ConfirmationType.Positive;

        if (IsNegativeConfirmation(message))
            return ConfirmationType.Negative;

        return ConfirmationType.Unclear;
    }

    /// <summary>
    /// Extract any specific feedback or concerns from a negative confirmation
    /// </summary>
    public static string? ExtractFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        // Look for common feedback patterns
        var feedbackPatterns = new[]
        {
            @"because (.+)",
            @"but (.+)",
            @"however (.+)",
            @"concern[s]? (?:is|are) (.+)",
            @"issue[s]? (?:is|are) (.+)",
            @"problem[s]? (?:is|are) (.+)",
            @"need[s]? (?:to|more) (.+)",
            @"should (.+)",
            @"must (.+)",
            @"requires? (.+)"
        };

        foreach (var pattern in feedbackPatterns)
        {
            var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        // If negative but no specific feedback found, return the full message
        if (IsNegativeConfirmation(message))
        {
            return message.Trim();
        }

        return null;
    }
}

public enum ConfirmationType
{
    Positive,
    Negative,
    Unclear
}