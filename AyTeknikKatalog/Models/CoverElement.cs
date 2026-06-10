using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AyTeknikKatalog.Models;

public enum CoverElementType
{
    Image,      // dış dosya görsel (foto)
    Title,      // büyük başlık
    TextBox,    // serbest metin (slogan, açıklama)
    Rectangle,  // dolgu/border kutu (dekoratif veya panel)
    Line,       // ince çizgi (yatay/dikey)
}

public partial class CoverElement : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private CoverElementType _type;

    // Konum (A4 milimetre cinsinden, 210×297)
    [ObservableProperty] private double _xMm;
    [ObservableProperty] private double _yMm;
    [ObservableProperty] private double _widthMm = 50;
    [ObservableProperty] private double _heightMm = 20;
    [ObservableProperty] private int _zIndex;

    // İçerik (Image: dosya yolu, Title/TextBox: metin)
    [ObservableProperty] private string _content = "";

    // Stil
    [ObservableProperty] private double _fontSize = 18;
    [ObservableProperty] private bool _bold;
    [ObservableProperty] private bool _italic;
    [ObservableProperty] private string _foregroundHex = "#000000";
    [ObservableProperty] private string _backgroundHex = "";  // boş = transparan
    [ObservableProperty] private double _opacity = 1.0;

    public CoverElement Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Type = Type, XMm = XMm, YMm = YMm, WidthMm = WidthMm, HeightMm = HeightMm,
        ZIndex = ZIndex, Content = Content,
        FontSize = FontSize, Bold = Bold, Italic = Italic,
        ForegroundHex = ForegroundHex, BackgroundHex = BackgroundHex,
        Opacity = Opacity,
    };
}
