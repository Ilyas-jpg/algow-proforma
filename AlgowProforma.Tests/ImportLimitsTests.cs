using System;
using System.IO;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// İçe aktarım sınırları: eksik dosya / sınır üstü / sınır altı. Büyük dosyalar sparse üretilir
/// (FileStream.SetLength) — gerçek 25 MB yazılmaz, FileInfo.Length yine sınırı raporlar.
/// </summary>
public class ImportLimitsTests
{
    private static string TempFile(long length = 0)
    {
        var path = Path.Combine(Path.GetTempPath(), $"alg-limit-{Guid.NewGuid():N}.bin");
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        if (length > 0) fs.SetLength(length);
        return path;
    }

    [Fact]
    public void EnsureExcelSize_MissingFile_ThrowsFileNotFound()
        => Assert.Throws<FileNotFoundException>(() => ImportLimits.EnsureExcelSize(@"C:\yok\boyle\dosya.xlsx"));

    [Fact]
    public void EnsureExcelSize_OverLimit_ThrowsWithTurkishMessage()
    {
        var path = TempFile(ImportLimits.MaxExcelBytes + 1);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ImportLimits.EnsureExcelSize(path));
            Assert.Contains("çok büyük", ex.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void EnsureExcelSize_AtLimit_Passes()
    {
        var path = TempFile(ImportLimits.MaxExcelBytes);
        try { ImportLimits.EnsureExcelSize(path); }   // fırlatmamalı
        finally { File.Delete(path); }
    }

    [Fact]
    public void IsImageSizeOk_MissingFile_FalseWithError()
    {
        Assert.False(ImportLimits.IsImageSizeOk(@"C:\yok\gorsel.png", out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void IsImageSizeOk_OverLimit_FalseWithError()
    {
        var path = TempFile(ImportLimits.MaxImageBytes + 1);
        try
        {
            Assert.False(ImportLimits.IsImageSizeOk(path, out var error));
            Assert.Contains("çok büyük", error);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void IsImageSizeOk_UnderLimit_True()
    {
        var path = TempFile(1024);
        try
        {
            Assert.True(ImportLimits.IsImageSizeOk(path, out var error));
            Assert.Equal("", error);
        }
        finally { File.Delete(path); }
    }
}
