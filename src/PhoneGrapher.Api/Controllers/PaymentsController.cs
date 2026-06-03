using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IBookingService bookingService) : ControllerBase
{
    [HttpGet("vnpay-return")]
    [AllowAnonymous]
    public async Task<ActionResult<VnPayCallbackResult>> VnPayReturn(CancellationToken cancellationToken)
    {
        return Ok(await bookingService.HandleVnPayCallbackAsync(ToDictionary(), cancellationToken));
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
