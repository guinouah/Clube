using System.Security.Cryptography;
using System.Text;

namespace Clubes.Api.Security;

public static class TokenDigest
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
