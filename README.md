# Algow Proforma PDF

[![build](https://github.com/Ilyas-jpg/algow-proforma/actions/workflows/build.yml/badge.svg)](https://github.com/Ilyas-jpg/algow-proforma/actions/workflows/build.yml)

Masaüstü **teklif / proforma + ürün kataloğu PDF üretim aracı**. Tek programda: katalog tasarımı, fiyat teklifi (proforma), müşteri yönetimi (CRM), fiyat havuzu ve toplu e-posta gönderimi.

> **White-label:** Marka kimliği (logo, firma adı, imza, iletişim, renk teması) tamamen **uygulama içinden yapılandırılır** — hiçbir müşteriye sabitlenmemiştir. Algow, ürünü kendi markası altında geliştirir; her son kullanıcı kendi kimliğini girer.

**Üretici:** AlgowAI · **Platform:** Windows 10/11 (x64) · **Tür:** .NET 8 WPF masaüstü uygulaması

---

## Özellikler

- **Katalog PDF** — kapak tasarımı, ürün ızgaraları (1–16/sayfa), referans sayfası, 23 renk teması, 13 kapak tasarımı, serbest kapak tasarım stüdyosu
- **Fiyat teklifi / proforma PDF** — kalem tablosu, KDV (oransal iskonto dağıtımı), para birimi, geçerlilik, premium şablon
- **Müşteri yönetimi (CRM)** — arama/filtre, Excel içe/dışa aktarım
- **Fiyat havuzu** — Excel'den beslenir, toplu yüzde zam
- **Toplu e-posta** — Gmail API (OAuth2) veya SMTP; zorunlu önizleme + onay, hız sınırı, gönderim logu
- **PDF kalitesi** — QuestPDF + Linearize (Fast Web View) → anında ilk-sayfa render

## Teknoloji

| Katman | Teknoloji |
|---|---|
| UI | WPF (.NET 8), MVVM (CommunityToolkit.Mvvm) |
| PDF | QuestPDF 2026.5 + SkiaSharp/HarfBuzz |
| Excel | ClosedXML |
| Mail | Gmail API (OAuth2, loopback) · MailKit (SMTP fallback) |
| Sırlar | Windows DPAPI (ProtectedData) — parola/token şifreli, kaynakta değil |

## Geliştirme

```powershell
# Gereksinim: .NET 8 SDK (windows)
dotnet restore AlgowProforma.sln
dotnet build   AlgowProforma.sln -c Release      # 0 hata / 0 uyarı
dotnet run --project AlgowProforma/AlgowProforma.csproj   # uygulamayı başlat
```

### Self-test komutları (headless, PDF üretimini doğrular)

```powershell
dotnet run --project AlgowProforma/AlgowProforma.csproj -c Release -- --test-quote    # örnek teklif PDF'i → Masaüstü
dotnet run --project AlgowProforma/AlgowProforma.csproj -c Release -- --test-covers   # katalog/kapak smoke testi
```

### Dağıtılabilir kurulum (Inno Setup)

```powershell
dotnet publish AlgowProforma/AlgowProforma.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
# installer/AlgowProforma.iss → ISCC ile derlenir (kod imzalama sertifikası repoda DEĞİL)
```

> **Docker notu:** WPF masaüstü uygulaması Windows + grafik oturumu gerektirir; container'da çalışmaz. "Sağlam pipeline" karşılığı **GitHub Actions CI**'dır (`.github/workflows/build.yml`) — her push'ta `windows-latest` üzerinde restore + build + xUnit test suite (render-smoke dahil) + teklif-PDF smoke testi.

## Veri konumları (kullanıcı makinesinde)

- Kataloglar / teklifler / ayarlar: `Belgeler\Algow Proforma Kataloglar\`
- Çökme/otomatik kayıt logu: `%LOCALAPPDATA%\AlgowProforma\`

## Lisans

© 2026 AlgowAI. Tüm hakları saklıdır — açık kaynak lisansı değildir, ayrıntı: [LICENSE](LICENSE).
