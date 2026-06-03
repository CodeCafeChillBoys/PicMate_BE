using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;

namespace PhoneGrapher.Api.Controllers;

[ApiController]
[Route("api/bookings/{bookingId:guid}/reviews")]
public sealed class ReviewsController(IReviewService reviewService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<ActionResult<ReviewResponse>> Create(
        Guid bookingId,
        ReviewRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await reviewService.CreateReviewAsync(User.GetUserId(), bookingId, request, cancellationToken));
    }
}
