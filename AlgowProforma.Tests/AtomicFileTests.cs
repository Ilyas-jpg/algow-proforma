using System;
using System.IO;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>Atomik yazım + bozuk-dosya yedeği (R-1/L4) sözleşmesi.</summary>
public class AtomicFileTests
{
    private static string Temp(string ext) => Path.Combine(Path.GetTempPath(), $"alg-atomic-{Guid.NewGuid():N}{ext}");

    [Fact]
    public void WriteAllText_CreatesFile_WithExactContent_NoTmpLeftBehind()
    {
        var path = Temp(".txt");
        try
        {
            AtomicFile.WriteAllText(path, "merhaba ş ç ı ö ü");
            Assert.True(File.Exists(path));
            Assert.Equal("merhaba ş ç ı ö ü", File.ReadAllText(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void WriteAllText_Overwrites_Existing()
    {
        var path = Temp(".txt");
        try
        {
            AtomicFile.WriteAllText(path, "first");
            AtomicFile.WriteAllText(path, "second");
            Assert.Equal("second", File.ReadAllText(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void BackupCorrupt_CreatesCorruptCopy_PreservingContent()
    {
        var path = Temp(".json");
        var bak = path + ".corrupt";
        try
        {
            File.WriteAllText(path, "bozuk-icerik");
            AtomicFile.BackupCorrupt(path);
            Assert.True(File.Exists(bak));
            Assert.Equal("bozuk-icerik", File.ReadAllText(bak));
        }
        finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(bak)) File.Delete(bak); }
    }
}
