using System.IO;
using System.Text.RegularExpressions;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.Infrastructure.Security;

public partial class PrivacyFilter : IPrivacyFilter
{
    // Regex for credit cards: 13-19 digits with optional spaces or hyphens
    [GeneratedRegex(@"\b(?:\d[ -]*?){13,19}\b")]
    private static partial Regex CreditCardCandidateRegex();

    // Sensitive keys/tokens
    [GeneratedRegex(@"(?i)-----BEGIN\s+(?:RSA|OPENSSH|EC|PGP|DSA)?\s*PRIVATE\s+KEY-----")]
    private static partial Regex PrivateKeyRegex();

    [GeneratedRegex(@"(?:ghp_[A-Za-z0-9_]{36}|gho_[A-Za-z0-9_]{36}|glpat-[0-9a-zA-Z_\-]{20}|xox[baprs]-[0-9a-zA-Z]{10,48}|AKIA[0-9A-Z]{16})")]
    private static partial Regex ApiKeyRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}")]
    private static partial Regex JwtTokenRegex();

    [GeneratedRegex(@"(?i)(?:password|passwd|secret|pwd)\s*[:=]\s*([^\s]{4,64})")]
    private static partial Regex PasswordAssignmentRegex();

    // OTP / Verification codes
    [GeneratedRegex(@"(?i)(?:otp|code|pin|verification\s*code|auth\s*code)\D{1,12}\b(\d{4,8})\b")]
    private static partial Regex OtpRegex();

    public bool IsSensitiveText(string text, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        // 1. Private keys
        if (PrivateKeyRegex().IsMatch(text))
        {
            reason = "Private cryptographic key pattern detected";
            return true;
        }

        // 2. API Keys and Tokens
        if (ApiKeyRegex().IsMatch(text))
        {
            reason = "API access token / credential pattern detected";
            return true;
        }

        // 3. JWT Tokens
        if (JwtTokenRegex().IsMatch(text))
        {
            reason = "JWT security token pattern detected";
            return true;
        }

        // 4. Password assignments
        if (PasswordAssignmentRegex().IsMatch(text))
        {
            reason = "Password credential pattern detected";
            return true;
        }

        // 5. OTP / Verification codes
        if (OtpRegex().IsMatch(text))
        {
            reason = "One-time passcode / verification code pattern detected";
            return true;
        }

        // 6. Credit card numbers with Luhn verification
        var candidates = CreditCardCandidateRegex().Matches(text);
        foreach (Match match in candidates)
        {
            string digits = Regex.Replace(match.Value, @"\D", "");
            if (digits.Length >= 13 && digits.Length <= 19 && IsValidLuhn(digits))
            {
                reason = "Payment card number detected";
                return true;
            }
        }

        return false;
    }

    public bool IsProcessExcluded(string? processName, IEnumerable<string> excludedList)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        string cleanName = Path.GetFileNameWithoutExtension(processName).Trim();

        foreach (string excluded in excludedList)
        {
            if (string.IsNullOrWhiteSpace(excluded)) continue;
            string cleanExcluded = Path.GetFileNameWithoutExtension(excluded).Trim();

            if (string.Equals(cleanName, cleanExcluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsValidLuhn(string digits)
    {
        if (string.IsNullOrEmpty(digits)) return false;

        int sum = 0;
        bool alternate = false;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(digits[i])) return false;
            int n = digits[i] - '0';

            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return (sum % 10 == 0);
    }
}
