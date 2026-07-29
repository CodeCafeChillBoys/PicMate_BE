using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(
    IBookingService bookingService,
    IPaymentReconciliationService reconciliationService) : ControllerBase
{
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var result = await bookingService.HandleVnPayCallbackAsync(ToDictionary(), cancellationToken);
        var frontendUrl = "http://localhost:5173/payment-result";
        
        if (result.Success)
        {
            return Redirect($"{frontendUrl}?payment=success&bookingId={result.BookingId}");
        }
        
        return Redirect($"{frontendUrl}?payment=failed&message={Uri.EscapeDataString(result.Message)}");
    }

    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    public async Task<ActionResult<VnPayCallbackResult>> VnPayIpn(CancellationToken cancellationToken)
    {
        var result = await bookingService.HandleVnPayCallbackAsync(ToDictionary(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Thanh toán VietQR (tiền thật, đối soát thủ công) ─────────────────────

    [HttpGet("{bookingId:guid}/vietqr")]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<VietQrPaymentResponse>> GetVietQr(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reconciliationService.GetVietQrAsync(bookingId, User.GetUserId(), cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("{bookingId:guid}/claim-transferred")]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<PaymentStatusResponse>> ClaimTransferred(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reconciliationService.ClaimTransferredAsync(bookingId, User.GetUserId(), cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpGet("{bookingId:guid}/status")]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<PaymentStatusResponse>> GetStatus(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reconciliationService.GetStatusAsync(bookingId, User.GetUserId(), cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    private IReadOnlyDictionary<string, string> ToDictionary()
    {
        return Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal);
    }
}
