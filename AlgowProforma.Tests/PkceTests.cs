using System.Linq;
using System.Text.RegularExpressions;
using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// PKCE (RFC 7636) çekirdeği: challenge, standardın Appendix B test vektörüyle birebir doğrulanır;
/// verifier uzunluk/karakter kümesi spec aralığındadır ve her çağrıda benzersizdir.
/// </summary>
public class PkceTests
{
    [Fact]
    public void CodeChallenge_MatchesRfc7636AppendixBVector()
        => Assert.Equal(
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            GoogleOAuthService.CreateCodeChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void CodeVerifier_LengthAndCharset_WithinSpec()
    {
        var v = GoogleOAuthService.CreateCodeVerifier();
        Assert.InRange(v.Length, 43, 128);
        Assert.Matches(new Regex("^[A-Za-z0-9_-]+$"), v);   // base64url, padding'siz
    }

    [Fact]
    public void CodeVerifier_IsUniquePerCall()
    {
        var set = Enumerable.Range(0, 50).Select(_ => GoogleOAuthService.CreateCodeVerifier()).ToHashSet();
        Assert.Equal(50, set.Count);
    }
}
