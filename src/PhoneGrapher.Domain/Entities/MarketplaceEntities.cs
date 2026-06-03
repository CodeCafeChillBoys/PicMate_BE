using PhoneGrapher.Domain.Common;
using PhoneGrapher.Domain.Enums;

namespace PhoneGrapher.Domain.Entities;

public sealed class User : Entity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    public GrapherProfile? GrapherProfile { get; set; }
    public ICollection<Booking> CustomerBookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public User User { get; set; } = null!;
}

public sealed class GrapherProfile : Entity
{
    public Guid UserId { get; set; }
    public string Bio { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? CccdNumber { get; set; }
    public string? CccdFrontImageUrl { get; set; }
    public string? CccdBackImageUrl { get; set; }
    public KycStatus KycStatus { get; set; } = KycStatus.NotSubmitted;
    public bool IsVerified { get; set; }
    public bool IsOnline { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }

    public User User { get; set; } = null!;
    public ICollection<GrapherPortfolioItem> PortfolioItems { get; set; } = new List<GrapherPortfolioItem>();
    public ICollection<GrapherServicePackage> ServicePackages { get; set; } = new List<GrapherServicePackage>();
    public ICollection<GrapherStyleTag> StyleTags { get; set; } = new List<GrapherStyleTag>();
    public ICollection<GrapherActivityArea> ActivityAreas { get; set; } = new List<GrapherActivityArea>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

public sealed class StyleTag : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Emoji { get; set; }
    public string? Color { get; set; }

    public ICollection<GrapherStyleTag> Graphers { get; set; } = new List<GrapherStyleTag>();
}

public sealed class GrapherStyleTag
{
    public Guid GrapherProfileId { get; set; }
    public Guid StyleTagId { get; set; }

    public GrapherProfile GrapherProfile { get; set; } = null!;
    public StyleTag StyleTag { get; set; } = null!;
}

public sealed class GrapherPortfolioItem : Entity
{
    public Guid GrapherProfileId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }

    public GrapherProfile GrapherProfile { get; set; } = null!;
}

public sealed class GrapherActivityArea : Entity
{
    public Guid GrapherProfileId { get; set; }
    public string City { get; set; } = string.Empty;
    public string? District { get; set; }

    public GrapherProfile GrapherProfile { get; set; } = null!;
}

public sealed class GrapherServicePackage : Entity
{
    public Guid GrapherProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    public GrapherProfile GrapherProfile { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

public sealed class Booking : Entity
{
    public Guid CustomerId { get; set; }
    public Guid GrapherProfileId { get; set; }
    public Guid ServicePackageId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Note { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;
    public decimal TotalAmount { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal GrapherPayoutAmount { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CancellationReason { get; set; }

    public User Customer { get; set; } = null!;
    public GrapherProfile GrapherProfile { get; set; } = null!;
    public GrapherServicePackage ServicePackage { get; set; } = null!;
    public PaymentTransaction? PaymentTransaction { get; set; }
    public Review? Review { get; set; }
}

public sealed class PaymentTransaction : Entity
{
    public Guid BookingId { get; set; }
    public PaymentProvider Provider { get; set; } = PaymentProvider.VnPay;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public EscrowStatus EscrowStatus { get; set; } = EscrowStatus.None;
    public string TransactionCode { get; set; } = string.Empty;
    public string? ProviderTransactionId { get; set; }
    public string? ProviderResponseCode { get; set; }
    public decimal Amount { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal GrapherPayoutAmount { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? RawCallbackPayload { get; set; }

    public Booking Booking { get; set; } = null!;
}

public sealed class Review : Entity
{
    public Guid BookingId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid GrapherProfileId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;

    public Booking Booking { get; set; } = null!;
    public User Customer { get; set; } = null!;
    public GrapherProfile GrapherProfile { get; set; } = null!;
}

public sealed class Preset : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public decimal Rating { get; set; }
    public int DownloadCount { get; set; }
    public bool IsActive { get; set; } = true;
}
