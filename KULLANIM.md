# Algow Proforma PDF — Kullanım Kılavuzu

> Sürüm: 1.3.0 · Windows 10/11 (x64) · Bu kılavuz son kullanıcı içindir; geliştirici kurulumu için [README.md](README.md), Gmail/Workspace bağlantısı için [GOOGLE_WORKSPACE_SETUP.md](GOOGLE_WORKSPACE_SETUP.md).

Algow Proforma PDF, tek programda **ürün kataloğu PDF'i**, **fiyat teklifi / proforma PDF'i**, **müşteri yönetimi**, **fiyat havuzu** ve **toplu e-posta gönderimi** sunan masaüstü araçtır. White-label'dır: logo, firma adı, iletişim ve renkler tamamen sizin kimliğinizle çalışır — programın içinde hiçbir markaya sabitlenmiş içerik yoktur.

---

## İçindekiler

1. [Kurulum](#1-kurulum)
2. [Hızlı başlangıç — 5 dakikada ilk katalog](#2-hızlı-başlangıç)
3. [Marka bilgileri (white-label kimlik)](#3-marka-bilgileri)
4. [Katalog hazırlama](#4-katalog-hazırlama)
5. [Ürün kütüphanesi](#5-ürün-kütüphanesi)
6. [Excel ile çalışma](#6-excel-ile-çalışma)
7. [Fiyat listesi (havuz)](#7-fiyat-listesi)
8. [Müşteriler](#8-müşteriler)
9. [Fiyat teklifi / proforma](#9-fiyat-teklifi--proforma)
10. [Toplu teklif gönderimi](#10-toplu-teklif-gönderimi)
11. [Ayarlar](#11-ayarlar)
12. [Verileriniz nerede? Yedekleme](#12-verileriniz-nerede-yedekleme)
13. [Sorun giderme](#13-sorun-giderme)
14. [Klavye kısayolları](#14-klavye-kısayolları)

---

## 1. Kurulum

1. `AlgowProforma-Setup-X.Y.Z.exe` dosyasını çalıştırın. Kurulum **yönetici izni gerektirmez** (kullanıcı profiline kurulur).
2. Windows SmartScreen "tanınmayan uygulama" uyarısı gösterirse: **Daha fazla bilgi → Yine de çalıştır**. (Uygulama kod imzasızdır; bu uyarı zararlı olduğu anlamına gelmez, yalnızca yayıncının Microsoft'ta kayıtlı olmadığını söyler.)
3. Kurulum sırasında eksikse Microsoft Visual C++ çalışma zamanı otomatik kurulur.
4. Kaldırmak için: Ayarlar → Uygulamalar → **Algow Proforma PDF** → Kaldır. Verileriniz (kataloglar, teklifler, müşteriler) **silinmez** — `Belgeler` altında kalır.

> Güncelleme: yeni sürümün Setup'ını çalıştırmanız yeterli; üzerine kurar, verilere dokunmaz.

## 2. Hızlı başlangıç

İlk katalog PDF'inizi üretmek için:

1. **Marka Bilgileri** sekmesinde firma adınızı yazın, **Logo Seç** ile logonuzu yükleyin, telefon/e-posta/web/adres girin.
2. **Ürünler** sekmesinde **Ürün Ekle** ile birkaç ürün girin (ad zorunlu; görsel, kod ve fiyat isteğe bağlı).
3. **Tasarımlar** sekmesinden bir kapak tasarımı, **Temalar** sekmesinden bir renk teması seçin.
4. Sağ üstteki **PDF Üret**'e basın, kataloğa bir isim verin.
5. PDF üretilir, kütüphaneye kaydedilir ve görüntüleyicide açılır. Hepsi bu.

Teklif için: üst bardaki **Teklif** düğmesi → kalemleri girin → **PDF Üret**. (Ayrıntı: [bölüm 9](#9-fiyat-teklifi--proforma).)

## 3. Marka bilgileri

**Marka Bilgileri** sekmesi white-label kimliğinizdir; hem katalog hem teklif PDF'lerinde kullanılır.

- **Marka adı (zorunlu)** — PDF başlıklarında ve dosya adlarında görünür.
- **Logo** — PNG/JPG. Kapaklarda ve teklif başlığında çıkar. *Logonun arkasında beyaz dikdörtgen göster* seçeneği koyu zeminli kapaklarda okunabilirlik içindir.
- **Slogan, hakkımızda metni** — bazı kapak tasarımlarında yer alır.
- **Filigran** — sayfaların arkasına soluk metin basar (örn. `TASLAK`, `ÖRNEKTİR`). Boş bırakılırsa kapalıdır.
- **İletişim** (telefon, e-posta, web, adres) — kapak iletişim bandında ve teklif altlığında.

Marka bilgisi **katalogla birlikte** saklanır; ayrıca son kullandığınız kimlik kalıcı profil olarak tutulur ve teklif/toplu gönderim PDF'leri her zaman bu kalıcı profili kullanır.

## 4. Katalog hazırlama

### Ürünler

- **Ürün Ekle** ile tek tek, **Excel'den İçe Aktar** ile toplu ekleyin ([bölüm 6](#6-excel-ile-çalışma)).
- Her üründe: ad (zorunlu), kod, fiyat + para birimi, görsel.
- **Bu ürünü öne çıkar** — PDF'te 2 sütun genişliğinde büyük kart olur.
- **Tablo modu** — ürünü başlık + özellik tablosundan oluşan tam genişlik blok olarak basar (teknik ürünler için).
- Satırdaki ok düğmeleriyle sıralayın; sıra PDF'e aynen yansır.
- Satırdaki **yer imi** düğmesi ürünü kalıcı kütüphaneye kaydeder ([bölüm 5](#5-ürün-kütüphanesi)).

### Sayfa düzeni

**Sayfa Düzeni** sekmesinde sayfa başına ürün sayısını seçin: 1, 2, 4, 6, 8, 9 (varsayılan), 12 (iki varyant) veya 16. Az ürün = büyük görselli vitrin; 16 = yoğun fiyat-listesi tarzı. İsterseniz *özel sayfalar* ile farklı bölümlere farklı düzen atayabilirsiniz.

### Kapak ve tema

- **Tasarımlar** sekmesinde **13 hazır kapak** vardır (Klasik, Eğri, Çerçeve, Geometri, Madalya, Katman, Mozaik, Diyagonal, Minimalist, Sertifika, Teknik Çizim, Akış, Yatay Bant). Önizlemeler gerçek render'dır — ne görüyorsanız PDF'te onu alırsınız.
- **Temalar** sekmesinde **23 renk paleti** vardır; kapak + ürün kartları + başlıklar temaya göre boyanır.
- **Kapak Sayfası** sekmesinden başlık/alt başlık/yıl gibi kapak metinlerini, kapak görselini ve iletişim bandını yönetirsiniz.
- **Kapak Tasarım Stüdyosu** ile hazır kapak yerine kendi serbest kompozisyonunuzu (metin/görsel elementlerle) kurabilirsiniz.

### Referanslar

**Referanslar** sekmesine iş ortağı/müşteri logoları ekleyin; PDF sonunda otomatik referans sayfası oluşur.

### PDF üretimi

**PDF Üret** → kataloğa kütüphane adı verin → PDF `Belgeler\Algow Proforma Kataloglar` altına yazılır ve açılır. Katalog aynı anda **kütüphaneye** kaydedilir; **Kütüphane** düğmesinden veya **Son Kataloglar**'dan tekrar açabilirsiniz.

> PDF'ler *Fast Web View* (linearized) üretilir — büyük kataloglar görüntüleyicide ilk sayfayı beklemeden açılır.

## 5. Ürün kütüphanesi

Kütüphane, **katalogdan bağımsız, kalıcı** ürün havuzudur: bir kez girdiğiniz ürünü (görseliyle) her yeni katalogda yeniden kullanırsınız.

- **Kütüphaneye kaydetme:** ürün satırındaki yer imi düğmesi (tek ürün) veya **Kütüphaneye Kaydet** (katalogdaki tümü). Aynı kod/ad varsa ürün **güncellenir**, kopya oluşmaz.
- **Kataloğa ekleme:** **Kütüphaneden Ekle** → istediğiniz ürünün **Kataloğa Ekle** düğmesi. Eklenen kopya bağımsızdır; katalogda yapacağınız değişiklik kütüphaneyi bozmaz.
- **Excel'den İçe Aktar / Excel'e Aktar:** kütüphane penceresinin sağ üstünde — Excel listenizi katalogdan geçirmeden doğrudan kütüphaneye alır; kütüphaneyi kod/ad/fiyat/para birimi sütunlarıyla dışa verir.

## 6. Excel ile çalışma

**Ürünler → Excel'den İçe Aktar**:

1. Dosyanızı seçin (en fazla 25 MB).
2. Sayfa ve sütunları eşleyin (kod / ad / fiyat sütunu). Program sütunları otomatik tahmin eder; gerekirse düzeltin.
3. Önizlemeden onaylayın — ürünler kataloğa eklenir.

Aynı sihirbaz ürün kütüphanesinde ve fiyat listesinde de kullanılır. Müşteriler ekranının kendi Excel içe/dışa aktarımı vardır.

## 7. Fiyat listesi

**Fiyat Listesi** düğmesi firma geneli fiyat havuzunu açar: kod, ad, açıklama, birim, birim fiyat, KDV, para birimi, kategori.

- Excel'den beslenir, Excel'e verilir.
- **Toplu zam:** seçtiğiniz kalemlere veya tümüne yüzde uygulayın.
- Teklif yazarken kalemleri bu havuzdan seçebilirsiniz — fiyatlar elle yazılmaz, havuzdan gelir.

## 8. Müşteriler

**Müşteriler** ekranı basit ama yeterli bir CRM'dir: firma, yetkili (hitap: Bey/Hanım), e-posta, telefon, adres, vergi dairesi/no.

- Arama ve filtreleme üstte.
- Excel içe/dışa aktarım düğmeleri ile mevcut listenizi taşıyın.
- Teklife müşteri seçtiğinizde bilgiler **o anki haliyle teklife işlenir** (snapshot) — sonradan müşteri kartını değiştirmeniz eski teklifleri bozmaz.

## 9. Fiyat teklifi / proforma

**Teklif** düğmesi teklif editörünü açar:

- **Kalemler:** ad, açıklama, miktar, birim, birim fiyat, satır iskontosu (%), KDV oranı. Fiyat havuzundan seçerek de ekleyebilirsiniz.
- **Genel iskonto:** yüzde veya tutar olarak; KDV hesabına oransal dağıtılır.
- **Şartlar:** ödeme, teslim süresi/yeri, kur notu, serbest notlar.
- **Geçerlilik:** gün sayısı girilir; bitiş tarihi otomatik hesaplanıp PDF'e yazılır.
- **Teklif numarası:** kaydederken otomatik atanır — biçim `ÖNEK-YIL-SIRA` (örn. `TKF-2026-0001`). Ön eki Ayarlar'dan belirlersiniz; boşsa `2026-0001` biçimi kullanılır. Numara **kayıtta** atanır; vazgeçilen taslak numara tüketmez.
- **Canlı önizleme:** editörün sağındaki panel, siz yazdıkça teklif PDF'inin ilk sayfasını gösterir (kısa bir gecikmeyle kendiliğinden yenilenir).
- **Dil:** `Türkçe` veya `English` — EN seçilirse tüm PDF etiketleri İngilizce basılır (başlık **PROFORMA INVOICE**) ve sayı biçimi `1,234.56` olur; ihracat müşterisi için birebir.
- **Şablon:** `Modern` (renkli, varsayılan) · `Kompakt` (dar kenar + küçük punto, uzun listeler tek sayfaya) · `Sade` (tek renk, dolgusuz — yazıcı dostu).
- **TCMB Kurunu Çek:** günün resmî döviz satış kurlarını (USD/EUR) tek tıkla **Kur Notu** alanına yazar; internet yoksa elle de doldurabilirsiniz.
- **PDF Üret** teklif PDF'ini oluşturur ve açar. Renkler Ayarlar'daki **Teklif teması**ndan gelir ([bölüm 11](#11-ayarlar)).
- Kaydedilen teklifler `Belgeler\Algow Proforma Kataloglar\Teklifler` altında durur; editörden tekrar açıp **revizyon** olarak güncelleyebilirsiniz. Revizyon PDF'leri `-rev1`, `-rev2`… ekiyle ayrı dosya olur, öncekinin üstüne yazılmaz.

### Teklifler panosu (durum takibi)

Ana penceredeki **Teklifler** düğmesi kayıtlı tüm teklifleri listeler: numara, tarih, tutar ve **durum** (Taslak / Gönderildi / Onaylandı / Reddedildi). Başlıkta özet sayaç görürsünüz. Durumu listeden değiştirdiğiniz anda kaydedilir; e-postayla başarıyla gönderilen teklif **kendiliğinden** Taslak → Gönderildi olur. **Aç** teklifi editörde açar, **PDF** üretilmiş PDF'i gösterir.

## 10. Toplu teklif gönderimi

**Toplu Gönderim**, kayıtlı bir teklifi birden çok müşteriye **kişiselleştirilmiş PDF + kişiselleştirilmiş e-posta** ile yollar:

1. Gönderilecek teklifi seçin.
2. Alıcıları müşteri listenizden işaretleyin (hitap ve ad şablona otomatik girer). **Etiket süzgeci** kutusuna örn. `bayi` yazarsanız *CRM'den Yükle* yalnız o etiketli müşterileri getirir (müşteri kartındaki *Etiketler* alanına göre).
3. **Önizleme zorunludur** — örnek e-postayı görmeden gönderim başlamaz.
4. Onaylayın; program her alıcı için teklifi kopyalar, müşteri bilgisini işler, PDF üretir ve sırayla gönderir.

Güvenlik ve hijyen:

- **Hız sınırı:** dakikada gönderim adedi Ayarlar'dan (varsayılan 20) — spam filtrelerine takılmamak için.
- **Gönderim logu** tutulur (kime, ne zaman, hangi teklif, sonuç).
- **BCC arşiv** açıksa her mailin kopyası size düşer.
- Gönderim **Gmail API (önerilen)** veya **SMTP** ile yapılır — kurulum için [GOOGLE_WORKSPACE_SETUP.md](GOOGLE_WORKSPACE_SETUP.md). SMTP'de normal şifre değil **Uygulama Şifresi (App Password)** kullanılır; şifreler diske **Windows DPAPI ile şifrelenerek** yazılır, düz metin saklanmaz.

## 11. Ayarlar

| Bölüm | Ne işe yarar |
|---|---|
| **Google Workspace / Gmail** | OAuth istemci bilgileri, Google girişi, Gmail gönderim bağlantısı, izinli domain kısıtı |
| **Mail Hesabı (SMTP)** | Sunucu/port, gönderen ad/adres, App Password (şifreli saklanır), BCC arşiv, dakikada mail sınırı, test e-postası |
| **Teklif PDF** | **Teklif teması** (23 paletten; yalnızca teklif/proformayı boyar, katalog teması ayrıdır) ve **teklif no ön eki** |
| **Veri Yedeği** | **Yedek Al (ZIP)** tüm verinizi tek dosyaya alır; **Yedekten Geri Yükle** geri getirir (mevcut veri silinmez, güvenlik kopyasına taşınır) |
| **E-posta Şablonu** | Konu, hitaplar (`{ad}` yer tutucu), gövde, kapanış, imza, ek dosya adı kalıbı (`{musteri}`, `{tarih}`) |

Değişiklikler **Kaydet** ile kalıcı olur.

## 12. Verileriniz nerede? Yedekleme

| Veri | Konum |
|---|---|
| Kataloglar, görseller, PDF'ler | `Belgeler\Algow Proforma Kataloglar\` |
| Teklifler | `Belgeler\Algow Proforma Kataloglar\Teklifler\` |
| Müşteriler, fiyat havuzu, ürün kütüphanesi, ayarlar, sayaç | `...\Algow Proforma Kataloglar\data\` |
| Şifreli kimlik bilgileri (DPAPI) | `...\data\*.bin` — yalnızca **bu kullanıcı + bu makine** çözebilir |
| Çökme kurtarma (otomatik kayıt) + hata logu | `%LOCALAPPDATA%\AlgowProforma\` |

**Yedekleme (önerilen yol):** Ayarlar → **Veri Yedeği → Yedek Al (ZIP)** tüm veriyi tek dosyaya alır; **Yedekten Geri Yükle** ile geri getirirsiniz (mevcut veri silinmez, `pre-restore` güvenlik kopyasına taşınır; geri yükleme sonrası uygulamayı yeniden başlatın). Elle yedek isterseniz `Belgeler\Algow Proforma Kataloglar` klasörünü kopyalamak da yeterlidir. `.bin` dosyaları başka makinede çözülemez (tasarım gereği) — yeni makinede şifre/Google bağlantısını bir kez yeniden girersiniz.

**Veri güvenliği içeride nasıl:** tüm JSON yazımları atomiktir (önce geçici dosya, sonra yer değiştirme) — elektrik kesilse bile dosya yarım kalmaz. Bozuk bir dosya bulunursa üzerine yazılmadan `.corrupt` kopyası alınır. Uygulama beklenmedik kapanırsa bir sonraki açılışta **otomatik kayıttan devam** önerilir.

## 13. Sorun giderme

| Belirti | Çözüm |
|---|---|
| Kurulumda SmartScreen uyarısı | **Daha fazla bilgi → Yine de çalıştır** (bkz. [Kurulum](#1-kurulum)) |
| **Adobe Acrobat** PDF açarken "Font Capture / Uygulama Hatası (0xc06d007e)" | Acrobat'ın Türkçe karakter yoğun PDF'lerdeki bilinen hatasıdır, PDF sağlamdır. PDF'i **Microsoft Edge** ile açın (varsayılan yapmanız yeterli). |
| Mail gönderilemiyor (SMTP) | App Password kullandığınızdan emin olun (normal şifre çalışmaz); port 587; "Test Gönder" ile deneyin. |
| Gmail bağlantısı kopuyor / "refresh token yok" | Google hesabınızdan eski izni kaldırıp **Connect Gmail**'i yeniden çalıştırın ([rehber](GOOGLE_WORKSPACE_SETUP.md)). |
| Excel alınamıyor | Dosya 25 MB sınırını aşıyor olabilir; gereksiz sayfaları/sütunları silip tekrar deneyin. |
| Görsel eklenemiyor | Görsel başına 20 MB sınırı vardır; fotoğrafı küçültün. |
| Program açılmıyor / tuhaf davranıyor | `%LOCALAPPDATA%\AlgowProforma\` içindeki log dosyasına bakın; sorunu bildirirken bu dosyayı ekleyin. |

Kendi kendine sağlık testi (teknik kullanıcılar): uygulama `--test-quote` ve `--test-covers` parametreleriyle başlatılırsa örnek PDF üretip çıkar; `0` çıkış kodu = sağlıklı.

## 14. Klavye kısayolları

| Kısayol | İşlev |
|---|---|
| `Ctrl+N` | Yeni katalog |
| `Ctrl+O` | Son üretilen PDF'i aç |
| `Ctrl+L` | Kütüphaneyi aç |
| `Ctrl+P` / `Ctrl+S` | PDF üret (kaydet) |
| `F5` | Kapak önizlemesini yenile |

---

© 2026 AlgowAI · [algow.net](https://algow.net) · Sorular: info@algow.net
