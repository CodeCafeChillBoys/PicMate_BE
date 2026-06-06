using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class AdminService(PhoneGrapherDbContext dbContext) : IAdminService
{
    public async Task<RevenueSummaryResponse> GetRevenueSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var startOfYear = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Tất cả giao dịch ĐÃ THANH TOÁN THÀNH CÔNG (bao gồm cả đang giữ escrow)
        // → Dùng để tính tổng doanh thu thu được và phí platform
        var succeededPayments = await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Succeeded)
            .ToArrayAsync(cancellationToken);

        // Giao dịch ĐÃ GIẢI PHÓNG ESCROW → Thực sự đã chuyển tiền cho Grapher
        var releasedPayments = succeededPayments
            .Where(x => x.EscrowStatus == EscrowStatus.Released)
            .ToArray();

        var completedBookings = await dbContext.Bookings
            .AsNoTracking()
            .CountAsync(x => x.Status == BookingStatus.Completed, cancellationToken);

        var pendingKyc = await dbContext.GrapherProfiles
            .AsNoTracking()
            .CountAsync(x => x.KycStatus == KycStatus.Pending, cancellationToken);

        var totalUsers = await dbContext.Users
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalGraphers = await dbContext.Users
            .AsNoTracking()
            .CountAsync(x => x.Role == UserRole.Grapher, cancellationToken);

        var bookingsThisMonth = await dbContext.Bookings
            .AsNoTracking()
            .CountAsync(x => x.CreatedAt >= startOfMonth, cancellationToken);

        // Phí platform thu được tháng này từ tất cả giao dịch thành công
        var revenueThisMonth = succeededPayments
            .Where(x => x.PaidAt.HasValue && x.PaidAt.Value >= startOfMonth)
            .Sum(x => x.PlatformFeeAmount);

        // Doanh thu thực theo từng tháng trong năm hiện tại
        var paymentsThisYear = succeededPayments
            .Where(x => x.PaidAt.HasValue && x.PaidAt.Value >= startOfYear)
            .ToArray();

        var vietnameseMonths = new[] { "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12" };

        var monthlyRevenue = Enumerable.Range(1, 12).Select(month =>
        {
            var monthPayments = paymentsThisYear
                .Where(x => x.PaidAt!.Value.Month == month)
                .ToArray();

            return new MonthlyRevenueItem(
                month,
                vietnameseMonths[month - 1],
                monthPayments.Sum(x => x.Amount),
                monthPayments.Sum(x => x.PlatformFeeAmount),
                monthPayments.Length);
        }).ToArray();

        return new RevenueSummaryResponse(
            GrossRevenue: succeededPayments.Sum(x => x.Amount),           // Tổng tiền đã thu
            PlatformRevenue: succeededPayments.Sum(x => x.PlatformFeeAmount), // Phí platform đã thu
            GrapherPayouts: releasedPayments.Sum(x => x.GrapherPayoutAmount), // Tiền đã chuyển cho Grapher
            CompletedBookings: completedBookings,
            PendingKycCount: pendingKyc,
            TotalUsers: totalUsers,
            TotalGraphers: totalGraphers,
            BookingsThisMonth: bookingsThisMonth,
            RevenueThisMonth: revenueThisMonth,
            MonthlyRevenue: monthlyRevenue);
    }


    public async Task<IReadOnlyList<AdminUserResponse>> GetAllUsersAsync(
        string? search,
        string? role,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Include(u => u.CustomerBookings)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(role) && role != "all")
        {
            if (Enum.TryParse<UserRole>(role, ignoreCase: true, out var roleEnum))
                query = query.Where(u => u.Role == roleEnum);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return users.Select(u => new AdminUserResponse(
            u.Id,
            u.FullName,
            u.Email,
            u.Role.ToString(),
            u.CreatedAt.ToString("dd/MM/yyyy"),
            u.CustomerBookings.Count,
            u.IsActive
        )).ToArray();
    }

    public async Task<AdminUserResponse> ToggleUserStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(u => u.CustomerBookings)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminUserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Role.ToString(),
            user.CreatedAt.ToString("dd/MM/yyyy"),
            user.CustomerBookings.Count,
            user.IsActive);
    }

    public async Task<IReadOnlyList<AdminPendingGrapherResponse>> GetPendingGraphersAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await dbContext.GrapherProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.StyleTags).ThenInclude(st => st.StyleTag)
            .Include(p => p.PortfolioItems)
            .Where(p => p.KycStatus == KycStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return profiles.Select(p => new AdminPendingGrapherResponse(
            p.Id,
            p.User.FullName,
            p.User.AvatarUrl,
            p.StyleTags.Select(st => st.StyleTag.Name).ToArray(),
            p.PortfolioItems.Count,
            p.Location,
            p.CreatedAt.ToString("yyyy-MM-dd")
        )).ToArray();
    }

    public async Task<IReadOnlyList<AdminBookingResponse>> GetAllBookingsAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.GrapherProfile).ThenInclude(gp => gp.User)
            .Include(b => b.ServicePackage)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var statusEnum))
                query = query.Where(b => b.Status == statusEnum);
        }

        var bookings = await query
            .OrderByDescending(b => b.CreatedAt)
            .Take(100)
            .ToArrayAsync(cancellationToken);

        return bookings.Select(b => new AdminBookingResponse(
            b.Id,
            b.GrapherProfile.User.FullName,
            b.GrapherProfile.User.AvatarUrl,
            b.ServicePackage.Name,
            b.ScheduledAt.ToString("dd/MM/yyyy"),
            b.Location,
            b.TotalAmount,
            b.Status.ToString()
        )).ToArray();
    }

    public async Task<IReadOnlyList<AdminActivityResponse>> GetRecentActivitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var activities = new List<AdminActivityResponse>();

        // Lấy 5 bookings mới nhất
        var recentBookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.GrapherProfile).ThenInclude(gp => gp.User)
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .ToArrayAsync(cancellationToken);

        foreach (var b in recentBookings)
        {
            activities.Add(new AdminActivityResponse(
                $"booking-{b.Id}",
                "📸",
                $"{b.Customer.FullName} đặt lịch với {b.GrapherProfile.User.FullName}",
                FormatTimeAgo(b.CreatedAt)
            ));
        }

        // Lấy 3 người dùng đăng ký mới nhất
        var recentUsers = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Customer)
            .OrderByDescending(u => u.CreatedAt)
            .Take(3)
            .ToArrayAsync(cancellationToken);

        foreach (var u in recentUsers)
        {
            activities.Add(new AdminActivityResponse(
                $"user-{u.Id}",
                "👤",
                $"{u.FullName} đăng ký tài khoản mới",
                FormatTimeAgo(u.CreatedAt)
            ));
        }

        // Lấy 2 graphers đăng ký KYC mới nhất
        var recentKyc = await dbContext.GrapherProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.KycStatus == KycStatus.Pending)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(2)
            .ToArrayAsync(cancellationToken);

        foreach (var p in recentKyc)
        {
            activities.Add(new AdminActivityResponse(
                $"kyc-{p.Id}",
                "🆔",
                $"{p.User.FullName} gửi yêu cầu xác minh KYC",
                FormatTimeAgo(p.UpdatedAt ?? p.CreatedAt)
            ));
        }

        return activities
            .OrderByDescending(a => a.Time)
            .Take(10)
            .ToArray();
    }

    private static string FormatTimeAgo(DateTimeOffset time)
    {
        var diff = DateTimeOffset.UtcNow - time;
        if (diff.TotalMinutes < 1) return "vừa xong";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} phút trước";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} giờ trước";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} ngày trước";
        return time.ToString("dd/MM/yyyy");
    }
}
