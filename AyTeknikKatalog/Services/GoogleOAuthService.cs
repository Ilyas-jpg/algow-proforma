using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AyTeknikKatalog.Models;

namespace AyTeknikKatalog.Services;

public class GoogleOAuthException : Exception
{
    public GoogleOAuthException(string message) : base(message) { }
}

public class GoogleUserInfo
{
    public string Sub { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public bool EmailVerified { get; set; }
}

public class GoogleTokenResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int ExpiresIn { get; set; }
    public string Scope { get; set; } = "";
    public string IdToken { get; set; } = "";
}

public class GoogleOAuthService
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";
    private const string IdentityScopes = "openid email profile";
    private const string GmailSendScopes = "openid email profile https://www.googleapis.com/auth/gmail.send";
    // Tek paylaşımlı HttpClient — soket tükenmesini (her new'de yeni handler) önler. HttpClient thread-safe.
    private static readonly HttpClient _http = new();

    public async Task<GoogleUserInfo> SignInAsync(GoogleOAuthSettings settings, CancellationToken ct = default)
    {
        EnsureConfigured(settings, settings.EffectiveAuthRedirectUri);
        var token = await AuthorizeAsync(settings, settings.EffectiveAuthRedirectUri, IdentityScopes, includeOfflineAccess: false, ct);
        var user = await GetUserInfoAsync(token.AccessToken, ct);
        ValidateAllowedDomain(settings, user.Email);
        return user;
    }

    public async Task<GoogleGmailCredential> ConnectGmailAsync(GoogleOAuthSettings settings, CancellationToken ct = default)
    {
        EnsureConfigured(settings, settings.EffectiveGmailRedirectUri);
        var token = await AuthorizeAsync(settings, settings.EffectiveGmailRedirectUri, GmailSendScopes, includeOfflineAccess: true, ct);
        var user = await GetUserInfoAsync(token.AccessToken, ct);
        ValidateAllowedDomain(settings, user.Email);

        if (string.IsNullOrWhiteSpace(token.RefreshToken))
            throw new GoogleOAuthException("Google refresh token vermedi. Google hesabında eski izni kaldırıp Gmail bağlantısını tekrar deneyin.");

        return new GoogleGmailCredential
        {
            GoogleSub = user.Sub,
            GoogleEmail = user.Email,
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            TokenExpiresAt = DateTime.Now.AddSeconds(Math.Max(60, token.ExpiresIn)),
            GrantedScopes = token.Scope,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };
    }

    public async Task<GoogleGmailCredential> RefreshAsync(GoogleOAuthSettings settings, GoogleGmailCredential credential, CancellationToken ct = default)
    {
        EnsureConfigured(settings, settings.EffectiveGmailRedirectUri);
        if (string.IsNullOrWhiteSpace(credential.RefreshToken))
            throw new GoogleOAuthException("Gmail refresh token yok. Gmail hesabını yeniden bağlayın.");

        var form = new Dictionary<string, string>
        {
            ["client_id"] = settings.EffectiveClientId,
            ["client_secret"] = settings.EffectiveClientSecret,
            ["refresh_token"] = credential.RefreshToken,
            ["grant_type"] = "refresh_token",
        };

        var token = await PostTokenAsync(form, ct);
        credential.AccessToken = token.AccessToken;
        credential.TokenExpiresAt = DateTime.Now.AddSeconds(Math.Max(60, token.ExpiresIn));
        if (!string.IsNullOrWhiteSpace(token.Scope)) credential.GrantedScopes = token.Scope;
        credential.UpdatedAt = DateTime.Now;
        return credential;
    }

    public async Task RevokeAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            await _http.PostAsync(RevokeEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token,
            }), ct);
        }
        catch
        {
            // Yerel bağlantıyı silmek revoke başarısız olsa da devam edebilmeli.
        }
    }

    private async Task<GoogleTokenResponse> AuthorizeAsync(
        GoogleOAuthSettings settings, string redirectUri, string scopes, bool includeOfflineAccess, CancellationToken ct)
    {
        var state = CreateState();
        var authUrl = BuildAuthUrl(settings.EffectiveClientId, redirectUri, scopes, state, includeOfflineAccess);
        using var listener = StartListener(redirectUri);

        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var callback = await WaitForCallbackAsync(listener, state, timeout.Token);

        var form = new Dictionary<string, string>
        {
            ["client_id"] = settings.EffectiveClientId,
            ["client_secret"] = settings.EffectiveClientSecret,
            ["code"] = callback,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
        };
        return await PostTokenAsync(form, ct);
    }

    private static string BuildAuthUrl(string clientId, string redirectUri, string scopes, string state, bool includeOfflineAccess)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scopes,
            ["state"] = state,
            ["include_granted_scopes"] = "true",
            ["prompt"] = includeOfflineAccess ? "consent" : "select_account",
        };
        if (includeOfflineAccess) query["access_type"] = "offline";

        return AuthEndpoint + "?" + string.Join("&", query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
    }

    private static HttpListener StartListener(string redirectUri)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();
        return listener;
    }

    private static async Task<string> WaitForCallbackAsync(HttpListener listener, string expectedState, CancellationToken ct)
    {
        var contextTask = listener.GetContextAsync();
        using (ct.Register(() => { try { listener.Stop(); } catch { } }))
        {
            HttpListenerContext context;
            try { context = await contextTask; }
            catch (Exception ex) { throw new GoogleOAuthException("OAuth callback alınamadı: " + ex.Message); }

            var req = context.Request;
            var error = req.QueryString["error"];
            var code = req.QueryString["code"];
            var state = req.QueryString["state"];

            var ok = string.IsNullOrWhiteSpace(error) && !string.IsNullOrWhiteSpace(code) && state == expectedState;
            var html = ok
                ? "<html><body><h2>Algow Proforma bağlantısı tamamlandı.</h2><p>Bu sekmeyi kapatabilirsiniz.</p></body></html>"
                : "<html><body><h2>Google bağlantısı tamamlanamadı.</h2><p>Uygulamaya dönüp tekrar deneyin.</p></body></html>";
            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, ct);
            context.Response.Close();

            if (!string.IsNullOrWhiteSpace(error)) throw new GoogleOAuthException("Google OAuth hatası: " + error);
            if (state != expectedState) throw new GoogleOAuthException("OAuth state doğrulaması başarısız.");
            if (string.IsNullOrWhiteSpace(code)) throw new GoogleOAuthException("Google OAuth kodu alınamadı.");
            return code;
        }
    }

    private async Task<GoogleTokenResponse> PostTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var res = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new GoogleOAuthException("Google token hatası: " + ExtractError(json));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new GoogleTokenResponse
        {
            AccessToken = ReadString(root, "access_token"),
            RefreshToken = ReadString(root, "refresh_token"),
            ExpiresIn = ReadInt(root, "expires_in"),
            Scope = ReadString(root, "scope"),
            IdToken = ReadString(root, "id_token"),
        };
    }

    private async Task<GoogleUserInfo> GetUserInfoAsync(string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new GoogleOAuthException("Google kullanıcı bilgisi alınamadı: " + ExtractError(json));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new GoogleUserInfo
        {
            Sub = ReadString(root, "sub"),
            Email = ReadString(root, "email"),
            Name = ReadString(root, "name"),
            EmailVerified = ReadBool(root, "email_verified"),
        };
    }

    private static void EnsureConfigured(GoogleOAuthSettings settings, string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(settings.EffectiveClientId))
            throw new GoogleOAuthException("GOOGLE_CLIENT_ID eksik.");
        if (string.IsNullOrWhiteSpace(settings.EffectiveClientSecret))
            throw new GoogleOAuthException("GOOGLE_CLIENT_SECRET eksik.");
        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new GoogleOAuthException("Google redirect URI eksik.");
    }

    private static void ValidateAllowedDomain(GoogleOAuthSettings settings, string email)
    {
        var allowed = settings.EffectiveAllowedDomains;
        if (string.IsNullOrWhiteSpace(allowed) || string.IsNullOrWhiteSpace(email)) return;
        var domain = email.Split('@').LastOrDefault() ?? "";
        var ok = allowed.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(d => d.Trim().Equals(domain, StringComparison.OrdinalIgnoreCase));
        if (!ok) throw new GoogleOAuthException($"Bu Google domainine izin yok: {domain}");
    }

    private static string CreateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";

    private static int ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.TryGetInt32(out var value) ? value : 0;

    private static bool ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static string ExtractError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error_description", out var d)) return d.GetString() ?? "bilinmeyen hata";
            if (root.TryGetProperty("error", out var e)) return e.ToString();
        }
        catch { }
        return "bilinmeyen hata";
    }
}
