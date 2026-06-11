using System;
using System.IO;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// AppPaths.LibraryRoot'u geçici dizine yönlendiren testler bu collection'da SIRALI çalışır
/// (static state — paralel çakışma olmaz). Diğer test sınıfları paralel kalır.
/// </summary>
[CollectionDefinition("AppPathsIsolation")]
public class AppPathsIsolationCollection { }

/// <summary>
/// Geçici LibraryRoot fixture'ı: ctor'da benzersiz temp köke yönlendirir, Dispose'da orijinali
/// geri yükler ve temp'i siler. xUnit her test için sınıfı yeniden kurar → test başına taze kök.
/// </summary>
internal sealed class TempLibraryRoot : IDisposable
{
    private readonly string _original;
    public string Root { get; }

    public TempLibraryRoot()
    {
        _original = AppPaths.LibraryRoot;
        Root = Path.Combine(Path.GetTempPath(), "alg-it-" + Guid.NewGuid().ToString("N"));
        AppPaths.OverrideLibraryRootForTests(Root);
    }

    public void Dispose()
    {
        AppPaths.OverrideLibraryRootForTests(_original);
        try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); } catch { /* best-effort */ }
    }
}
