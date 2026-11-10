using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.Infrastructure.Security;

public class DpapiDataProtectionService : IDataProtectionService
{
    private static readonly byte[] Entropy = "NOVA_CLIP_DPAPI_ENTROPY_V1"u8.ToArray();
    private readonly ILogger<DpapiDataProtectionService> _logger;

    public DpapiDataProtectionService(ILogger<DpapiDataProtectionService> logger)
    {
        _logger = logger;
    }

    public byte[] Encrypt(byte[] plainBytes)
    {
        ArgumentNullException.ThrowIfNull(plainBytes);
        if (plainBytes.Length == 0) return [];

        try
        {
            return ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to protect data using DPAPI CurrentUser scope");
            throw;
        }
    }

    public byte[] Decrypt(byte[] cipherBytes)
    {
        ArgumentNullException.ThrowIfNull(cipherBytes);
        if (cipherBytes.Length == 0) return [];

        try
        {
            return ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect data using DPAPI CurrentUser scope");
            throw;
        }
    }

    public string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        byte[] bytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encrypted = Encrypt(bytes);
        return Convert.ToBase64String(encrypted);
    }

    public string DecryptString(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;
        byte[] cipherBytes = Convert.FromBase64String(cipherText);
        byte[] decrypted = Decrypt(cipherBytes);
        return Encoding.UTF8.GetString(decrypted);
    }
}
