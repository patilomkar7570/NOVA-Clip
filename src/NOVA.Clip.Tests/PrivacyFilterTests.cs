using FluentAssertions;
using NOVA.Clip.Infrastructure.Security;
using Xunit;

namespace NOVA.Clip.Tests;

public class PrivacyFilterTests
{
    private readonly PrivacyFilter _filter = new();

    [Theory]
    [InlineData("49927398716", true)] // Valid Luhn
    [InlineData("49927398717", false)] // Invalid Luhn
    [InlineData("1234567812345670", true)] // Valid Luhn
    [InlineData("1234567812345678", false)] // Invalid Luhn
    public void IsValidLuhn_ValidatesCorrectly(string number, bool expected)
    {
        PrivacyFilter.IsValidLuhn(number).Should().Be(expected);
    }

    [Fact]
    public void IsSensitiveText_DetectsCreditCardNumber()
    {
        string text = "Please charge card: 1234-5678-1234-5670 for the order.";
        bool isSensitive = _filter.IsSensitiveText(text, out string? reason);

        isSensitive.Should().BeTrue();
        reason.Should().Contain("Payment card");
    }

    [Fact]
    public void IsSensitiveText_DetectsPrivateKey()
    {
        string text = "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA0...==\n-----END RSA PRIVATE KEY-----";
        bool isSensitive = _filter.IsSensitiveText(text, out string? reason);

        isSensitive.Should().BeTrue();
        reason.Should().Contain("Private cryptographic key");
    }

    [Fact]
    public void IsSensitiveText_DetectsGithubToken()
    {
        string text = "export GITHUB_TOKEN=ghp_123456789012345678901234567890123456";
        bool isSensitive = _filter.IsSensitiveText(text, out string? reason);

        isSensitive.Should().BeTrue();
        reason.Should().Contain("API access token");
    }

    [Fact]
    public void IsSensitiveText_DetectsOtpVerificationCode()
    {
        string text = "Your one-time verification code is 849204 to complete sign in.";
        bool isSensitive = _filter.IsSensitiveText(text, out string? reason);

        isSensitive.Should().BeTrue();
        reason.Should().Contain("passcode");
    }

    [Fact]
    public void IsSensitiveText_DetectsPasswordAssignment()
    {
        string text = "server credentials: password = MySuperSecretPassw0rd!";
        bool isSensitive = _filter.IsSensitiveText(text, out string? reason);

        isSensitive.Should().BeTrue();
        reason.Should().Contain("Password");
    }

    [Fact]
    public void IsSensitiveText_AllowsNormalText()
    {
        string text = "Hello, this is a normal message about meeting tomorrow at 3pm.";
        bool isSensitive = _filter.IsSensitiveText(text, out string? reason);

        isSensitive.Should().BeFalse();
        reason.Should().BeNull();
    }

    [Theory]
    [InlineData("1Password.exe", true)]
    [InlineData("1Password", true)]
    [InlineData("C:\\Program Files\\1Password\\1Password.exe", true)]
    [InlineData("KeePass.exe", true)]
    [InlineData("keepassxc.exe", true)]
    [InlineData("notepad.exe", false)]
    [InlineData("code.exe", false)]
    public void IsProcessExcluded_ChecksAgainstExcludedList(string processName, bool expected)
    {
        var list = new[] { "1Password.exe", "KeePass.exe", "Bitwarden.exe", "KeePassXC.exe" };
        bool result = _filter.IsProcessExcluded(processName, list);

        result.Should().Be(expected);
    }
}
