using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;

namespace PhoneGrapher.Infrastructure.Tests;

internal sealed class FakeEmailService : IEmailService
{
    public List<(string ToEmail, string Subject)> Sent { get; } = [];

    public Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        Sent.Add((toEmail, subject));
        return Task.CompletedTask;
    }
}

internal sealed class FakeNotificationService : INotificationService
{
    public List<(Guid UserId, string Type, Guid? BookingId)> Created { get; } = [];

    public Task CreateAsync(Guid userId, string type, string title, string message, Guid? relatedBookingId = null, CancellationToken cancellationToken = default)
    {
        Created.Add((userId, type, relatedBookingId));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationResponse>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NotificationResponse>>([]);

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class FakeVnPayService : IVnPayService
{
    public string CreatePaymentUrl(PaymentTransaction payment, string clientIpAddress)
        => "https://sandbox.vnpayment.vn/fake";

    public bool VerifyCallback(IReadOnlyDictionary<string, string> query) => true;
}
