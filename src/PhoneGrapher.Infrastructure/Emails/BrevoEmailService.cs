using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Infrastructure.Options;

namespace PhoneGrapher.Infrastructure.Emails;

/// <summary>
/// Gửi email qua Brevo HTTP API (https://api.brevo.com/v3/smtp/email).
/// Dùng cổng 443 (HTTPS) nên không bị Render free chặn như SMTP.
/// Không bao giờ ném lỗi ra ngoài: chưa cấu hình hoặc lỗi API thì chỉ ghi log.
/// </summary>
public sealed class BrevoEmailService(
    IOptions<BrevoOptions> options,
    ILogger<BrevoEmailService> logger) : IEmailService
{
    // HttpClient dùng lại (host cố định api.brevo.com) — tránh tạo mới mỗi lần.
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("https://api.brevo.com/") };
    private readonly BrevoOptions _options = options.Value;

    public async Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.SenderEmail))
        {
            logger.LogInformation(
                "Brevo chưa được cấu hình (Enabled={Enabled}). Bỏ qua email '{Subject}' gửi tới {ToEmail}.",
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
            using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
            request.Headers.Add("api-key", _options.ApiKey);
            request.Content = JsonContent.Create(new
            {
                sender = new { name = _options.SenderName, email = _options.SenderEmail },
                to = new[] { new { email = toEmail, name = string.IsNullOrWhiteSpace(toName) ? toEmail : toName } },
                subject,
                htmlContent = htmlBody
            });

            var response = await Http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Đã gửi email (Brevo) '{Subject}' tới {ToEmail}.", subject, toEmail);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Brevo gửi email '{Subject}' tới {ToEmail} thất bại: HTTP {Status} - {Body}",
                    subject, toEmail, (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: không để lỗi email làm hỏng luồng gọi.
            logger.LogError(ex, "Lỗi khi gửi email Brevo '{Subject}' tới {ToEmail}.", subject, toEmail);
        }
    }
}
