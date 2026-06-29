namespace AlgowProforma.Models;

/// <summary>
/// Gömülü Google OAuth Desktop-client kimlikleri. Bu dosya repo'da PLACEHOLDER ("") tutulur;
/// gerçek değerler yalnız dağıtım/derleme makinesinde
///   git update-index --skip-worktree AlgowProforma/Models/OAuthDefaults.cs
/// ile yerel olarak doldurulur ve PUBLIC repo'ya commit EDİLMEZ (GitHub secret-scanning bir
/// Google client secret'i görürse onu otomatik iptal eder → app tamamen çalışmaz hale gelir).
///
/// Desktop OAuth client secret'i Google tarafından "gizli" sayılmaz (PKCE korur), ama public
/// repo hijyeni için gerçek kopya yine de skip-worktree ile repo dışında tutulur.
///
/// Boş kaldığında (CI / başka makine / repo checkout) app client'ı env var
/// (GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET) veya Ayarlar'dan okumaya devam eder — yani
/// placeholder hâli derlenir ve mevcut davranışı bozmaz.
/// </summary>
internal static class OAuthDefaults
{
    public const string ClientId = "";
    public const string ClientSecret = "";
}
