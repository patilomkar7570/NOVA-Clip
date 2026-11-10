using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NOVA.Clip.Infrastructure.Security;
using Xunit;

namespace NOVA.Clip.Tests;

public class DataProtectionTests
{
    private readonly DpapiDataProtectionService _service = new(NullLogger<DpapiDataProtectionService>.Instance);

    [Fact]
    public void EncryptAndDecryptBytes_RoundtripMatchesOriginal()
    {
        byte[] original = Encoding.UTF8.GetBytes("Super secret test clipboard content! 12345");
        byte[] encrypted = _service.Encrypt(original);

        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotEqual(original);

        byte[] decrypted = _service.Decrypt(encrypted);
        decrypted.Should().Equal(original);
        Encoding.UTF8.GetString(decrypted).Should().Be("Super secret test clipboard content! 12345");
    }

    [Fact]
    public void EncryptAndDecryptString_RoundtripMatchesOriginal()
    {
        string text = "Confidential text with unicode symbols: 🚀 🔒 \n Multi-line support.";
        string encrypted = _service.EncryptString(text);

        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(text);

        string decrypted = _service.DecryptString(encrypted);
        decrypted.Should().Be(text);
    }

    [Fact]
    public void Encrypt_EmptyInput_ReturnsEmpty()
    {
        _service.Encrypt([]).Should().BeEmpty();
        _service.EncryptString("").Should().BeEmpty();
    }
}
