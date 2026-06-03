using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;

namespace PhoneGrapher.Infrastructure.Services;

internal static class MappingExtensions
{
    public static BookingResponse ToResponse(this Booking booking)
    {
        return new BookingResponse(
            booking.Id,
            booking.CustomerId,
            booking.GrapherProfileId,
            booking.ServicePackageId,
            booking.ScheduledAt,
            booking.Location,
            booking.Status.ToString(),
            booking.TotalAmount,
            booking.PlatformFeeAmount,
            booking.GrapherPayoutAmount,
            booking.PaymentTransaction?.ToResponse());
    }

    public static PaymentTransactionResponse ToResponse(this PaymentTransaction payment)
    {
        return new PaymentTransactionResponse(
            payment.Id,
            payment.Provider.ToString(),
            payment.Status.ToString(),
            payment.EscrowStatus.ToString(),
            payment.TransactionCode,
            payment.ProviderTransactionId,
            payment.Amount,
            payment.PaidAt,
            payment.ReleasedAt);
    }

    public static GrapherSummaryResponse ToSummaryResponse(this GrapherProfile profile)
    {
        var hourly = profile.ServicePackages
            .Where(x => x.IsActive)
            .OrderBy(x => x.Price)
            .Select(x => x.Price)
            .FirstOrDefault();

        var daily = profile.ServicePackages
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.DurationMinutes)
            .Select(x => x.Price)
            .FirstOrDefault();

        return new GrapherSummaryResponse(
            profile.Id,
            profile.User.FullName,
            profile.User.AvatarUrl,
            profile.Location,
            profile.AverageRating,
            profile.ReviewCount,
            profile.IsOnline,
            profile.IsVerified,
            profile.StyleTags.Select(x => x.StyleTag.Name).OrderBy(x => x).ToArray(),
            profile.PortfolioItems.OrderBy(x => x.DisplayOrder).Select(x => x.ImageUrl).ToArray(),
            new GrapherPricingResponse(hourly, daily == 0 ? hourly : daily));
    }
}
