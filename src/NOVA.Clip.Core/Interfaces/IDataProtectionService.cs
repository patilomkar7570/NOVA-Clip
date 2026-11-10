namespace NOVA.Clip.Core.Interfaces;

public interface IDataProtectionService
{
    byte[] Encrypt(byte[] plainBytes);
    byte[] Decrypt(byte[] cipherBytes);
    string EncryptString(string plainText);
    string DecryptString(string cipherText);
}
