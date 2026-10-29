using System.Security.Cryptography;
using System.Text;

namespace focus_ai;

internal static class SessionTokenProtector
{
    private const string Prefix = "dpapi:v1:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FocusAI.SessionToken.v1");

    public static string Protect(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        byte[] clearBytes = Encoding.UTF8.GetBytes(token);
        byte[] protectedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
            return string.Empty;

        if (!storedValue.StartsWith(Prefix, StringComparison.Ordinal))
            return storedValue;

        try
        {
            byte[] protectedBytes = Convert.FromBase64String(storedValue[Prefix.Length..]);
            byte[] clearBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    public static bool IsProtected(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith(Prefix, StringComparison.Ordinal);
}
