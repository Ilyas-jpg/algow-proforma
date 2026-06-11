using AlgowProforma.Services;
using Xunit;

namespace AlgowProforma.Tests;

/// <summary>
/// Teklif numarası biçimi (white-label). Teklif-no ön eki configurable (AppSettings.QuoteNoPrefix):
/// ön ek boşsa salt yıl-sayaç, doluysa "{ÖNEK}-{yıl}-{sıra}". FormatNo, sayaç I/O'su olmayan saf çekirdektir.
/// </summary>
public class QuoteNumberingTests
{
    [Fact]
    public void FormatNo_WithPrefix_PrependsIt()
        => Assert.Equal("TKF-2026-0007", QuoteService.FormatNo("TKF", 2026, 7));

    [Fact]
    public void FormatNo_EmptyPrefix_YearAndSeqOnly()
        => Assert.Equal("2026-0007", QuoteService.FormatNo("", 2026, 7));

    [Fact]
    public void FormatNo_NullPrefix_YearAndSeqOnly()
        => Assert.Equal("2026-0001", QuoteService.FormatNo(null, 2026, 1));

    [Fact]
    public void FormatNo_TrimsPrefix()
        => Assert.Equal("ABC-2026-0042", QuoteService.FormatNo("  ABC  ", 2026, 42));

    [Fact]
    public void FormatNo_PadsSequenceToFourDigits()
        => Assert.Equal("2026-1234", QuoteService.FormatNo("", 2026, 1234));
}
