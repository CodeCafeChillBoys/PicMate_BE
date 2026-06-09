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
    IAdminService adminService) : ControllerBase
{
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

    [HttpGet("bookings")]
    public async Task<ActionResult<IReadOnlyList<AdminBookingResponse>>> GetAllBookings(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetAllBookingsAsync(status, cancellationToken));
    }

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
}
