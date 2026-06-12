using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class AdminService(PhoneGrapherDbContext dbContext) : IAdminService
{
    // ── Revenue / Stats ──────────────────────────────────────────────────────

    public async Task<RevenueSummaryResponse> GetRevenueSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var startOfYear = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Tất cả giao dịch ĐÃ THANH TOÁN THÀNH CÔNG
        var succeededPayments = await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Succeeded)
            .ToArrayAsync(cancellationToken);

        // Giao dịch ĐÃ GIẢI PHÓNG ESCROW → đã chuyển tiền cho Grapher
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

        var revenueThisMonth = succeededPayments
            .Where(x => x.PaidAt.HasValue && x.PaidAt.Value >= startOfMonth)
            .Sum(x => x.PlatformFeeAmount);

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
            GrossRevenue: succeededPayments.Sum(x => x.Amount),
            PlatformRevenue: succeededPayments.Sum(x => x.PlatformFeeAmount),
            GrapherPayouts: releasedPayments.Sum(x => x.GrapherPayoutAmount),
            CompletedBookings: completedBookings,
            PendingKycCount: pendingKyc,
            TotalUsers: totalUsers,
            TotalGraphers: totalGraphers,
            BookingsThisMonth: bookingsThisMonth,
            RevenueThisMonth: revenueThisMonth,
            MonthlyRevenue: monthlyRevenue);
    }

    // ── Users ────────────────────────────────────────────────────────────────

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

    public async Task<AdminUserResponse> ToggleUserStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
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

    // ── Graphers – Pending KYC ───────────────────────────────────────────────

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

    // ── Graphers – Active (Admin view) ───────────────────────────────────────

    public async Task<IReadOnlyList<AdminActiveGrapherResponse>> GetActiveGraphersAsync(
        CancellationToken cancellationToken = default)
    {
        var profiles = await dbContext.GrapherProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.Bookings)
            .Where(p => p.KycStatus == KycStatus.Approved)
            .OrderByDescending(p => p.AverageRating)
            .Take(100)
            .ToArrayAsync(cancellationToken);

        return profiles.Select(p => new AdminActiveGrapherResponse(
            p.Id,
            p.UserId,
            p.User.FullName,
            p.User.AvatarUrl,
            p.Location,
            p.AverageRating,
            p.ReviewCount,
            p.IsOnline,
            p.IsVerified,
            p.User.IsActive,
            p.KycStatus.ToString(),
            p.Bookings.Count
        )).ToArray();
    }

    public async Task<AdminActiveGrapherResponse> ToggleGrapherStatusAsync(
        Guid grapherProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .Include(p => p.User)
            .Include(p => p.Bookings)
            .FirstOrDefaultAsync(p => p.Id == grapherProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        // Toggle User.IsActive để khóa / mở tài khoản grapher
        profile.User.IsActive = !profile.User.IsActive;
        profile.User.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminActiveGrapherResponse(
            profile.Id,
            profile.UserId,
            profile.User.FullName,
            profile.User.AvatarUrl,
            profile.Location,
            profile.AverageRating,
            profile.ReviewCount,
            profile.IsOnline,
            profile.IsVerified,
            profile.User.IsActive,
            profile.KycStatus.ToString(),
            profile.Bookings.Count);
    }

    // ── Bookings ─────────────────────────────────────────────────────────────

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

    // ── Activities ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AdminActivityResponse>> GetRecentActivitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var activities = new List<AdminActivityResponse>();

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

    // ── Disputes ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AdminDisputeResponse>> GetDisputesAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.Reporter)
            .Include(d => d.Respondent)
            .Include(d => d.Booking)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (Enum.TryParse<DisputeStatus>(status, ignoreCase: true, out var statusEnum))
                query = query.Where(d => d.Status == statusEnum);
        }

        var disputes = await query
            .OrderByDescending(d => d.Priority)
            .ThenByDescending(d => d.CreatedAt)
            .Take(50)
            .ToArrayAsync(cancellationToken);

        return disputes.Select(d => new AdminDisputeResponse(
            d.Id,
            d.BookingId,
            d.Reporter.FullName,
            d.Reporter.AvatarUrl,
            d.Respondent.FullName,
            d.Respondent.AvatarUrl,
            d.Reason,
            d.Status.ToString(),
            d.Priority.ToString(),
            d.AdminNote,
            d.Resolution,
            d.Booking.TotalAmount,
            d.CreatedAt.ToString("dd/MM/yyyy"),
            d.ResolvedAt?.ToString("dd/MM/yyyy")
        )).ToArray();
    }

    public async Task<AdminDisputeResponse> ResolveDisputeAsync(
        Guid disputeId,
        ResolveDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        var dispute = await dbContext.Disputes
            .Include(d => d.Reporter)
            .Include(d => d.Respondent)
            .Include(d => d.Booking)
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken)
            ?? throw new InvalidOperationException("Dispute not found.");

        if (dispute.Status != DisputeStatus.Pending)
            throw new InvalidOperationException("Dispute has already been resolved.");

        dispute.Status = request.Action == "resolved" ? DisputeStatus.Resolved : DisputeStatus.Closed;
        dispute.Resolution = request.Action;
        dispute.AdminNote = request.AdminNote;
        dispute.ResolvedAt = DateTimeOffset.UtcNow;
        dispute.UpdatedAt = DateTimeOffset.UtcNow;

        // Nếu action là hoàn tiền → cập nhật payment + booking
        if (request.Action == "refund")
        {
            var payment = await dbContext.PaymentTransactions
                .FirstOrDefaultAsync(p => p.BookingId == dispute.BookingId, cancellationToken);
            if (payment is not null && payment.Status == PaymentStatus.Succeeded)
            {
                payment.Status = PaymentStatus.Refunded;
                payment.EscrowStatus = EscrowStatus.Refunded;
                payment.UpdatedAt = DateTimeOffset.UtcNow;
            }

            dispute.Booking.Status = BookingStatus.Cancelled;
            dispute.Booking.CancellationReason = $"Admin hoàn tiền sau tranh chấp: {request.AdminNote}";
            dispute.Booking.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminDisputeResponse(
            dispute.Id,
            dispute.BookingId,
            dispute.Reporter.FullName,
            dispute.Reporter.AvatarUrl,
            dispute.Respondent.FullName,
            dispute.Respondent.AvatarUrl,
            dispute.Reason,
            dispute.Status.ToString(),
            dispute.Priority.ToString(),
            dispute.AdminNote,
            dispute.Resolution,
            dispute.Booking.TotalAmount,
            dispute.CreatedAt.ToString("dd/MM/yyyy"),
            dispute.ResolvedAt?.ToString("dd/MM/yyyy"));
    }

    // ── System Settings ───────────────────────────────────────────────────────

    public async Task<SystemSettingsResponse> GetSystemSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        // Chưa có row → trả giá trị mặc định
        if (settings is null)
        {
            return new SystemSettingsResponse(
                PlatformFeePercent: 15m,
                MinWithdrawalAmount: 200000m,
                MomoEnabled: true,
                VnPayEnabled: true,
                ZaloPayEnabled: false,
                EmailNotifyNewBooking: true,
                EmailNotifyDispute: true,
                MaintenanceMode: false);
        }

        return ToSettingsResponse(settings);
    }

    public async Task<SystemSettingsResponse> UpdateSystemSettingsAsync(
        UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.SystemSettings
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            // Tạo mới singleton row
            settings = new Domain.Entities.SystemSettings();
            dbContext.SystemSettings.Add(settings);
        }

        settings.PlatformFeePercent = request.PlatformFeePercent;
        settings.MinWithdrawalAmount = request.MinWithdrawalAmount;
        settings.MomoEnabled = request.MomoEnabled;
        settings.VnPayEnabled = request.VnPayEnabled;
        settings.ZaloPayEnabled = request.ZaloPayEnabled;
        settings.EmailNotifyNewBooking = request.EmailNotifyNewBooking;
        settings.EmailNotifyDispute = request.EmailNotifyDispute;
        settings.MaintenanceMode = request.MaintenanceMode;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToSettingsResponse(settings);
    }

    // ── Detail Views ─────────────────────────────────────────────────────────

    public async Task<AdminUserDetailResponse> GetUserDetailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(u => u.CustomerBookings).ThenInclude(b => b.GrapherProfile).ThenInclude(gp => gp.User)
            .Include(u => u.CustomerBookings).ThenInclude(b => b.ServicePackage)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var recentBookings = user.CustomerBookings
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .Select(b => new AdminBookingResponse(
                b.Id,
                b.GrapherProfile.User.FullName,
                b.GrapherProfile.User.AvatarUrl,
                b.ServicePackage.Name,
                b.ScheduledAt.ToString("dd/MM/yyyy HH:mm"),
                b.Location,
                b.TotalAmount,
                b.Status.ToString()
            )).ToArray();

        return new AdminUserDetailResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Role.ToString(),
            user.CreatedAt.ToString("dd/MM/yyyy"),
            user.CustomerBookings.Count,
            user.IsActive,
            recentBookings);
    }

    public async Task<AdminGrapherDetailResponse> GetGrapherDetailAsync(
        Guid grapherProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.StyleTags).ThenInclude(st => st.StyleTag)
            .Include(p => p.PortfolioItems)
            .Include(p => p.ServicePackages)
            .Include(p => p.Bookings)
            .FirstOrDefaultAsync(p => p.Id == grapherProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        var completedBookingsIds = profile.Bookings
            .Where(b => b.Status == BookingStatus.Completed)
            .Select(b => b.Id)
            .ToArray();

        var payouts = await dbContext.PaymentTransactions
            .AsNoTracking()
            .Where(pt => completedBookingsIds.Contains(pt.BookingId) && pt.Status == PaymentStatus.Succeeded)
            .SumAsync(pt => pt.GrapherPayoutAmount, cancellationToken);

        return new AdminGrapherDetailResponse(
            profile.Id,
            profile.UserId,
            profile.User.FullName,
            profile.User.AvatarUrl,
            profile.Location,
            profile.AverageRating,
            profile.ReviewCount,
            profile.IsOnline,
            profile.IsVerified,
            profile.User.IsActive,
            profile.KycStatus.ToString(),
            profile.Bookings.Count,
            payouts,
            profile.Bio,
            profile.StyleTags.Select(st => st.StyleTag.Name).ToArray(),
            profile.PortfolioItems.OrderBy(pi => pi.DisplayOrder).Select(pi => pi.ImageUrl).ToArray(),
            profile.ServicePackages.Select(sp => new ServicePackageResponse(
                sp.Id, sp.Name, sp.Description, sp.Price, sp.DurationMinutes)).ToArray(),
            profile.User.CreatedAt.ToString("dd/MM/yyyy")
        );
    }

    public async Task<AdminBookingDetailResponse> GetBookingDetailAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.GrapherProfile).ThenInclude(gp => gp.User)
            .Include(b => b.ServicePackage)
            .Include(b => b.PaymentTransaction)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new InvalidOperationException("Booking not found.");

        var payment = booking.PaymentTransaction;
        PaymentTransactionResponse? paymentResponse = null;
        if (payment != null)
        {
            paymentResponse = new PaymentTransactionResponse(
                payment.Id,
                payment.Provider.ToString(),
                payment.Status.ToString(),
                payment.EscrowStatus.ToString(),
                payment.TransactionCode,
                payment.ProviderTransactionId,
                payment.Amount,
                payment.PaidAt,
                payment.ReleasedAt);
        }

        return new AdminBookingDetailResponse(
            booking.Id,
            booking.GrapherProfileId,
            booking.GrapherProfile.User.FullName,
            booking.CustomerId,
            booking.Customer.FullName,
            booking.ServicePackage.Name,
            booking.ScheduledAt,
            booking.ServicePackage.DurationMinutes,
            booking.Location,
            booking.Note,
            booking.Status.ToString(),
            booking.TotalAmount,
            booking.PlatformFeeAmount,
            booking.GrapherPayoutAmount,
            booking.CreatedAt,
            booking.CancellationReason,
            paymentResponse);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SystemSettingsResponse ToSettingsResponse(Domain.Entities.SystemSettings s) =>
        new(s.PlatformFeePercent, s.MinWithdrawalAmount, s.MomoEnabled, s.VnPayEnabled,
            s.ZaloPayEnabled, s.EmailNotifyNewBooking, s.EmailNotifyDispute, s.MaintenanceMode);

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
