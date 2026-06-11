using System;

namespace AlgowProforma.Models;

public class CatalogEntry
{
    public string Name { get; init; } = "";
    public string PdfPath { get; init; } = "";
    public string JsonPath { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public int ProductCount { get; init; }
    public int ReferenceCount { get; init; }
    public string BrandName { get; init; } = "";
    public string LibraryLabel { get; init; } = "";

    public string DisplayTitle => string.IsNullOrWhiteSpace(LibraryLabel) ? BrandName : LibraryLabel;
    public string DisplaySubtitle => string.IsNullOrWhiteSpace(LibraryLabel) ? "" : BrandName;
}
