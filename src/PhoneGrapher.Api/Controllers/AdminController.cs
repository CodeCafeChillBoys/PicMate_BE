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
    // ── Revenue / Stats ─────────────────────────────────────────────────────

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

    [HttpGet("graphers/pending")]
    public async Task<ActionResult<IReadOnlyList<AdminPendingGrapherResponse>>> GetPendingGraphers(
        CancellationToken cancellationToken)
    {
        return Ok(await adminService.GetPendingGraphersAsync(cancellationToken));
    }

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
}
