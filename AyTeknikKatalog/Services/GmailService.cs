using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AyTeknikKatalog.Models;
using MimeKit;

namespace AyTeknikKatalog.Services;

public class GmailService
{
    // Tek paylaşımlı HttpClient — soket tükenmesini önler. HttpClient thread-safe.
    private static readonly HttpClient _http = new();

    public async Task<MailSendResult> SendAsync(
        GoogleOAuthSettings oauthSettings,
        GoogleGmailCredential credential,
        string fromName,
        string toEmail,
        string? toName,
        string subject,
        string bodyText,
        string? attachmentPath,
        string? attachmentDisplayName = null,
        CancellationToken ct = default)
    {
        try
        {
            if (credential.AccessTokenExpired)
            {
                var refreshed = await new GoogleOAuthService().RefreshAsync(oauthSettings, credential, ct);
                CredentialStore.SaveGmailCredential(refreshed);
                credential = refreshed;
            }

            var msg = BuildMimeMessage(
                fromName,
                credential.GoogleEmail,
                toEmail,
                toName,
                subject,
                bodyText,
                attachmentPath,
                attachmentDisplayName);

            using var stream = new MemoryStream();
            await msg.WriteToAsync(stream, ct);
            var raw = Base64UrlEncode(stream.ToArray());

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
            req.Content = new StringContent(JsonSerializer.Serialize(new { raw }), Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return new MailSendResult { Success = false, Recipient = toEmail, Error = ExtractGoogleError(json) };

            return new MailSendResult { Success = true, Recipient = toEmail };
        }
        catch (Exception ex)
        {
            return new MailSendResult { Success = false, Recipient = toEmail, Error = ex.Message };
        }
    }

    public static MimeMessage BuildMimeMessage(
        string fromName,
        string fromEmail,
        string toEmail,
        string? toName,
        string subject,
        string bodyText,
        string? attachmentPath,
        string? attachmentDisplayName = null)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(fromName ?? "", fromEmail));
        msg.To.Add(new MailboxAddress(toName ?? "", toEmail));
        msg.Subject = subject;

        var builder = new BodyBuilder
        {
            TextBody = bodyText,
            HtmlBody = TextToHtml(bodyText),
        };

        if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath))
        {
            if (!string.IsNullOrWhiteSpace(attachmentDisplayName))
                builder.Attachments.Add(attachmentDisplayName, File.ReadAllBytes(attachmentPath));
            else
                builder.Attachments.Add(attachmentPath);
        }

        msg.Body = builder.ToMessageBody();
        return msg;
    }

    public static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string TextToHtml(string text)
    {
        var encoded = HtmlEncoder.Default.Encode(text ?? "");
        return "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.55;color:#111827;\">"
            + Regex.Replace(encoded, @"\r?\n", "<br>")
            + "</div>";
    }

    private static string ExtractGoogleError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var msg)) return msg.GetString() ?? "Gmail API hatası";
                return error.ToString();
            }
        }
        catch { }
        return "Gmail API hatası";
    }
}
