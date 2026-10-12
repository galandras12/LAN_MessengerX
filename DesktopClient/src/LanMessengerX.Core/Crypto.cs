using System.Security.Cryptography;

namespace LanMessengerX.Core;

/// <summary>
/// The key exchange of the original client, byte for byte:
///  1. The accepting side sends its RSA public key as PEM "RSA PUBLIC KEY" (PKCS#1).
///  2. The connecting side draws 32 random bytes, stretches them with EVP_BytesToKey
///     (AES-256-CBC, SHA-1, no salt, 5 rounds) into a 32 byte key and 16 byte IV, and sends
///     key||iv encrypted with RSA/OAEP (SHA-1) as HNDSHK.
///  3. Every following message is AES-256-CBC/PKCS7 with that fixed key and IV.
/// </summary>
public static class LmxCrypto
{
    public const int AesKeyLen = 32;
    public const int AesIvLen = 16;

    public sealed class RsaKeys : IDisposable
    {
        public RSA Rsa { get; }
        public byte[] PublicPem { get; }

        public RsaKeys(RSA rsa)
        {
            Rsa = rsa;
            PublicPem = System.Text.Encoding.ASCII.GetBytes(rsa.ExportRSAPublicKeyPem().Replace("\r\n", "\n") + "\n");
        }

        public void Dispose() => Rsa.Dispose();
    }

    public static RsaKeys GenerateRsa(int bits = 2048) => new(RSA.Create(bits));

    /// <summary>EVP_BytesToKey with SHA-1 and no salt; returns keyLen + ivLen bytes.</summary>
    public static byte[] EvpBytesToKey(byte[] data, int rounds, int keyLen, int ivLen)
    {
        var output = new List<byte>();
        byte[] prev = Array.Empty<byte>();
        while (output.Count < keyLen + ivLen)
        {
            var input = new byte[prev.Length + data.Length];
            Buffer.BlockCopy(prev, 0, input, 0, prev.Length);
            Buffer.BlockCopy(data, 0, input, prev.Length, data.Length);
            var d = SHA1.HashData(input);
            for (int r = 1; r < rounds; r++) d = SHA1.HashData(d);
            output.AddRange(d);
            prev = d;
        }
        return output.Take(keyLen + ivLen).ToArray();
    }

    public static byte[] NewSessionMaterial() => EvpBytesToKey(RandomNumberGenerator.GetBytes(32), 5, AesKeyLen, AesIvLen);

    public static byte[] RsaEncrypt(RSA publicKey, byte[] plain) => publicKey.Encrypt(plain, RSAEncryptionPadding.OaepSHA1);
    public static byte[] RsaDecrypt(RSA privateKey, byte[] cipher) => privateKey.Decrypt(cipher, RSAEncryptionPadding.OaepSHA1);

    /// <summary>Accepts both "RSA PUBLIC KEY" (PKCS#1) and "PUBLIC KEY" (X.509 SPKI) PEM.</summary>
    public static RSA PublicKeyFromPem(byte[] pem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(System.Text.Encoding.ASCII.GetString(pem));
        return rsa;
    }
}

/// <summary>AES-256-CBC with a fixed key and IV (the IV is reset for every message, as in the original).</summary>
public sealed class SessionCipher
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public SessionCipher(byte[] material)
    {
        if (material.Length < LmxCrypto.AesKeyLen + LmxCrypto.AesIvLen) throw new ArgumentException("session material too short");
        _key = material.AsSpan(0, LmxCrypto.AesKeyLen).ToArray();
        _iv = material.AsSpan(LmxCrypto.AesKeyLen, LmxCrypto.AesIvLen).ToArray();
    }

    public byte[] Encrypt(byte[] plain)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        return aes.EncryptCbc(plain, _iv, PaddingMode.PKCS7);
    }

    public byte[]? Decrypt(byte[] cipher)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            return aes.DecryptCbc(cipher, _iv, PaddingMode.PKCS7);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
