using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Net.Http;
namespace focus_ai;

internal static class GooglePkceAuthenticator
{
    private static readonly HttpClient Http = new();

    public static async Task<string> GetIdTokenAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Configurează FOCUSAI_GOOGLE_CLIENT_ID cu un OAuth client de tip Desktop app.");

        string verifier = Base64Url(RandomNumberGenerator.GetBytes(64));
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string state = Base64Url(RandomNumberGenerator.GetBytes(32));

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string redirectUri = $"http://127.0.0.1:{port}/";

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
            ["prompt"] = "select_account"
        };

        string authorizationUrl = "https://accounts.google.com/o/oauth2/v2/auth?" +
                                  string.Join("&", query.Select(pair =>
                                      Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));

        Process.Start(new ProcessStartInfo(authorizationUrl) { UseShellExecute = true });

        using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
        listener.Stop();

        string requestLine;
        using (NetworkStream stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
        {
            requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            string headerLine;
            do
            {
                headerLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            } while (headerLine.Length > 0);

            Uri callback = ParseCallback(requestLine);
            Dictionary<string, string> parameters = ParseQuery(callback.Query);
            bool stateValid = parameters.TryGetValue("state", out string? returnedState) &&
                              FixedTimeEquals(state, returnedState);
            bool hasCode = parameters.TryGetValue("code", out string? code) && !string.IsNullOrWhiteSpace(code);
            string page = stateValid && hasCode
                ? "<!doctype html><html><body><h2>Focus AI</h2><p>Autentificarea s-a încheiat. Poți reveni la aplicație.</p></body></html>"
                : "<!doctype html><html><body><h2>Focus AI</h2><p>Autentificarea a eșuat. Revino în aplicație și încearcă din nou.</p></body></html>";
            byte[] body = Encoding.UTF8.GetBytes(page);
            byte[] header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            if (!stateValid)
                throw new InvalidOperationException("Verificarea state pentru autentificarea Google a eșuat.");
            if (!hasCode)
            {
                string error = parameters.GetValueOrDefault("error", "unknown_error");
                throw new InvalidOperationException("Autentificarea Google a fost anulată sau a eșuat: " + error);
            }

            return await ExchangeCodeForIdTokenAsync(clientId, code!, verifier, redirectUri, cancellationToken);
        }
    }

    private static async Task<string> ExchangeCodeForIdTokenAsync(
        string clientId,
        string code,
        string verifier,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        using HttpResponseMessage response = await Http.PostAsync(
            "https://oauth2.googleapis.com/token", content, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Schimbul codului OAuth Google a eșuat: " + (int)response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("id_token", out JsonElement idTokenElement))
            throw new InvalidDataException("Răspunsul OAuth Google nu conține id_token.");

        string? idToken = idTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(idToken))
            throw new InvalidDataException("Google a returnat un id_token gol.");

        return idToken;
    }

    private static Uri ParseCallback(string requestLine)
    {
        string[] parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !parts[0].Equals("GET", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Cererea callback OAuth nu este validă.");

        return new Uri("http://127.0.0.1" + parts[1]);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = item.Split('=', 2);
            string key = WebUtility.UrlDecode(pair[0]);
            string value = pair.Length > 1 ? WebUtility.UrlDecode(pair[1]) : string.Empty;
            values[key] = value;
        }

        return values;
    }

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (actual is null)
            return false;
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
