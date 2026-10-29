using System.Configuration;
using System.Text.RegularExpressions;

namespace focus_ai;

internal static class AppSettings
{
    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        string environmentKey = "FOCUSAI_" + Regex.Replace(key, "([a-z0-9])([A-Z])", "$1_$2").ToUpperInvariant();
        string? environmentValue = Environment.GetEnvironmentVariable(environmentKey);
        string value = !string.IsNullOrWhiteSpace(environmentValue)
            ? environmentValue.Trim()
            : ConfigurationManager.AppSettings[key]?.Trim() ?? string.Empty;

        return key.Equals("RealtimeDatabaseUrl", StringComparison.Ordinal)
            ? value.TrimEnd('/')
            : value;
    }
}
