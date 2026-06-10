using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AyTeknikKatalog.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AyTeknikKatalog.Services;

public class MailSendResult
{
    public bool Success { get; init; }
    public string Recipient { get; init; } = "";
    public string? Error { get; init; }
    public DateTime At { get; init; } = DateTime.Now;
}

/// <summary>
/// MailKit ile SMTP gönderim (Google Workspace: smtp.gmail.com:587 STARTTLS + App Password).
/// PDF eki + UTF-8 gönderen adı. Toplu gönderimde tek SMTP oturumunu yeniden kullanmak için
/// Open/Send/Close ayrımı da var.
/// </summary>
public class MailService
{
    private readonly MailAccount _account;
    private readonly string _password;
    private SmtpClient? _client;

    public MailService(MailAccount account, string password)
    {
        _account = account;
        _password = password ?? "";
    }

    /// <summary>Tek mail gönderir (kendi bağlantısını açıp kapatır). Test-gönderim için ideal.</summary>
    public async Task<MailSendResult> SendOnceAsync(
        string toEmail, string? toName, string subject, string bodyText,
        string? attachmentPath, CancellationToken ct = default, string? attachmentDisplayName = null)
    {
        try
        {
            await OpenAsync(ct);
            var r = await SendAsync(toEmail, toName, subject, bodyText, attachmentPath, ct, attachmentDisplayName);
            return r;
        }
        catch (Exception ex)
        {
            return new MailSendResult { Success = false, Recipient = toEmail, Error = ex.Message };
        }
        finally
        {
            await CloseAsync();
        }
    }

    /// <summary>SMTP oturumu açar (toplu gönderim öncesi bir kez).</summary>
    public async Task OpenAsync(CancellationToken ct = default)
    {
        _client = new SmtpClient();
        await _client.ConnectAsync(_account.SmtpHost, _account.SmtpPort, SecureSocketOptions.StartTls, ct);
        await _client.AuthenticateAsync(_account.Username, _password, ct);
    }

    /// <summary>Açık oturum üzerinden tek mail gönderir (toplu gönderim döngüsünde çağrılır).</summary>
    public async Task<MailSendResult> SendAsync(
        string toEmail, string? toName, string subject, string bodyText,
        string? attachmentPath, CancellationToken ct = default, string? attachmentDisplayName = null)
    {
        try
        {
            if (_client is null || !_client.IsConnected) await OpenAsync(ct);

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_account.FromName, _account.FromEmail));
            msg.To.Add(new MailboxAddress(toName ?? "", toEmail));
            if (_account.BccSelf)
                msg.Bcc.Add(new MailboxAddress(_account.FromName, _account.FromEmail));
            msg.Subject = subject;

            var builder = new BodyBuilder { TextBody = bodyText };
            if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath))
            {
                // Disk yolu benzersiz ({id}.pdf) olabilir; ekte müşteriye GÖRÜNEN dostane ad kullanılır.
                if (!string.IsNullOrWhiteSpace(attachmentDisplayName))
                    builder.Attachments.Add(attachmentDisplayName, File.ReadAllBytes(attachmentPath));
                else
                    builder.Attachments.Add(attachmentPath);
            }
            msg.Body = builder.ToMessageBody();

            await _client!.SendAsync(msg, ct);
            return new MailSendResult { Success = true, Recipient = toEmail };
        }
        catch (Exception ex)
        {
            return new MailSendResult { Success = false, Recipient = toEmail, Error = ex.Message };
        }
    }

    public async Task CloseAsync()
    {
        if (_client is not null)
        {
            try { if (_client.IsConnected) await _client.DisconnectAsync(true); }
            catch { /* yoksay */ }
            _client.Dispose();
            _client = null;
        }
    }
}
