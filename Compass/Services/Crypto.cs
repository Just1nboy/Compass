using System.Security.Cryptography;
using System.Text;

namespace Compass.Services;

/// <summary>Per-user encryption (Windows DPAPI) for secrets like app passwords and tokens.</summary>
public static class Crypto
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Compass.v1.secret");

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        try
        {
            byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }
        catch { return ""; }
    }

    public static string Unprotect(string encBase64)
    {
        if (string.IsNullOrEmpty(encBase64)) return "";
        try
        {
            byte[] dec = ProtectedData.Unprotect(Convert.FromBase64String(encBase64), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }
        catch { return ""; }
    }
}
