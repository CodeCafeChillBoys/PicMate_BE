namespace PhoneGrapher.Domain.Enums;

public enum UserRole
{
    Customer = 1,
    Grapher = 2,
    Admin = 3
}

public enum KycStatus
{
    NotSubmitted = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4
}

public enum BookingStatus
{
    PendingPayment = 1,
    PendingConfirmation = 2,
    Confirmed = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6
}

public enum PaymentProvider
{
    VnPay = 1
}

public enum PaymentStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Refunded = 4
}

public enum EscrowStatus
{
    None = 1,
    Held = 2,
    ReleasePending = 3,
    Released = 4,
    Refunded = 5
}

public enum DisputeStatus
{
    Pending = 1,
    Resolved = 2,
    Closed = 3
}

public enum DisputePriority
{
    Medium = 1,
    High = 2,
    Urgent = 3
}
