using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(IBookingService bookingService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> Mine(CancellationToken cancellationToken)
    {
        return Ok(await bookingService.GetBookingsForUserAsync(User.GetUserId(), cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<CreateBookingPaymentResponse>> Create(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var response = await bookingService.CreateBookingAsync(User.GetUserId(), request, ip, cancellationToken);
        return CreatedAtAction(nameof(Mine), new { id = response.Booking.Id }, response);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "Grapher")]
    public async Task<ActionResult<CompleteBookingResponse>> Complete(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await bookingService.CompleteBookingAsync(id, User.GetUserId(), cancellationToken));
    }
}
