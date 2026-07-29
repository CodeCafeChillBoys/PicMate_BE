using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Infrastructure.Options;

namespace PhoneGrapher.Infrastructure.Emails;

/// <summary>
/// Gửi email qua SMTP. Được thiết kế để không bao giờ ném lỗi ra ngoài: nếu chưa cấu hình
/// hoặc SMTP lỗi thì chỉ ghi log, để luồng nghiệp vụ gọi nó (vd: callback thanh toán) không bị gián đoạn.
/// </summary>
public sealed class SmtpEmailService(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            logger.LogInformation(
                "SMTP chưa được cấu hình (Enabled={Enabled}). Bỏ qua email '{Subject}' gửi tới {ToEmail}.",
                _options.Enabled, subject, toEmail);
            return;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            logger.LogWarning("Bỏ qua email '{Subject}': địa chỉ người nhận trống.", subject);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail, string.IsNullOrWhiteSpace(toName) ? toEmail : toName));

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15_000
            };

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Đã gửi email '{Subject}' tới {ToEmail}.", subject, toEmail);
        }
        catch (Exception ex)
        {
            // Best-effort: không để lỗi email làm hỏng luồng gọi.
            logger.LogError(ex, "Gửi email '{Subject}' tới {ToEmail} thất bại.", subject, toEmail);
        }
    }
}
