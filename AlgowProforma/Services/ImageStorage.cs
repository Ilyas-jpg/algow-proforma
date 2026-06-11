using System;
using System.IO;

namespace AlgowProforma.Services;

public static class ImageStorage
{
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Algow Proforma Kataloglar", "images");

    public static string CoversDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Algow Proforma Kataloglar", "covers");

    public static string CreateNewPath(string extension = ".png")
    {
        System.IO.Directory.CreateDirectory(Directory);
        return Path.Combine(Directory, $"{Guid.NewGuid():N}{extension}");
    }

    public static string? FindCoverImage(string designId)
    {
        if (string.IsNullOrWhiteSpace(designId)) return null;
        System.IO.Directory.CreateDirectory(CoversDirectory);
        var basePath = Path.Combine(CoversDirectory, $"cover-{designId}");
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
        var target = Path.Combine(CoversDirectory, $"cover-{designId}{ext.ToLowerInvariant()}");
        File.Copy(sourcePath, target, overwrite: true);
        return target;
    }
}
