using PhoneGrapher.Infrastructure.Services.Analytics;

namespace PhoneGrapher.Infrastructure.Tests;

public class AnalyticsWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("today", 1)]
    [InlineData("7d", 7)]
    [InlineData("30d", 30)]
    [InlineData("quarter", 90)]
    public void Create_DatDungDoDaiKhoang(string range, int expectedDays)
    {
        var window = AnalyticsWindow.Create(range, Now);

        Assert.Equal(TimeSpan.FromDays(expectedDays), window.Duration);
        Assert.Equal(Now, window.ToUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khong-ton-tai")]
    [InlineData("90d")]
    public void Create_KhoangLa_RoiVe30d(string? range)
    {
        var window = AnalyticsWindow.Create(range, Now);

        Assert.Equal("30d", window.Range);
        Assert.Equal(TimeSpan.FromDays(30), window.Duration);
    }

    [Theory]
    [InlineData("TODAY", "today")]
    [InlineData("  7D  ", "7d")]
    public void Create_ChapNhanChuHoaVaKhoangTrang(string input, string expected)
    {
        Assert.Equal(expected, AnalyticsWindow.Create(input, Now).Range);
    }

    [Fact]
    public void Create_KyTruocDaiBangKyHienTaiVaKeSatPhiaTruoc()
    {
        var window = AnalyticsWindow.Create("30d", Now);

        Assert.Equal(window.FromUtc - TimeSpan.FromDays(30), window.PreviousFromUtc);
        Assert.Equal(window.Duration, window.FromUtc - window.PreviousFromUtc);
    }

    [Fact]
    public void Buckets_LuonTraVe12Doan()
    {
        Assert.Equal(12, AnalyticsWindow.Create("today", Now).Buckets().Count);
        Assert.Equal(12, AnalyticsWindow.Create("quarter", Now).Buckets().Count);
    }

    [Fact]
    public void Buckets_PhuKinKhoangVaNoiTiepNhau()
    {
        var window = AnalyticsWindow.Create("30d", Now);
        var buckets = window.Buckets();

        Assert.Equal(window.FromUtc, buckets[0].StartUtc);
        Assert.Equal(window.ToUtc, buckets[^1].EndUtc);

        for (var i = 1; i < buckets.Count; i++)
        {
            Assert.Equal(buckets[i - 1].EndUtc, buckets[i].StartUtc);
        }
    }

    [Fact]
    public void Buckets_KhoangHomNay_NhanTheoGio()
    {
        var buckets = AnalyticsWindow.Create("today", Now).Buckets();

        Assert.All(buckets, b => Assert.Matches(@"^\d{2}:\d{2}$", b.Label));
    }

    [Fact]
    public void Buckets_KhoangDaiHon_NhanTheoNgayThang()
    {
        var buckets = AnalyticsWindow.Create("30d", Now).Buckets();

        Assert.All(buckets, b => Assert.Matches(@"^\d{2}/\d{2}$", b.Label));
    }

    [Fact]
    public void Buckets_NhanDocTheoGioVietNam()
    {
        // 2026-07-29 10:00 UTC là 17:00 giờ Việt Nam cùng ngày.
        var window = AnalyticsWindow.Create("today", Now);
        var lastBucketStart = window.Buckets()[^1].StartUtc;

        Assert.Equal(new DateTimeOffset(2026, 7, 29, 8, 0, 0, TimeSpan.Zero), lastBucketStart);
        Assert.Equal("15:00", window.Buckets()[^1].Label);
    }
}
