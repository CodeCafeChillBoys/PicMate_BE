using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Options;
using PhoneGrapher.Infrastructure.Persistence;
using PhoneGrapher.Infrastructure.Services.Analytics;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace PhoneGrapher.Infrastructure.Tests;

public class AdminAnalyticsServiceTests
{
    private static AdminAnalyticsService CreateService(
        PhoneGrapherDbContext db,
        VietQrOptions? vietQr = null,
        VnPayOptions? vnPay = null,
        BrevoOptions? brevo = null,
        SmtpOptions? smtp = null)
        => new(
            db,
            MsOptions.Create(vietQr ?? new VietQrOptions()),
            MsOptions.Create(vnPay ?? new VnPayOptions()),
            MsOptions.Create(brevo ?? new BrevoOptions()),
            MsOptions.Create(smtp ?? new SmtpOptions()),
            NullLogger<AdminAnalyticsService>.Instance);

    /// <summary>Thêm một booking, kèm payment nếu truyền provider.</summary>
    private static async Task AddBookingAsync(
        PhoneGrapherDbContext db,
        SeedResult seed,
        DateTimeOffset createdAt,
        BookingStatus status = BookingStatus.PendingPayment,
        int durationMinutes = 60,
        DateTimeOffset? completedAt = null,
        Guid? customerId = null,
        PaymentProvider? provider = null,
        PaymentStatus paymentStatus = PaymentStatus.Succeeded,
        decimal amount = 100_000m,
        DateTimeOffset? paidAt = null)
    {
        var booking = new Booking
        {
            CustomerId = customerId ?? seed.CustomerId,
            GrapherProfileId = seed.GrapherProfileId,
            ServicePackageId = seed.ServicePackageId,
            ScheduledAt = createdAt.AddDays(1),
            DurationMinutes = durationMinutes,
            Location = "Ho Guom",
            Status = status,
            TotalAmount = amount,
            PlatformFeeAmount = amount * 0.15m,
            GrapherPayoutAmount = amount * 0.85m,
            CreatedAt = createdAt,
            CompletedAt = completedAt
        };
        db.Bookings.Add(booking);

        if (provider.HasValue)
        {
            db.PaymentTransactions.Add(new PaymentTransaction
            {
                Booking = booking,
                Provider = provider.Value,
                Status = paymentStatus,
                Amount = amount,
                PlatformFeeAmount = amount * 0.15m,
                GrapherPayoutAmount = amount * 0.85m,
                TransactionCode = $"T{Guid.NewGuid():N}"[..20],
                PaidAt = paidAt ?? createdAt
            });
        }

        await db.SaveChangesAsync();
    }

    // ── Bốn chỗ chia cho 0 ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_KyTruocBangKhong_ChangePercentLaNull()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        // Chỉ có dữ liệu trong kỳ hiện tại, kỳ trước hoàn toàn trống.
        await AddBookingAsync(db, seed, DateTimeOffset.UtcNow.AddDays(-1), provider: PaymentProvider.VietQr);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Null(result.Kpis.Revenue.ChangePercent);
        Assert.Null(result.Kpis.ActiveUsers.ChangePercent);
        Assert.Equal(0m, result.Kpis.Revenue.Previous);
        Assert.True(result.Kpis.Revenue.Current > 0m);
    }

    [Fact]
    public async Task GetAsync_KyTruocCoDuLieu_TinhDungPhanTramThayDoi()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        // Kỳ trước 100k, kỳ này 150k → tăng 50%.
        await AddBookingAsync(db, seed, DateTimeOffset.UtcNow.AddDays(-40), provider: PaymentProvider.VietQr, amount: 100_000m);
        await AddBookingAsync(db, seed, DateTimeOffset.UtcNow.AddDays(-10), provider: PaymentProvider.VietQr, amount: 150_000m);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(150_000m, result.Kpis.Revenue.Current);
        Assert.Equal(100_000m, result.Kpis.Revenue.Previous);
        Assert.Equal(50m, result.Kpis.Revenue.ChangePercent);
    }

    [Fact]
    public async Task GetAsync_KhongCoDonNao_TiLeHoanThanhBangKhongChuKhongPhaiNaN()
    {
        using var db = TestDb.Create();
        await TestDb.SeedAsync(db);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(0m, result.Kpis.CompletionRate.Current);
        Assert.All(result.Kpis.CompletionRate.Sparkline, v => Assert.Equal(0m, v));
    }

    [Fact]
    public async Task GetAsync_KhongCoDoanhThu_MoiLatDonutBangKhongPhanTram()
    {
        using var db = TestDb.Create();
        await TestDb.SeedAsync(db);

        var result = await CreateService(db).GetAsync("30d");

        Assert.All(result.RevenueByGateway, x => Assert.Equal(0m, x.Percent));
        Assert.All(result.RevenueByGateway, x => Assert.Equal(0m, x.Value));
    }

    [Fact]
    public async Task GetAsync_ChiTieuBangKhong_TienDoBangKhongChuKhongLoi()
    {
        using var db = TestDb.Create();
        await TestDb.SeedAsync(db);
        var settings = await db.SystemSettings.FirstAsync();
        settings.QuarterlyRevenueTarget = 0m;
        settings.VerifiedGrapherTarget = 0;
        settings.CompletionRateTarget = 0m;
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync("30d");

        Assert.All(result.Goals, g => Assert.Equal(0m, g.Percent));
    }

    // ── Đúng đắn của số liệu ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_DoanhThuChiTinhGiaoDichThanhCong()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var recent = DateTimeOffset.UtcNow.AddDays(-2);
        await AddBookingAsync(db, seed, recent, provider: PaymentProvider.VietQr, paymentStatus: PaymentStatus.Succeeded, amount: 200_000m);
        await AddBookingAsync(db, seed, recent, provider: PaymentProvider.VietQr, paymentStatus: PaymentStatus.Pending, amount: 999_000m);
        await AddBookingAsync(db, seed, recent, provider: PaymentProvider.VietQr, paymentStatus: PaymentStatus.Failed, amount: 888_000m);
        await AddBookingAsync(db, seed, recent, provider: PaymentProvider.VietQr, paymentStatus: PaymentStatus.AwaitingVerification, amount: 777_000m);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(200_000m, result.Kpis.Revenue.Current);
    }

    [Fact]
    public async Task GetAsync_TongCacCongThanhToanBangTongDoanhThu()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var recent = DateTimeOffset.UtcNow.AddDays(-3);
        await AddBookingAsync(db, seed, recent, provider: PaymentProvider.VietQr, amount: 300_000m);
        await AddBookingAsync(db, seed, recent, provider: PaymentProvider.VnPay, amount: 100_000m);
        await AddBookingAsync(db, seed, recent, provider: PaymentProvider.Cod, amount: 100_000m);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(500_000m, result.RevenueByGateway.Sum(x => x.Value));
        Assert.Equal(result.Kpis.Revenue.Current, result.RevenueByGateway.Sum(x => x.Value));
        Assert.Equal(60m, result.RevenueByGateway.Single(x => x.Label == "VietQR").Percent);
        Assert.Equal(100m, result.RevenueByGateway.Sum(x => x.Percent));
    }

    [Fact]
    public async Task GetAsync_NguoiDungHoatDongDemTheoKhachPhanBiet()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var recent = DateTimeOffset.UtcNow.AddDays(-2);
        // Cùng một khách đặt ba đơn thì vẫn chỉ là một người dùng hoạt động.
        await AddBookingAsync(db, seed, recent);
        await AddBookingAsync(db, seed, recent);
        await AddBookingAsync(db, seed, recent);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(1m, result.Kpis.ActiveUsers.Current);
    }

    [Fact]
    public async Task GetAsync_PhutChupChiTinhDonDaHoanThanh()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var recent = DateTimeOffset.UtcNow.AddDays(-2);
        await AddBookingAsync(db, seed, recent, BookingStatus.Completed, durationMinutes: 90, completedAt: recent);
        await AddBookingAsync(db, seed, recent, BookingStatus.Confirmed, durationMinutes: 120);
        await AddBookingAsync(db, seed, recent, BookingStatus.Cancelled, durationMinutes: 240);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(90m, result.Kpis.ShootMinutes.Current);
    }

    [Fact]
    public async Task GetAsync_TiLeHoanThanhTinhTrenTongDonTaoTrongKy()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var recent = DateTimeOffset.UtcNow.AddDays(-2);
        await AddBookingAsync(db, seed, recent, BookingStatus.Completed, completedAt: recent);
        await AddBookingAsync(db, seed, recent, BookingStatus.Completed, completedAt: recent);
        await AddBookingAsync(db, seed, recent, BookingStatus.Cancelled);
        await AddBookingAsync(db, seed, recent, BookingStatus.PendingPayment);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(50m, result.Kpis.CompletionRate.Current);
    }

    [Fact]
    public async Task GetAsync_DuLieuNgoaiKhoangKhongDuocTinh()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        // Cách đây 100 ngày, nằm ngoài cả kỳ hiện tại lẫn kỳ trước của khoảng 30 ngày.
        await AddBookingAsync(db, seed, DateTimeOffset.UtcNow.AddDays(-100), provider: PaymentProvider.VietQr, amount: 500_000m);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(0m, result.Kpis.Revenue.Current);
        Assert.Equal(0m, result.Kpis.Revenue.Previous);
    }

    // ── Hình dạng dữ liệu trả về ─────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_MoiSparklineVaXuHuongDeuCo12Moc()
    {
        using var db = TestDb.Create();
        await TestDb.SeedAsync(db);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(12, result.RevenueTrend.Count);
        Assert.Equal(12, result.ActiveUserTrend.Count);
        Assert.Equal(12, result.Kpis.Revenue.Sparkline.Count);
        Assert.Equal(12, result.Kpis.ActiveUsers.Sparkline.Count);
        Assert.Equal(12, result.Kpis.ShootMinutes.Sparkline.Count);
        Assert.Equal(12, result.Kpis.VerifiedGraphers.Sparkline.Count);
        Assert.Equal(12, result.Kpis.CompletionRate.Sparkline.Count);
    }

    [Fact]
    public async Task GetAsync_CoCauNguoiDungDemTheoVaiTro()
    {
        using var db = TestDb.Create();
        await TestDb.SeedAsync(db);

        var result = await CreateService(db).GetAsync("30d");

        Assert.Equal(1m, result.UserComposition.Single(x => x.Label == "Khách hàng").Value);
        Assert.Equal(1m, result.UserComposition.Single(x => x.Label == "Thợ chụp").Value);
        Assert.Equal(1m, result.UserComposition.Single(x => x.Label == "Quản trị").Value);
        Assert.All(result.UserComposition, x => Assert.Equal(33.3m, x.Percent));
    }

    [Fact]
    public async Task GetAsync_PhanTramLamTronCoTheKhongKhepKin100()
    {
        using var db = TestDb.Create();
        await TestDb.SeedAsync(db);

        var result = await CreateService(db).GetAsync("30d");

        // Ba phần bằng nhau làm tròn một chữ số cho tổng 99,9 chứ không phải 100.
        // Đây là hành vi đã chấp nhận: Percent chỉ dùng làm nhãn.
        // Biểu đồ tròn ở frontend phải vẽ cung từ Value, nếu vẽ theo Percent sẽ hở một khe.
        Assert.Equal(99.9m, result.UserComposition.Sum(x => x.Percent));
    }

    [Fact]
    public async Task GetAsync_TinhTrangHeThongDocTheoCauHinhThuc()
    {
        using var db = TestDb.Create();
        await TestDb.SeedAsync(db);

        var configured = await CreateService(db,
            vietQr: new VietQrOptions { Enabled = true, BankBin = "970405", AccountNumber = "5402205457446" },
            vnPay: new VnPayOptions { TmnCode = "ABC123" },
            brevo: new BrevoOptions { Enabled = true }).GetAsync("30d");

        Assert.True(configured.Health.Items.Single(x => x.Name == "VietQR").IsHealthy);
        Assert.True(configured.Health.Items.Single(x => x.Name == "VNPay").IsHealthy);
        Assert.True(configured.Health.Items.Single(x => x.Name == "Email").IsHealthy);

        var bare = await CreateService(db).GetAsync("30d");

        Assert.False(bare.Health.Items.Single(x => x.Name == "VietQR").IsHealthy);
        Assert.Equal("Chưa cấu hình", bare.Health.Items.Single(x => x.Name == "VNPay").Status);
        Assert.Equal("Chưa cấu hình", bare.Health.Items.Single(x => x.Name == "Email").Status);
    }

    [Fact]
    public async Task GetAsync_TraVeDungKhoangDaYeuCau()
    {
        using var db = TestDb.Create();
        await TestDb.SeedAsync(db);

        var result = await CreateService(db).GetAsync("7d");

        Assert.Equal("7d", result.Range);
        Assert.Equal(TimeSpan.FromDays(7), result.ToUtc - result.FromUtc);
    }
}
