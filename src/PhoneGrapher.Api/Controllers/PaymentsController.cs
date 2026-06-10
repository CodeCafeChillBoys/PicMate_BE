using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IBookingService bookingService, IConfiguration configuration) : ControllerBase
{
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var result = await bookingService.HandleVnPayCallbackAsync(ToDictionary(), cancellationToken);
        var frontendBaseUrl = configuration["FrontendUrl"]?.Trim()?.TrimEnd('/') ?? "https://pic-mate-fe.vercel.app";
        // Ensure no control characters (like \n) are present in the base URL that could cause header validation to fail
        frontendBaseUrl = new string(frontendBaseUrl.Where(c => !char.IsControl(c)).ToArray());
        var frontendUrl = $"{frontendBaseUrl}/payment-result";
        
        if (result.Success)
        {
            return Redirect($"{frontendUrl}?payment=success&bookingId={result.BookingId}");
        }
        
        return Redirect($"{frontendUrl}?payment=failed&message={Uri.EscapeDataString(result.Message ?? string.Empty)}");
    }

    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    public async Task<ActionResult<VnPayCallbackResult>> VnPayIpn(CancellationToken cancellationToken)
    {
        var result = await bookingService.HandleVnPayCallbackAsync(ToDictionary(), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private IReadOnlyDictionary<string, string> ToDictionary()
    {
        return Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.Ordinal);
    }
}
