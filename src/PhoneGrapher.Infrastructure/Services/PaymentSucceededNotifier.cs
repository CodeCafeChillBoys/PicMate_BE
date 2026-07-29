using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Infrastructure.Emails;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

/// <summary>
/// Gửi email cho khách và thông báo cho thợ khi một khoản thanh toán chuyển sang thành công.
/// Toàn bộ là best-effort: lỗi gửi tin không được làm hỏng giao dịch đã ghi nhận.
/// </summary>
/// <remarks>
/// Logic này trùng với hai phương thức private trong BookingService dùng cho VNPay.
/// Trùng lặp có chủ đích: đường chạy VNPay chưa có test bảo vệ nên không refactor nó.
/// </remarks>
public sealed class PaymentSucceededNotifier(
    PhoneGrapherDbContext dbContext,
    IEmailService emailService,
    INotificationService notificationService,
    ILogger<PaymentSucceededNotifier> logger)
{
    public async Task NotifyAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await SendConfirmationEmailAsync(booking, cancellationToken);

        try
        {
            await notificationService.CreateAsync(
                booking.GrapherProfile.UserId,
                "booking_new",
                "Đơn đặt mới",
                "Bạn có một đơn đặt lịch mới đã thanh toán, đang chờ bạn xác nhận.",
                booking.Id,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không tạo được thông báo cho thợ (booking {BookingId}).", booking.Id);
        }
    }

    private async Task SendConfirmationEmailAsync(Booking booking, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await dbContext.SystemSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (settings is not null && !settings.EmailNotifyNewBooking)
            {
                return;
            }

            var customerEmail = booking.Customer?.Email;
            if (string.IsNullOrWhiteSpace(customerEmail))
            {
                return;
            }

            var (subject, htmlBody) = BookingEmailTemplates.PaymentConfirmation(booking);
            await emailService.SendAsync(customerEmail, booking.Customer!.FullName, subject, htmlBody, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không gửi được email xác nhận thanh toán cho booking {BookingId}.", booking.Id);
        }
    }
}
