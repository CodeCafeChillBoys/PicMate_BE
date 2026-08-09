using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class AdminService : IAdminService
{
    private readonly PhoneGrapherDbContext dbContext;
    private readonly IConfiguration configuration;

    public AdminService(PhoneGrapherDbContext dbContext, IConfiguration configuration)
    {
        this.dbContext = dbContext;
        this.configuration = configuration;
    }

    public AdminService(PhoneGrapherDbContext dbContext) : this(dbContext, null!)
    {
    }
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

    public async Task<AdminPendingGrapherDetailResponse> GetPendingGrapherDetailAsync(
        Guid grapherProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.StyleTags).ThenInclude(st => st.StyleTag)
            .Include(p => p.PortfolioItems)
            .FirstOrDefaultAsync(p => p.Id == grapherProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        string[] externalLinks = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(profile.ExternalLinks))
        {
            try { externalLinks = System.Text.Json.JsonSerializer.Deserialize<string[]>(profile.ExternalLinks) ?? Array.Empty<string>(); }
            catch { /* ignore parse errors */ }
        }

        return new AdminPendingGrapherDetailResponse(
            profile.Id,
            profile.UserId,
            profile.User.FullName,
            profile.User.Email,
            profile.User.PhoneNumber,
            profile.User.AvatarUrl,
            profile.Bio,
            profile.Location,
            profile.ExperienceYears,
            profile.Specialization,
            profile.CvFileUrl,
            externalLinks,
            profile.StyleTags.Select(st => st.StyleTag.Name).ToArray(),
            profile.PortfolioItems.OrderBy(pi => pi.DisplayOrder).Select(pi => pi.ImageUrl).ToArray(),
            profile.CreatedAt.ToString("yyyy-MM-dd"),
            profile.KycRejectReason
        );
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
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.GrapherProfile).ThenInclude(gp => gp.User)
            .Include(b => b.ServicePackage)
            .Include(b => b.PaymentTransaction)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var statusEnum))
                query = query.Where(b => b.Status == statusEnum);
        }

        // Lọc theo ngày tạo đơn. Mốc "đến" đã được controller đẩy sang đầu ngày kế tiếp
        // nên ở đây dùng so sánh nhỏ hơn để bao trọn ngày người dùng chọn.
        if (fromUtc.HasValue)
        {
            query = query.Where(b => b.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(b => b.CreatedAt < toUtc.Value);
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
            b.Status.ToString(),
            b.Customer.FullName,
            b.PaymentTransaction?.Id,
            b.PaymentTransaction?.Provider.ToString(),
            b.PaymentTransaction?.Status.ToString(),
            b.CreatedAt
        )).ToArray();
    }

    // ── Thao tác của admin lên đơn hàng ──────────────────────────────────────

    public async Task<AdminBookingDetailResponse> ForceCompleteBookingAsync(
        Guid bookingId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var booking = await LoadBookingForActionAsync(bookingId, cancellationToken);

        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled)
        {
            throw new InvalidOperationException("Đơn đã hoàn thành hoặc đã huỷ.");
        }

        if (booking.Status is not (BookingStatus.PendingConfirmation or BookingStatus.Confirmed or BookingStatus.InProgress))
        {
            throw new InvalidOperationException("Đơn chưa tới bước có thể hoàn thành.");
        }

        var payment = booking.PaymentTransaction
            ?? throw new InvalidOperationException("Đơn này chưa có giao dịch thanh toán.");

        // Admin bỏ qua được kiểm tra "phải là thợ được giao", nhưng không bỏ qua luật tiền:
        // không đóng đơn khi chưa thu được tiền, trừ đơn trả tiền mặt.
        var isCod = payment.Provider == PaymentProvider.Cod;
        if (!isCod && (payment.Status != PaymentStatus.Succeeded || payment.EscrowStatus != EscrowStatus.Held))
        {
            throw new InvalidOperationException("Chưa thu được tiền của đơn này nên không thể hoàn thành.");
        }

        booking.Status = BookingStatus.Completed;
        booking.CompletedAt = DateTimeOffset.UtcNow;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        if (isCod)
        {
            payment.Status = PaymentStatus.Succeeded;
            payment.PaidAt ??= DateTimeOffset.UtcNow;
        }
        else
        {
            payment.EscrowStatus = EscrowStatus.Released;
            payment.ReleasedAt = DateTimeOffset.UtcNow;
        }

        payment.VerifiedByUserId = adminUserId;
        payment.VerifiedAt = DateTimeOffset.UtcNow;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToBookingDetailResponse(booking);
    }

    public async Task<AdminBookingDetailResponse> RefundBookingAsync(
        Guid bookingId,
        Guid adminUserId,
        RefundBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var booking = await LoadBookingForActionAsync(bookingId, cancellationToken);

        var payment = booking.PaymentTransaction
            ?? throw new InvalidOperationException("Đơn này chưa có giao dịch thanh toán.");

        if (payment.Status == PaymentStatus.Refunded)
        {
            throw new InvalidOperationException("Đơn này đã được đánh dấu hoàn tiền.");
        }

        if (payment.Status != PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException("Chỉ hoàn tiền được đơn đã thanh toán thành công.");
        }

        payment.Status = PaymentStatus.Refunded;
        payment.EscrowStatus = EscrowStatus.Refunded;
        payment.VerifiedByUserId = adminUserId;
        payment.VerifiedAt = DateTimeOffset.UtcNow;
        payment.VerificationNote = string.IsNullOrWhiteSpace(request.Note)
            ? "Admin đánh dấu đã hoàn tiền."
            : request.Note.Trim();
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        // Hoàn tiền cho đơn chưa xong nghĩa là buổi chụp không diễn ra nữa nên huỷ luôn.
        // Đơn đã hoàn thành thì giữ nguyên trạng thái: đó là hoàn tiền thiện chí sau khi đã chụp.
        if (booking.Status is not (BookingStatus.Completed or BookingStatus.Cancelled))
        {
            booking.Status = BookingStatus.Cancelled;
            booking.CancellationReason = $"Admin hoàn tiền: {payment.VerificationNote}";
            booking.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToBookingDetailResponse(booking);
    }

    private async Task<Domain.Entities.Booking> LoadBookingForActionAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Bookings
            .Include(b => b.Customer)
            .Include(b => b.GrapherProfile).ThenInclude(gp => gp.User)
            .Include(b => b.ServicePackage)
            .Include(b => b.PaymentTransaction)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");
    }

    // ── Activities ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AdminActivityResponse>> GetRecentActivitiesAsync(
        CancellationToken cancellationToken = default)
    {
        // Giữ mốc thời gian thật bên cạnh bản ghi để sắp xếp cho đúng.
        // Trước đây danh sách được sắp theo chuỗi đã định dạng ("2 giờ trước", "14/06/2026"),
        // so sánh chuỗi như vậy cho ra thứ tự vô nghĩa dù tiêu đề ghi "mới nhất trước".
        var activities = new List<(DateTimeOffset OccurredAt, AdminActivityResponse Item)>();

        var recentBookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.GrapherProfile).ThenInclude(gp => gp.User)
            .OrderByDescending(b => b.CreatedAt)
            .Take(12)
            .ToArrayAsync(cancellationToken);

        foreach (var b in recentBookings)
        {
            activities.Add((b.CreatedAt, new AdminActivityResponse(
                $"booking-{b.Id}",
                "📸",
                $"{b.Customer.FullName} đặt lịch với {b.GrapherProfile.User.FullName}",
                FormatTimeAgo(b.CreatedAt))));
        }

        var recentUsers = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Customer)
            .OrderByDescending(u => u.CreatedAt)
            .Take(6)
            .ToArrayAsync(cancellationToken);

        foreach (var u in recentUsers)
        {
            activities.Add((u.CreatedAt, new AdminActivityResponse(
                $"user-{u.Id}",
                "👤",
                $"{u.FullName} đăng ký tài khoản mới",
                FormatTimeAgo(u.CreatedAt))));
        }

        var recentKyc = await dbContext.GrapherProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.KycStatus == KycStatus.Pending)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(4)
            .ToArrayAsync(cancellationToken);

        foreach (var p in recentKyc)
        {
            var occurredAt = p.UpdatedAt ?? p.CreatedAt;
            activities.Add((occurredAt, new AdminActivityResponse(
                $"kyc-{p.Id}",
                "🆔",
                $"{p.User.FullName} gửi yêu cầu xác minh KYC",
                FormatTimeAgo(occurredAt))));
        }

        return activities
            .OrderByDescending(x => x.OccurredAt)
            .Take(14)
            .Select(x => x.Item)
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

        return disputes.Select(d => {
            string[] evidenceUrls = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(d.EvidenceImageUrls))
            {
                try { evidenceUrls = System.Text.Json.JsonSerializer.Deserialize<string[]>(d.EvidenceImageUrls) ?? Array.Empty<string>(); }
                catch { }
            }
            return new AdminDisputeResponse(
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
                d.ResolvedAt?.ToString("dd/MM/yyyy"),
                evidenceUrls
            );
        }).ToArray();
    }

    public async Task<AdminDisputeResponse> ResolveDisputeAsync(
        Guid disputeId,
        ResolveDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        var dispute = await dbContext.Disputes
            .Include(d => d.Reporter)
            .Include(d => d.Respondent)
            .Include(d => d.Booking).ThenInclude(b => b.GrapherProfile)
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken)
            ?? throw new InvalidOperationException("Dispute not found.");

        if (dispute.Status != DisputeStatus.Pending)
            throw new InvalidOperationException("Dispute has already been resolved.");

        dispute.Status = request.Action == "resolved" ? DisputeStatus.Resolved : DisputeStatus.Closed;
        dispute.Resolution = request.Action;
        dispute.AdminNote = request.AdminNote;
        dispute.ResolvedAt = DateTimeOffset.UtcNow;
        dispute.UpdatedAt = DateTimeOffset.UtcNow;

        var booking = dispute.Booking;
        var payment = await dbContext.PaymentTransactions
            .FirstOrDefaultAsync(p => p.BookingId == booking.Id, cancellationToken);

        if (payment is not null)
        {
            var isCod = payment.Provider == PaymentProvider.Cod;

            if (request.Action == "refund")
            {
                // Hoàn tiền 100% cho khách
                if (booking.Status == BookingStatus.Completed)
                {
                    // Nếu đơn đã xong (thợ đã nhận tiền), khấu trừ lại tiền từ ví thợ
                    if (!isCod)
                    {
                        booking.GrapherProfile.Balance -= payment.GrapherPayoutAmount;
                        payment.Status = PaymentStatus.Refunded;
                        payment.EscrowStatus = EscrowStatus.Refunded;
                    }
                    else
                    {
                        booking.GrapherProfile.Balance += payment.PlatformFeeAmount; // trả lại phí platform đã trừ
                        payment.Status = PaymentStatus.Refunded;
                    }
                }
                else
                {
                    // Đơn chưa xong, hủy và hoàn tiền ký quỹ
                    if (!isCod)
                    {
                        payment.Status = PaymentStatus.Refunded;
                        payment.EscrowStatus = EscrowStatus.Refunded;
                    }
                    else
                    {
                        payment.Status = PaymentStatus.Failed;
                    }
                }

                booking.Status = BookingStatus.Cancelled;
                booking.CancellationReason = $"Admin hoàn tiền 100% sau tranh chấp: {request.AdminNote}";
                booking.UpdatedAt = DateTimeOffset.UtcNow;
                payment.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else if (request.Action == "resolved")
            {
                // Giải ngân 100% cho thợ (Bác bỏ khiếu nại)
                if (booking.Status != BookingStatus.Completed)
                {
                    // Đơn chưa hoàn thành → tiến hành hoàn thành đơn và giải ngân cho thợ
                    booking.Status = BookingStatus.Completed;
                    booking.CompletedAt = DateTimeOffset.UtcNow;
                    booking.UpdatedAt = DateTimeOffset.UtcNow;

                    if (isCod)
                    {
                        payment.Status = PaymentStatus.Succeeded;
                        payment.PaidAt = DateTimeOffset.UtcNow;
                        booking.GrapherProfile.Balance -= payment.PlatformFeeAmount;
                    }
                    else
                    {
                        payment.Status = PaymentStatus.Succeeded;
                        payment.EscrowStatus = EscrowStatus.Released;
                        payment.ReleasedAt = DateTimeOffset.UtcNow;
                        booking.GrapherProfile.Balance += payment.GrapherPayoutAmount;
                    }
                    payment.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
            else if (request.Action == "split")
            {
                // Chia tiền (Kéo thanh trượt)
                var refundPercent = request.RefundPercent ?? 50;
                if (refundPercent < 0) refundPercent = 0;
                if (refundPercent > 100) refundPercent = 100;

                decimal refundRatio = (decimal)refundPercent / 100m;
                decimal releaseRatio = 1m - refundRatio;

                if (booking.Status == BookingStatus.Completed)
                {
                    // Đơn đã xong (thợ đã nhận 100% payout). Khấu trừ phần bồi hoàn cho khách từ ví thợ.
                    if (!isCod)
                    {
                        decimal clawBackAmount = payment.GrapherPayoutAmount * refundRatio;
                        booking.GrapherProfile.Balance -= clawBackAmount;

                        payment.Status = PaymentStatus.Refunded; // Đánh dấu hoàn một phần
                        payment.EscrowStatus = EscrowStatus.Refunded;
                    }
                    else
                    {
                        // COD: hoàn lại phần phí platform tương ứng
                        decimal platformFeeReturn = payment.PlatformFeeAmount * refundRatio;
                        booking.GrapherProfile.Balance += platformFeeReturn;
                        payment.Status = PaymentStatus.Refunded;
                    }
                }
                else
                {
                    // Đơn chưa xong. Giải ngân phần releaseRatio cho thợ, phần refundRatio hoàn khách.
                    if (!isCod)
                    {
                        decimal releaseAmount = payment.GrapherPayoutAmount * releaseRatio;
                        booking.GrapherProfile.Balance += releaseAmount;

                        payment.Status = PaymentStatus.Refunded;
                        payment.EscrowStatus = EscrowStatus.Released;
                        payment.ReleasedAt = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        // COD: tính phí platform cho phần thợ nhận
                        decimal actualPlatformFee = payment.PlatformFeeAmount * releaseRatio;
                        booking.GrapherProfile.Balance -= actualPlatformFee;
                        payment.Status = PaymentStatus.Succeeded;
                    }
                }

                booking.Status = BookingStatus.Cancelled;
                booking.CancellationReason = $"Admin phân chia ký quỹ (Hoàn khách {refundPercent}%, Giải ngân thợ {100 - refundPercent}%): {request.AdminNote}";
                booking.UpdatedAt = DateTimeOffset.UtcNow;
                payment.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        string[] evidenceUrls = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(dispute.EvidenceImageUrls))
        {
            try { evidenceUrls = System.Text.Json.JsonSerializer.Deserialize<string[]>(dispute.EvidenceImageUrls) ?? Array.Empty<string>(); }
            catch { }
        }

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
            booking.TotalAmount,
            dispute.CreatedAt.ToString("dd/MM/yyyy"),
            dispute.ResolvedAt?.ToString("dd/MM/yyyy"),
            evidenceUrls);
    }

    public async Task<IReadOnlyList<ChatMessageResponse>> GetDisputeChatLogAsync(
        Guid disputeId,
        CancellationToken cancellationToken = default)
    {
        var dispute = await dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.Booking).ThenInclude(b => b.GrapherProfile)
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken)
            ?? throw new KeyNotFoundException("Dispute not found.");

        var customerId = dispute.Booking.CustomerId;
        var grapherUserId = dispute.Booking.GrapherProfile.UserId;

        // Lấy tất cả tin nhắn chat qua lại giữa customer và thợ chụp
        var messages = await dbContext.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => (m.SenderId == customerId && m.ReceiverId == grapherUserId) ||
                        (m.SenderId == grapherUserId && m.ReceiverId == customerId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return messages.Select(m => new ChatMessageResponse(
            m.Id,
            m.SenderId,
            m.Sender.FullName,
            m.Content,
            m.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
            m.SenderId == customerId
        )).ToArray();
    }

    public async Task<AdminDisputeAiAnalysisResponse> AnalyzeDisputeWithAiAsync(
        Guid disputeId,
        CancellationToken cancellationToken = default)
    {
        var dispute = await dbContext.Disputes
            .AsNoTracking()
            .Include(d => d.Reporter)
            .Include(d => d.Respondent)
            .Include(d => d.Booking).ThenInclude(b => b.GrapherProfile)
            .FirstOrDefaultAsync(d => d.Id == disputeId, cancellationToken)
            ?? throw new KeyNotFoundException("Dispute not found.");

        var chatLog = await GetDisputeChatLogAsync(disputeId, cancellationToken);

        // Xây dựng Chat Log Context dạng Text gửi lên cho Gemini
        var chatBuilder = new StringBuilder();
        chatBuilder.AppendLine("LỊCH SỬ CHAT GIỮA KHÁCH HÀNG VÀ THỢ CHỤP:");
        foreach (var msg in chatLog)
        {
            var roleStr = msg.IsFromCustomer ? "KHÁCH HÀNG" : "THỢ CHỤP";
            chatBuilder.AppendLine($"[{msg.CreatedAt}] {roleStr} ({msg.SenderName}): {msg.Content}");
        }

        var apiKey = configuration?["GeminiSettings:ApiKey"] ?? string.Empty;
        var modelName = configuration?["GeminiSettings:ModelName"] ?? "gemini-2.5-flash";

        if (string.IsNullOrEmpty(apiKey))
        {
            return new AdminDisputeAiAnalysisResponse(
                "Không thể kết nối dịch vụ AI (Thiếu API Key).",
                "Không khả dụng.",
                "Admin vui lòng tự đánh giá dựa trên đoạn chat thực tế."
            );
        }

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";

        var systemInstruction = @"Bạn là trợ lý AI chuyên viên phân giải tranh chấp cấp cao của sàn giao dịch nhiếp ảnh PicMate.
Nhiệm vụ của bạn là:
1. Đọc lý do tranh chấp và toàn bộ lịch sử trò chuyện (Chat Log) giữa Khách Hàng (Customer) và Thợ Chụp (Phone-Grapher).
2. Phân tích vụ việc một cách khách quan: Ai là người có lỗi? Thợ chụp có đi trễ, không đến, giao ảnh muộn, hay thái độ không tốt? Hay khách hàng đòi hỏi vô lý ngoài thỏa thuận?
3. Đưa ra 3 phần phản hồi chính xác bằng tiếng Việt và sắp xếp BẮT BUỘC theo cấu trúc bên dưới, sử dụng đúng các nhãn [SUMMARY], [SENTIMENT], và [RECOMMENDATION]:

[SUMMARY]
(Tóm tắt ngắn gọn diễn biến chính, các sự kiện mấu chốt)

[SENTIMENT]
(Đánh giá thái độ giao tiếp của mỗi bên (lịch sự/thô lỗ), xem ai vi phạm cam kết hoặc có biểu hiện bất hợp tác)

[RECOMMENDATION]
(Đề xuất cụ thể Admin nên chọn phương án nào: Hoàn tiền 100% cho khách, Giải ngân 100% cho thợ, hay Chia tỷ lệ ký quỹ Escrow Split - ví dụ hoàn khách 70%, giải ngân thợ 30%. Đưa ra lý do và phần trăm bồi hoàn cụ thể)";

        var userPrompt = $@"Lý do khiếu nại của người báo cáo ({dispute.Reporter.FullName}):
""{dispute.Reason}""

{chatBuilder}

Hãy phân tích vụ việc trên và trả về kết quả định dạng nhãn theo hướng dẫn.";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = userPrompt }
                    }
                }
            },
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = systemInstruction }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 1536
            }
        };

        try
        {
            using var client = new HttpClient();
            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, httpContent, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                return new AdminDisputeAiAnalysisResponse(
                    "Lỗi kết nối Gemini API.",
                    "Không thể kết nối.",
                    $"Mã lỗi API: {response.StatusCode}. Chi tiết: {errorText}"
                );
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var replyText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(replyText))
            {
                throw new InvalidOperationException("Gemini returned empty text.");
            }

            var summary = ExtractSection(replyText, "[SUMMARY]", "[SENTIMENT]");
            var chatSentiment = ExtractSection(replyText, "[SENTIMENT]", "[RECOMMENDATION]");
            var aiRecommendation = ExtractSection(replyText, "[RECOMMENDATION]", null);

            return new AdminDisputeAiAnalysisResponse(
                summary,
                chatSentiment,
                aiRecommendation
            );
        }
        catch (Exception ex)
        {
            return new AdminDisputeAiAnalysisResponse(
                "Lỗi ngoại lệ khi xử lý phân tích AI.",
                "Không thể xử lý.",
                $"Chi tiết lỗi: {ex.Message}"
            );
        }
    }

    private static string ExtractSection(string text, string startMarker, string? endMarker)
    {
        var startIdx = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (startIdx == -1) return "Không tìm thấy thông tin.";

        startIdx += startMarker.Length;
        
        if (string.IsNullOrEmpty(endMarker))
        {
            return text.Substring(startIdx).Trim();
        }

        var endIdx = text.IndexOf(endMarker, startIdx, StringComparison.OrdinalIgnoreCase);
        if (endIdx == -1)
        {
            return text.Substring(startIdx).Trim();
        }

        return text.Substring(startIdx, endIdx - startIdx).Trim();
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
                MaintenanceMode: false,
                QuarterlyRevenueTarget: 50_000_000m,
                VerifiedGrapherTarget: 50,
                CompletionRateTarget: 90m);
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
        settings.QuarterlyRevenueTarget = request.QuarterlyRevenueTarget;
        settings.VerifiedGrapherTarget = request.VerifiedGrapherTarget;
        settings.CompletionRateTarget = request.CompletionRateTarget;
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
            .Include(u => u.CustomerBookings).ThenInclude(b => b.PaymentTransaction)
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
                b.Status.ToString(),
                // Đang xem chi tiết chính người này nên khách hàng của đơn cũng là họ.
                user.FullName,
                b.PaymentTransaction?.Id,
                b.PaymentTransaction?.Provider.ToString(),
                b.PaymentTransaction?.Status.ToString(),
                b.CreatedAt
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

        return ToBookingDetailResponse(booking);
    }

    /// <summary>
    /// Dựng bản chi tiết đơn hàng. Dùng chung cho endpoint xem chi tiết và cho các
    /// thao tác của admin, để mọi nơi trả về cùng một hình dạng dữ liệu.
    /// </summary>
    private static AdminBookingDetailResponse ToBookingDetailResponse(Domain.Entities.Booking booking)
    {
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
            s.ZaloPayEnabled, s.EmailNotifyNewBooking, s.EmailNotifyDispute, s.MaintenanceMode,
            s.QuarterlyRevenueTarget, s.VerifiedGrapherTarget, s.CompletionRateTarget);

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
