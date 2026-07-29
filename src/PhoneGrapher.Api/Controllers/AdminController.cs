using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController(
    IGrapherService grapherService,
    IAdminService adminService,
    IPaymentReconciliationService reconciliationService,
    IAdminAnalyticsService analyticsService) : ControllerBase
{
    // ── Dashboard Tổng quan ──────────────────────────────────────────────────

    [HttpGet("analytics")]
    public async Task<ActionResult<AdminAnalyticsResponse>> Analytics(
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        return Ok(await analyticsService.GetAsync(range, cancellationToken));
    }

    // ── Revenue / Stats ──────────────────────────────────────────────────────

    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueSummaryResponse>> Revenue(CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetRevenueSummaryAsync(cancellationToken));
    }

    // ── Users ────────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetAllUsersAsync(search, role, cancellationToken));
    }

    [HttpPut("users/{id:guid}/toggle-status")]
    public async Task<ActionResult<AdminUserResponse>> ToggleUserStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.ToggleUserStatusAsync(id, cancellationToken));
    }

    // ── Photographers / Graphers ─────────────────────────────────────────────

    /// <summary>Lấy danh sách graphers đang chờ duyệt KYC.</summary>
    [HttpGet("graphers/pending")]
    public async Task<ActionResult<IReadOnlyList<AdminPendingGrapherResponse>>> GetPendingGraphers(
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetPendingGraphersAsync(cancellationToken));
    }

    /// <summary>Lấy danh sách graphers đã được duyệt (admin view với trạng thái khóa).</summary>
    [HttpGet("graphers/active")]
    public async Task<ActionResult<IReadOnlyList<AdminActiveGrapherResponse>>> GetActiveGraphers(
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetActiveGraphersAsync(cancellationToken));
    }

    /// <summary>Khóa hoặc mở khóa tài khoản của một grapher.</summary>
    [HttpPut("graphers/{grapherProfileId:guid}/toggle-status")]
    public async Task<ActionResult<AdminActiveGrapherResponse>> ToggleGrapherStatus(
        Guid grapherProfileId,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.ToggleGrapherStatusAsync(grapherProfileId, cancellationToken));
    }

    /// <summary>Duyệt hoặc từ chối KYC của một grapher.</summary>
    [HttpPost("graphers/{grapherProfileId:guid}/kyc")]
    public async Task<IActionResult> ApproveGrapherKyc(
        Guid grapherProfileId,
        [FromQuery] bool approved,
        CancellationToken cancellationToken)
    {
        await grapherService.ApproveKycAsync(grapherProfileId, approved, cancellationToken);
        return NoContent();
    }

    // ── Bookings ─────────────────────────────────────────────────────────────

    /// <summary>Admin đóng đơn thay thợ khi thợ chụp xong nhưng quên bấm hoàn thành.</summary>
    [HttpPost("bookings/{id:guid}/complete")]
    public async Task<ActionResult<AdminBookingDetailResponse>> ForceCompleteBooking(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await adminService.ForceCompleteBookingAsync(id, User.GetUserId(), cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>Đánh dấu đơn đã hoàn tiền. Không tự chuyển tiền, chỉ ghi nhận trạng thái.</summary>
    [HttpPost("bookings/{id:guid}/refund")]
    public async Task<ActionResult<AdminBookingDetailResponse>> RefundBooking(
        Guid id,
        RefundBookingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await adminService.RefundBookingAsync(id, User.GetUserId(), request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>Giờ Việt Nam: người dùng chọn ngày theo lịch của họ, không phải theo UTC.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    [HttpGet("bookings")]
    public async Task<ActionResult<IReadOnlyList<AdminBookingResponse>>> GetAllBookings(
        [FromQuery] string? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return BadRequest(new { Error = "Ngày bắt đầu phải trước ngày kết thúc." });
        }

        var fromUtc = from.HasValue ? ToVietnamStartOfDayUtc(from.Value) : (DateTimeOffset?)null;
        // Đẩy sang đầu ngày kế tiếp để đơn tạo trong chính ngày "đến" vẫn được tính.
        var toUtc = to.HasValue ? ToVietnamStartOfDayUtc(to.Value.AddDays(1)) : (DateTimeOffset?)null;

        return Ok(await adminService.GetAllBookingsAsync(status, fromUtc, toUtc, cancellationToken));
    }

    private static DateTimeOffset ToVietnamStartOfDayUtc(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime();

    // ── Activities ───────────────────────────────────────────────────────────

    [HttpGet("activities")]
    public async Task<ActionResult<IReadOnlyList<AdminActivityResponse>>> GetRecentActivities(
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetRecentActivitiesAsync(cancellationToken));
    }

    // ── Disputes ─────────────────────────────────────────────────────────────

    /// <summary>Lấy danh sách tranh chấp, có thể lọc theo status.</summary>
    [HttpGet("disputes")]
    public async Task<ActionResult<IReadOnlyList<AdminDisputeResponse>>> GetDisputes(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetDisputesAsync(status, cancellationToken));
    }

    /// <summary>
    /// Giải quyết một tranh chấp.
    /// action: 'refund' (hoàn tiền + hủy booking), 'warning' (cảnh báo), 'resolved' (đã giải quyết).
    /// </summary>
    [HttpPost("disputes/{disputeId:guid}/resolve")]
    public async Task<ActionResult<AdminDisputeResponse>> ResolveDispute(
        Guid disputeId,
        [FromBody] ResolveDisputeRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.ResolveDisputeAsync(disputeId, request, cancellationToken));
    }

    // ── System Settings ───────────────────────────────────────────────────────

    /// <summary>Lấy cài đặt hệ thống hiện tại.</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<SystemSettingsResponse>> GetSystemSettings(
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetSystemSettingsAsync(cancellationToken));
    }

    /// <summary>Cập nhật cài đặt hệ thống (platform fee, payment methods, thông báo, bảo trì).</summary>
    [HttpPut("settings")]
    public async Task<ActionResult<SystemSettingsResponse>> UpdateSystemSettings(
        [FromBody] UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.UpdateSystemSettingsAsync(request, cancellationToken));
    }

    // ── Detail Views ─────────────────────────────────────────────────────────

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<AdminUserDetailResponse>> GetUserDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetUserDetailAsync(id, cancellationToken));
    }

    [HttpGet("graphers/{id:guid}")]
    public async Task<ActionResult<AdminGrapherDetailResponse>> GetGrapherDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetGrapherDetailAsync(id, cancellationToken));
    }

    [HttpGet("bookings/{id:guid}")]
    public async Task<ActionResult<AdminBookingDetailResponse>> GetBookingDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetBookingDetailAsync(id, cancellationToken));
    }

    // ── Đối soát thanh toán VietQR ───────────────────────────────────────────

    [HttpGet("payments/pending")]
    public async Task<ActionResult<IReadOnlyList<PendingPaymentResponse>>> PendingPayments(CancellationToken cancellationToken)
    {
        return Ok(await reconciliationService.GetPendingAsync(cancellationToken));
    }

    [HttpGet("payments/recently-expired")]
    public async Task<ActionResult<IReadOnlyList<PendingPaymentResponse>>> RecentlyExpiredPayments(CancellationToken cancellationToken)
    {
        return Ok(await reconciliationService.GetRecentlyExpiredAsync(cancellationToken));
    }

    [HttpPost("payments/{paymentId:guid}/verify")]
    public async Task<ActionResult<PaymentStatusResponse>> VerifyPayment(
        Guid paymentId,
        VerifyPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reconciliationService.VerifyAsync(paymentId, User.GetUserId(), request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
