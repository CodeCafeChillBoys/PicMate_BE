using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhoneGrapher.Application.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

public sealed class DisputeService(
    PhoneGrapherDbContext dbContext,
    INotificationService notificationService,
    ILogger<DisputeService> logger) : IDisputeService
{
    public async Task<Guid> CreateDisputeAsync(Guid reporterId, CreateDisputeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Vui lòng nhập lý do khiếu nại.");
        }

        var booking = await dbContext.Bookings
            .Include(x => x.GrapherProfile)
            .FirstOrDefaultAsync(x => x.Id == request.BookingId, cancellationToken)
            ?? throw new KeyNotFoundException("Booking not found.");

        var grapherUserId = booking.GrapherProfile.UserId;
        if (reporterId != booking.CustomerId && reporterId != grapherUserId)
        {
            throw new UnauthorizedAccessException("Bạn không thuộc đơn đặt lịch này.");
        }

        // Chỉ cho khiếu nại đơn đã có tương tác thực sự (đã xác nhận / đang thực hiện / đã hoàn thành).
        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.InProgress or BookingStatus.Completed))
        {
            throw new InvalidOperationException("Chỉ có thể khiếu nại đơn đã xác nhận, đang thực hiện hoặc đã hoàn thành.");
        }

        // Người bị khiếu nại là phía còn lại của đơn.
        var respondentId = reporterId == booking.CustomerId ? grapherUserId : booking.CustomerId;

        var hasOpen = await dbContext.Disputes
            .AnyAsync(x => x.BookingId == booking.Id && x.Status == DisputeStatus.Pending, cancellationToken);
        if (hasOpen)
        {
            throw new InvalidOperationException("Đơn này đã có khiếu nại đang chờ xử lý.");
        }

        var priority = Enum.TryParse<DisputePriority>(request.Priority, ignoreCase: true, out var parsed)
            ? parsed
            : DisputePriority.Medium;

        var dispute = new Dispute
        {
            BookingId = booking.Id,
            ReporterId = reporterId,
            RespondentId = respondentId,
            Reason = request.Reason.Trim(),
            Status = DisputeStatus.Pending,
            Priority = priority
        };
        dbContext.Disputes.Add(dispute);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Báo cho tất cả admin đang hoạt động (best-effort: lỗi thông báo không làm hỏng việc tạo khiếu nại).
        try
        {
            var adminIds = await dbContext.Users
                .Where(u => u.Role == UserRole.Admin && u.IsActive)
                .Select(u => u.Id)
                .ToArrayAsync(cancellationToken);

            foreach (var adminId in adminIds)
            {
                await notificationService.CreateAsync(adminId, "dispute", "Khiếu nại mới",
                    "Có một khiếu nại mới cần xử lý.", booking.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không gửi được thông báo khiếu nại tới admin (dispute {DisputeId}).", dispute.Id);
        }

        return dispute.Id;
    }
}
