using System;

namespace AyTeknikKatalog.Models;

public class EmailSendLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string QuoteId { get; set; } = "";
    public string QuoteNo { get; set; } = "";
    public string Provider { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string ToEmail { get; set; } = "";
    public string RecipientCompany { get; set; } = "";
    public string RecipientContact { get; set; } = "";
    public string Subject { get; set; } = "";
    public string AttachmentFileName { get; set; } = "";
    public bool Success { get; set; }
    public string Error { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.Now;
}
