using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class ReviewService(PhoneGrapherDbContext dbContext) : IReviewService
{
    public async Task<ReviewResponse> CreateReviewAsync(Guid customerId, Guid bookingId, ReviewRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Rating is < 1 or > 5)
        {
            throw new InvalidOperationException("Rating must be between 1 and 5.");
        }

        var booking = await dbContext.Bookings
            .Include(x => x.GrapherProfile)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.CustomerId == customerId, cancellationToken)
            ?? throw new InvalidOperationException("Booking not found.");

        if (booking.Status != BookingStatus.Completed)
        {
            throw new InvalidOperationException("Only completed bookings can be reviewed.");
        }

        var exists = await dbContext.Reviews.AnyAsync(x => x.BookingId == bookingId, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("This booking already has a review.");
        }

        var review = new Review
        {
            BookingId = booking.Id,
            CustomerId = customerId,
            GrapherProfileId = booking.GrapherProfileId,
            Rating = request.Rating,
            Comment = request.Comment.Trim()
        };

        dbContext.Reviews.Add(review);
        booking.GrapherProfile.AverageRating =
            ((booking.GrapherProfile.AverageRating * booking.GrapherProfile.ReviewCount) + request.Rating)
            / (booking.GrapherProfile.ReviewCount + 1);
        booking.GrapherProfile.ReviewCount += 1;
        booking.GrapherProfile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ReviewResponse(review.Id, review.BookingId, review.Rating, review.Comment, review.CreatedAt);
    }
}
