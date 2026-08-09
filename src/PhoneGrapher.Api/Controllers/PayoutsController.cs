using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Interfaces;
using PhoneGrapher.Domain.Entities;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/payouts")]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payoutService;

    public PayoutsController(IPayoutService payoutService)
    {
        _payoutService = payoutService;
    }

    [HttpGet("my-wallet")]
    [Authorize]
    public async Task<ActionResult<GrapherWalletResponse>> GetMyWallet(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _payoutService.GetMyWalletAsync(User.GetUserId(), cancellationToken));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPut("my-bank")]
    [Authorize]
    public async Task<IActionResult> UpdateMyBankInfo(
        [FromBody] UpdateBankInfoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _payoutService.UpdateMyBankInfoAsync(User.GetUserId(), request.BankName, request.AccountNumber, request.AccountName, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("request")]
    [Authorize]
    public async Task<ActionResult<PayoutRequestResponse>> RequestPayout(
        [FromBody] PayoutAmountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _payoutService.RequestPayoutAsync(User.GetUserId(), request.Amount, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("my-requests")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<PayoutRequestResponse>>> GetMyRequests(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _payoutService.GetMyPayoutRequestsAsync(User.GetUserId(), cancellationToken));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<PayoutRequestResponse>>> GetAllRequests(
        [FromQuery] PayoutStatus? status,
        CancellationToken cancellationToken)
    {
        return Ok(await _payoutService.GetAllPayoutRequestsAsync(status, cancellationToken));
    }

    [HttpPut("{id:guid}/resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResolveRequest(
        Guid id,
        [FromBody] ResolvePayoutRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _payoutService.ResolvePayoutRequestAsync(id, request.ProofImageUrl, request.AdminNote, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectRequest(
        Guid id,
        [FromBody] RejectPayoutRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _payoutService.RejectPayoutRequestAsync(id, request.RejectReason, request.AdminNote, cancellationToken);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record UpdateBankInfoRequest(string BankName, string AccountNumber, string AccountName);
public record PayoutAmountRequest(decimal Amount);
public record ResolvePayoutRequest(string ProofImageUrl, string? AdminNote);
public record RejectPayoutRequest(string RejectReason, string? AdminNote);

