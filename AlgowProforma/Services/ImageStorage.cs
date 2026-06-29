using System;
using System.IO;

namespace AlgowProforma.Services;

public static class ImageStorage
{
    // AppPaths.LibraryRoot'tan türetilir (üretimde birebir aynı yol). MyDocuments'ı burada ikinci
    // kez hesaplamak "iki kaynaklı gerçek" üretiyordu; ayrıca expression-bodied olması testlerin
    // OverrideLibraryRootForTests yönlendirmesini görsellere de uygular.
    public static string Directory => Path.Combine(AppPaths.LibraryRoot, "images");

    public static string CoversDirectory => Path.Combine(AppPaths.LibraryRoot, "covers");

    public static string CreateNewPath(string extension = ".png")
    {
        System.IO.Directory.CreateDirectory(Directory);
        return Path.Combine(Directory, $"{Guid.NewGuid():N}{extension}");
    }

    /// <summary>designId dosya adına gömülür; geçersiz/traversal karakterlerini ('\' '/' vb.) temizler
    /// (L2 savunma derinliği). Bugün designId sabit kümeden gelir; ileride serbest-metin kaynak bağlanırsa
    /// "..\..\" kaçışını kapatır. FindCoverImage ile SaveCoverImage aynı dönüşümü kullanmalı.</summary>
    private static string SafeId(string designId)
    {
        foreach (var ch in Path.GetInvalidFileNameChars())
            designId = designId.Replace(ch, '_');
        return designId;
    }

    public static string? FindCoverImage(string designId)
    {
        if (string.IsNullOrWhiteSpace(designId)) return null;
        System.IO.Directory.CreateDirectory(CoversDirectory);
        var basePath = Path.Combine(CoversDirectory, $"cover-{SafeId(designId)}");
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".bmp" })
        {
            var p = basePath + ext;
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public static void DeleteCoverImage(string designId)
    {
        var path = FindCoverImage(designId);
        if (path != null && File.Exists(path)) File.Delete(path);
    }

    public static string SaveCoverImage(string designId, string sourcePath)
    {
        System.IO.Directory.CreateDirectory(CoversDirectory);
        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        DeleteCoverImage(designId);
        var target = Path.Combine(CoversDirectory, $"cover-{SafeId(designId)}{ext.ToLowerInvariant()}");
        File.Copy(sourcePath, target, overwrite: true);
        return target;
    }
}
