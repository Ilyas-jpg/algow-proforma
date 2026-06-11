using System.Collections.Generic;
using System.Linq;
using AlgowProforma.Models;

namespace AlgowProforma.Services;

public class EmailLogService
{
    public List<EmailSendLog> Load() => JsonStore.LoadList<EmailSendLog>(AppPaths.EmailLogsFile);

    public void Append(EmailSendLog log)
    {
        var logs = Load();
        logs.Add(log);
        JsonStore.SaveList(AppPaths.EmailLogsFile, logs.OrderByDescending(l => l.SentAt));
    }
}
