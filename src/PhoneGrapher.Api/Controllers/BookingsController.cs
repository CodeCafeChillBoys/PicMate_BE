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

    [HttpGet("my-orders")]
    [Authorize(Roles = "Grapher")]
    public async Task<ActionResult<IReadOnlyList<GrapherBookingResponse>>> MyOrders(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        return Ok(await bookingService.GetBookingsForGrapherAsync(User.GetUserId(), status, cancellationToken));
    }

    [HttpGet("customer/{customerId:guid}")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<CustomerBookingResponse>>> CustomerOrders(
        Guid customerId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        // Customers can only see their own orders, Admins can see anyone's
        if (!User.IsInRole("Admin") && User.GetUserId() != customerId)
        {
            return Forbid();
        }

        return Ok(await bookingService.GetBookingsByCustomerIdAsync(customerId, status, cancellationToken));
    }

    [HttpGet("{id:guid}/detail")]
    [Authorize]
    public async Task<ActionResult<BookingDetailResponse>> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await bookingService.GetBookingDetailAsync(id, User.GetUserId(), cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(Guid id, CancelBookingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await bookingService.CancelBookingAsync(id, User.GetUserId(), request, cancellationToken);
            return NoContent();
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
}
