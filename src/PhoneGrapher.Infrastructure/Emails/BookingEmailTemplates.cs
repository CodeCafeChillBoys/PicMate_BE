using System.Globalization;
using PhoneGrapher.Domain.Entities;

namespace PhoneGrapher.Infrastructure.Emails;

/// <summary>Dựng nội dung HTML cho các email liên quan tới booking.</summary>
public static class BookingEmailTemplates
{
    private static readonly CultureInfo Vietnam = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Email gửi cho khách hàng sau khi thanh toán thành công.</summary>
    public static (string Subject, string HtmlBody) PaymentConfirmation(Booking booking)
    {
        var grapherName = booking.GrapherProfile?.User?.FullName ?? "Phone-Grapher";
        var serviceName = booking.ServicePackage?.Name ?? "Gói chụp ảnh";
        var scheduled = booking.ScheduledAt.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm 'ngày' dd/MM/yyyy", Vietnam);
        var amount = booking.TotalAmount.ToString("N0", Vietnam) + "đ";
        var bookingRef = booking.Id.ToString("N")[..8].ToUpperInvariant();

        var subject = $"PICMate - Xác nhận thanh toán đơn #{bookingRef}";

        var body = $$"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#1a1a2e;">
          <div style="background:linear-gradient(135deg,#6C5CE7,#FF6B6B);padding:28px 24px;border-radius:12px 12px 0 0;">
            <h1 style="margin:0;color:#fff;font-size:22px;">PICMate</h1>
            <p style="margin:6px 0 0;color:#f0f0ff;font-size:14px;">Thanh toán thành công 🎉</p>
          </div>
          <div style="border:1px solid #e8e8f0;border-top:none;border-radius:0 0 12px 12px;padding:24px;">
            <p>Xin chào <strong>{{booking.Customer?.FullName}}</strong>,</p>
            <p>Cảm ơn bạn đã đặt lịch trên PICMate. Chúng tôi đã nhận được thanh toán của bạn cho đơn dưới đây:</p>
            <table style="width:100%;border-collapse:collapse;margin:16px 0;font-size:14px;">
              <tr><td style="padding:8px 0;color:#5a5a7a;">Mã đơn</td><td style="padding:8px 0;text-align:right;"><strong>#{{bookingRef}}</strong></td></tr>
              <tr><td style="padding:8px 0;color:#5a5a7a;">Phone-Grapher</td><td style="padding:8px 0;text-align:right;">{{grapherName}}</td></tr>
              <tr><td style="padding:8px 0;color:#5a5a7a;">Dịch vụ</td><td style="padding:8px 0;text-align:right;">{{serviceName}}</td></tr>
              <tr><td style="padding:8px 0;color:#5a5a7a;">Thời gian</td><td style="padding:8px 0;text-align:right;">{{scheduled}}</td></tr>
              <tr><td style="padding:8px 0;color:#5a5a7a;">Địa điểm</td><td style="padding:8px 0;text-align:right;">{{booking.Location}}</td></tr>
              <tr><td style="padding:12px 0 0;border-top:1px solid #e8e8f0;color:#5a5a7a;">Tổng thanh toán</td><td style="padding:12px 0 0;border-top:1px solid #e8e8f0;text-align:right;font-size:16px;color:#6C5CE7;"><strong>{{amount}}</strong></td></tr>
            </table>
            <div style="background:#f0fff8;border:1px solid #00b894;border-radius:8px;padding:12px 16px;font-size:13px;color:#1a1a2e;">
              🛡️ Số tiền của bạn được PICMate đảm bảo an toàn cho đến khi buổi chụp hoàn tất.
            </div>
            <p style="margin-top:20px;font-size:13px;color:#9a9ab0;">Phone-Grapher sẽ xác nhận đơn của bạn sớm. Bạn có thể theo dõi trạng thái đơn trong trang cá nhân trên PICMate.</p>
          </div>
          <p style="text-align:center;font-size:12px;color:#9a9ab0;margin-top:16px;">© PICMate · Đây là email tự động, vui lòng không trả lời.</p>
        </div>
        """;

        return (subject, body);
    }
}
