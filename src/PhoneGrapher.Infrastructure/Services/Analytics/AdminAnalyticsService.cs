using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Options;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services.Analytics;

public sealed class AdminAnalyticsService(
    PhoneGrapherDbContext dbContext,
    IOptions<VietQrOptions> vietQrOptions,
    IOptions<VnPayOptions> vnPayOptions,
    IOptions<BrevoOptions> brevoOptions,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<AdminAnalyticsService> logger) : IAdminAnalyticsService
{
    /// <summary>Bản ghi booking rút gọn, chỉ giữ trường cần cho thống kê.</summary>
    private sealed record BookingPoint(Guid CustomerId, DateTimeOffset CreatedAt, BookingStatus Status, int DurationMinutes, DateTimeOffset? CompletedAt);

    private sealed record PaymentPoint(decimal Amount, DateTimeOffset PaidAt, PaymentProvider Provider);

    public async Task<AdminAnalyticsResponse> GetAsync(string? range, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var window = AnalyticsWindow.Create(range, DateTimeOffset.UtcNow);
        var buckets = window.Buckets();

        // Tải từ đầu kỳ trước để tính được cả delta trong một lượt truy vấn.
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(x => x.CreatedAt >= window.PreviousFromUtc && x.CreatedAt < window.ToUtc)
            .Select(x => new BookingPoint(x.CustomerId, x.CreatedAt, x.Status, x.DurationMinutes, x.CompletedAt))
            .ToArrayAsync(cancellationToken);

        var payments = await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Succeeded
                && x.PaidAt != null
                && x.PaidAt >= window.PreviousFromUtc
                && x.PaidAt < window.ToUtc)
            .Select(x => new PaymentPoint(x.Amount, x.PaidAt!.Value, x.Provider))
            .ToArrayAsync(cancellationToken);

        var grapherCreatedAt = await dbContext.GrapherProfiles
            .AsNoTracking()
            .Where(x => x.IsVerified)
            .Select(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);

        var usersByRole = await dbContext.Users
            .AsNoTracking()
            .GroupBy(x => x.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToArrayAsync(cancellationToken);

        var settings = await dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? new Domain.Entities.SystemSettings();

        var kpis = BuildKpis(window, buckets, bookings, payments, grapherCreatedAt);
        var revenueTrend = buckets
            .Select(b => new TimeBucketResponse(b.Label, b.StartUtc, SumRevenue(payments, b.StartUtc, b.EndUtc)))
            .ToArray();
        var activeUserTrend = buckets
            .Select(b => new TimeBucketResponse(b.Label, b.StartUtc, CountActiveUsers(bookings, b.StartUtc, b.EndUtc)))
            .ToArray();

        var health = await BuildHealthAsync(stopwatch, cancellationToken);

        return new AdminAnalyticsResponse(
            window.Range,
            window.FromUtc,
            window.ToUtc,
            kpis,
            revenueTrend,
            activeUserTrend,
            BuildUserComposition(usersByRole.ToDictionary(x => x.Role, x => x.Count)),
            BuildRevenueByGateway(payments, window),
            BuildGoals(settings, payments, window, grapherCreatedAt.Length, kpis.CompletionRate.Current),
            health);
    }

    // ── KPI ──────────────────────────────────────────────────────────────────

    private static AnalyticsKpisResponse BuildKpis(
        AnalyticsWindow window,
        IReadOnlyList<AnalyticsBucket> buckets,
        IReadOnlyList<BookingPoint> bookings,
        IReadOnlyList<PaymentPoint> payments,
        IReadOnlyList<DateTimeOffset> verifiedGrapherCreatedAt)
    {
        var activeUsers = Metric(
            CountActiveUsers(bookings, window.FromUtc, window.ToUtc),
            CountActiveUsers(bookings, window.PreviousFromUtc, window.FromUtc),
            buckets.Select(b => CountActiveUsers(bookings, b.StartUtc, b.EndUtc)).ToArray());

        var shootMinutes = Metric(
            SumShootMinutes(bookings, window.FromUtc, window.ToUtc),
            SumShootMinutes(bookings, window.PreviousFromUtc, window.FromUtc),
            buckets.Select(b => SumShootMinutes(bookings, b.StartUtc, b.EndUtc)).ToArray());

        var revenue = Metric(
            SumRevenue(payments, window.FromUtc, window.ToUtc),
            SumRevenue(payments, window.PreviousFromUtc, window.FromUtc),
            buckets.Select(b => SumRevenue(payments, b.StartUtc, b.EndUtc)).ToArray());

        // Thợ đã duyệt là số tổng tại một thời điểm, không phải phát sinh trong kỳ.
        var verifiedNow = verifiedGrapherCreatedAt.Count;
        var verifiedAtStart = verifiedGrapherCreatedAt.Count(x => x < window.FromUtc);
        var verifiedGraphers = Metric(
            verifiedNow,
            verifiedAtStart,
            buckets.Select(b => (decimal)verifiedGrapherCreatedAt.Count(x => x < b.EndUtc)).ToArray());

        var completionRate = Metric(
            CompletionRate(bookings, window.FromUtc, window.ToUtc),
            CompletionRate(bookings, window.PreviousFromUtc, window.FromUtc),
            buckets.Select(b => CompletionRate(bookings, b.StartUtc, b.EndUtc)).ToArray());

        return new AnalyticsKpisResponse(activeUsers, shootMinutes, revenue, verifiedGraphers, completionRate);
    }

    private static MetricResponse Metric(decimal current, decimal previous, IReadOnlyList<decimal> sparkline)
        => new(current, previous, ChangePercent(current, previous), sparkline);

    /// <summary>Trả null khi kỳ trước bằng 0 — không có mẫu số, đừng bịa ra con số.</summary>
    private static decimal? ChangePercent(decimal current, decimal previous)
        => previous == 0m ? null : decimal.Round((current - previous) / previous * 100m, 1);

    private static decimal CountActiveUsers(IReadOnlyList<BookingPoint> bookings, DateTimeOffset from, DateTimeOffset to)
        => bookings
            .Where(x => x.CreatedAt >= from && x.CreatedAt < to)
            .Select(x => x.CustomerId)
            .Distinct()
            .Count();

    private static decimal SumShootMinutes(IReadOnlyList<BookingPoint> bookings, DateTimeOffset from, DateTimeOffset to)
        => bookings
            .Where(x => x.Status == BookingStatus.Completed
                && x.CompletedAt.HasValue
                && x.CompletedAt.Value >= from
                && x.CompletedAt.Value < to)
            .Sum(x => x.DurationMinutes);

    private static decimal SumRevenue(IReadOnlyList<PaymentPoint> payments, DateTimeOffset from, DateTimeOffset to)
        => payments
            .Where(x => x.PaidAt >= from && x.PaidAt < to)
            .Sum(x => x.Amount);

    /// <summary>Không có đơn nào thì trả 0, không để phép chia sinh ra NaN.</summary>
    private static decimal CompletionRate(IReadOnlyList<BookingPoint> bookings, DateTimeOffset from, DateTimeOffset to)
    {
        var inRange = bookings.Where(x => x.CreatedAt >= from && x.CreatedAt < to).ToArray();
        if (inRange.Length == 0)
        {
            return 0m;
        }

        var completed = inRange.Count(x => x.Status == BookingStatus.Completed);
        return decimal.Round((decimal)completed / inRange.Length * 100m, 1);
    }

    // ── Biểu đồ tròn ─────────────────────────────────────────────────────────

    private static IReadOnlyList<BreakdownSliceResponse> BuildUserComposition(IReadOnlyDictionary<UserRole, int> byRole)
    {
        var labels = new (UserRole Role, string Label)[]
        {
            (UserRole.Customer, "Khách hàng"),
            (UserRole.Grapher, "Thợ chụp"),
            (UserRole.Admin, "Quản trị")
        };

        var total = byRole.Values.Sum();

        return labels
            .Select(x =>
            {
                var value = byRole.TryGetValue(x.Role, out var count) ? count : 0;
                return new BreakdownSliceResponse(x.Label, value, Percent(value, total));
            })
            .ToArray();
    }

    private static IReadOnlyList<BreakdownSliceResponse> BuildRevenueByGateway(
        IReadOnlyList<PaymentPoint> payments,
        AnalyticsWindow window)
    {
        var inRange = payments.Where(x => x.PaidAt >= window.FromUtc && x.PaidAt < window.ToUtc).ToArray();
        var total = inRange.Sum(x => x.Amount);

        var labels = new (PaymentProvider Provider, string Label)[]
        {
            (PaymentProvider.VietQr, "VietQR"),
            (PaymentProvider.VnPay, "VNPay"),
            (PaymentProvider.Cod, "Tiền mặt")
        };

        return labels
            .Select(x =>
            {
                var value = inRange.Where(p => p.Provider == x.Provider).Sum(p => p.Amount);
                return new BreakdownSliceResponse(x.Label, value, Percent(value, total));
            })
            .ToArray();
    }

    /// <summary>Tổng bằng 0 thì mọi lát bằng 0 phần trăm, frontend vẽ vòng xám rỗng.</summary>
    private static decimal Percent(decimal value, decimal total)
        => total == 0m ? 0m : decimal.Round(value / total * 100m, 1);

    // ── Mục tiêu quý ─────────────────────────────────────────────────────────

    private static IReadOnlyList<GoalProgressResponse> BuildGoals(
        Domain.Entities.SystemSettings settings,
        IReadOnlyList<PaymentPoint> payments,
        AnalyticsWindow window,
        int verifiedGraphers,
        decimal completionRate)
    {
        var revenueInRange = SumRevenue(payments, window.FromUtc, window.ToUtc);

        return
        [
            new GoalProgressResponse("Doanh thu quý", revenueInRange, settings.QuarterlyRevenueTarget,
                Percent(revenueInRange, settings.QuarterlyRevenueTarget)),
            new GoalProgressResponse("Thợ đã duyệt", verifiedGraphers, settings.VerifiedGrapherTarget,
                Percent(verifiedGraphers, settings.VerifiedGrapherTarget)),
            new GoalProgressResponse("Tỉ lệ hoàn thành đơn", completionRate, settings.CompletionRateTarget,
                Percent(completionRate, settings.CompletionRateTarget))
        ];
    }

    // ── Tình trạng hệ thống ──────────────────────────────────────────────────

    private async Task<SystemHealthResponse> BuildHealthAsync(Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var databaseHealthy = false;
        try
        {
            databaseHealthy = await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không kiểm tra được kết nối cơ sở dữ liệu.");
        }

        var vietQr = vietQrOptions.Value;
        var vietQrReady = vietQr.Enabled
            && !string.IsNullOrWhiteSpace(vietQr.BankBin)
            && !string.IsNullOrWhiteSpace(vietQr.AccountNumber);

        var vnPayReady = !string.IsNullOrWhiteSpace(vnPayOptions.Value.TmnCode);
        var emailReady = brevoOptions.Value.Enabled || smtpOptions.Value.Enabled;

        var items = new[]
        {
            new HealthItemResponse("API", "Đang chạy", true),
            new HealthItemResponse("PostgreSQL", databaseHealthy ? "Kết nối OK" : "Mất kết nối", databaseHealthy),
            new HealthItemResponse("VietQR", vietQrReady ? "Đã cấu hình" : "Chưa cấu hình", vietQrReady),
            new HealthItemResponse("VNPay", vnPayReady ? "Đã cấu hình" : "Chưa cấu hình", vnPayReady),
            new HealthItemResponse("Email", emailReady ? "Đang phục vụ" : "Chưa cấu hình", emailReady)
        };

        stopwatch.Stop();
        return new SystemHealthResponse(items, stopwatch.ElapsedMilliseconds);
    }
}
