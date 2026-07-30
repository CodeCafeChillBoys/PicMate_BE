using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;
using PhoneGrapher.Infrastructure.Services;

namespace PhoneGrapher.Infrastructure.Tests;

public class AdminBookingActionsTests
{
    private static async Task<Booking> AddBookingAsync(
        PhoneGrapherDbContext db,
        SeedResult seed,
        BookingStatus bookingStatus,
        PaymentStatus paymentStatus,
        EscrowStatus escrowStatus,
        PaymentProvider provider = PaymentProvider.VietQr,
        DateTimeOffset? createdAt = null)
    {
        var booking = new Booking
        {
            CustomerId = seed.CustomerId,
            GrapherProfileId = seed.GrapherProfileId,
            ServicePackageId = seed.ServicePackageId,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            DurationMinutes = 60,
            Location = "Ho Guom",
            Status = bookingStatus,
            TotalAmount = 500_000m,
            PlatformFeeAmount = 75_000m,
            GrapherPayoutAmount = 425_000m,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };

        db.Bookings.Add(booking);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Booking = booking,
            Provider = provider,
            Status = paymentStatus,
            EscrowStatus = escrowStatus,
            Amount = booking.TotalAmount,
            PlatformFeeAmount = booking.PlatformFeeAmount,
            GrapherPayoutAmount = booking.GrapherPayoutAmount,
            TransactionCode = $"T{Guid.NewGuid():N}"[..20]
        });

        await db.SaveChangesAsync();
        return booking;
    }

    // ── Ép hoàn thành ────────────────────────────────────────────────────────

    [Fact]
    public async Task ForceComplete_DonDaThuTien_ChuyenSangCompletedVaGiaiPhongEscrow()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Succeeded, EscrowStatus.Held);

        var result = await new AdminService(db).ForceCompleteBookingAsync(booking.Id, seed.AdminId);

        var updated = await db.Bookings.Include(b => b.PaymentTransaction).SingleAsync();

        Assert.Equal("Completed", result.Status);
        Assert.Equal(BookingStatus.Completed, updated.Status);
        Assert.NotNull(updated.CompletedAt);
        Assert.Equal(EscrowStatus.Released, updated.PaymentTransaction!.EscrowStatus);
        Assert.NotNull(updated.PaymentTransaction.ReleasedAt);
        Assert.Equal(seed.AdminId, updated.PaymentTransaction.VerifiedByUserId);
    }

    [Fact]
    public async Task ForceComplete_DonChuaThuTien_BiTuChoi()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Pending, EscrowStatus.None);
        var service = new AdminService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ForceCompleteBookingAsync(booking.Id, seed.AdminId));

        Assert.Contains("chưa thu được tiền", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BookingStatus.Confirmed, (await db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task ForceComplete_DonTienMat_ChapNhanDuDdChuaCoEscrow()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Pending, EscrowStatus.None, PaymentProvider.Cod);

        await new AdminService(db).ForceCompleteBookingAsync(booking.Id, seed.AdminId);

        var updated = await db.Bookings.Include(b => b.PaymentTransaction).SingleAsync();

        Assert.Equal(BookingStatus.Completed, updated.Status);
        Assert.Equal(PaymentStatus.Succeeded, updated.PaymentTransaction!.Status);
        Assert.NotNull(updated.PaymentTransaction.PaidAt);
    }

    [Theory]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.Cancelled)]
    public async Task ForceComplete_DonDaKetThuc_BiTuChoi(BookingStatus status)
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, status, PaymentStatus.Succeeded, EscrowStatus.Held);
        var service = new AdminService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ForceCompleteBookingAsync(booking.Id, seed.AdminId));
    }

    [Fact]
    public async Task ForceComplete_DonChuaThanhToan_BiTuChoiVìChuaToiBuoc()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, BookingStatus.PendingPayment, PaymentStatus.Succeeded, EscrowStatus.Held);
        var service = new AdminService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ForceCompleteBookingAsync(booking.Id, seed.AdminId));
    }

    [Fact]
    public async Task ForceComplete_DonKhongTonTai_NemKeyNotFound()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var service = new AdminService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ForceCompleteBookingAsync(Guid.NewGuid(), seed.AdminId));
    }

    // ── Hoàn tiền ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refund_DonChuaXong_DanhDauHoanTienVaHuyLuonDon()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Succeeded, EscrowStatus.Held);

        await new AdminService(db).RefundBookingAsync(
            booking.Id, seed.AdminId, new RefundBookingRequest("Khách báo bận"));

        var updated = await db.Bookings.Include(b => b.PaymentTransaction).SingleAsync();

        Assert.Equal(PaymentStatus.Refunded, updated.PaymentTransaction!.Status);
        Assert.Equal(EscrowStatus.Refunded, updated.PaymentTransaction.EscrowStatus);
        Assert.Equal(BookingStatus.Cancelled, updated.Status);
        Assert.Contains("Khách báo bận", updated.CancellationReason);
        Assert.Equal(seed.AdminId, updated.PaymentTransaction.VerifiedByUserId);
    }

    [Fact]
    public async Task Refund_DonDaHoanThanh_GiuNguyenTrangThaiDon()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, BookingStatus.Completed, PaymentStatus.Succeeded, EscrowStatus.Released);

        await new AdminService(db).RefundBookingAsync(
            booking.Id, seed.AdminId, new RefundBookingRequest("Hoàn thiện chí sau tranh chấp"));

        var updated = await db.Bookings.Include(b => b.PaymentTransaction).SingleAsync();

        // Buổi chụp đã diễn ra nên đơn vẫn là Completed, chỉ phần tiền đổi trạng thái.
        Assert.Equal(BookingStatus.Completed, updated.Status);
        Assert.Equal(PaymentStatus.Refunded, updated.PaymentTransaction!.Status);
    }

    [Fact]
    public async Task Refund_DonChuaThanhToan_BiTuChoi()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, BookingStatus.PendingPayment, PaymentStatus.Pending, EscrowStatus.None);
        var service = new AdminService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefundBookingAsync(booking.Id, seed.AdminId, new RefundBookingRequest(null)));

        Assert.Equal(PaymentStatus.Pending, (await db.PaymentTransactions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Refund_GoiLanHai_BiTuChoi()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Succeeded, EscrowStatus.Held);
        var service = new AdminService(db);

        await service.RefundBookingAsync(booking.Id, seed.AdminId, new RefundBookingRequest("Lần đầu"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefundBookingAsync(booking.Id, seed.AdminId, new RefundBookingRequest("Lần hai")));

        Assert.Equal("Lần đầu", (await db.PaymentTransactions.SingleAsync()).VerificationNote);
    }

    [Fact]
    public async Task Refund_KhongGhiChu_VanCoNoiDungMacDinh()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Succeeded, EscrowStatus.Held);

        await new AdminService(db).RefundBookingAsync(booking.Id, seed.AdminId, new RefundBookingRequest("   "));

        var payment = await db.PaymentTransactions.SingleAsync();

        Assert.False(string.IsNullOrWhiteSpace(payment.VerificationNote));
    }

    // ── Danh sách đơn ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllBookings_TraVeTenKhachVaThongTinThanhToan()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Succeeded, EscrowStatus.Held);

        var rows = await new AdminService(db).GetAllBookingsAsync(null, null, null);

        Assert.Single(rows);
        Assert.Equal("Khach Hang Test", rows[0].CustomerName);
        Assert.Equal("VietQr", rows[0].PaymentProvider);
        Assert.Equal("Succeeded", rows[0].PaymentStatus);
        Assert.NotNull(rows[0].PaymentId);
    }

    [Fact]
    public async Task GetAllBookings_LocTheoKhoangNgayDungBien()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var anchor = new DateTimeOffset(2026, 7, 10, 3, 0, 0, TimeSpan.Zero);
        await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Succeeded, EscrowStatus.Held, createdAt: anchor.AddDays(-1));
        await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Succeeded, EscrowStatus.Held, createdAt: anchor);
        await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Succeeded, EscrowStatus.Held, createdAt: anchor.AddDays(5));

        var service = new AdminService(db);

        // Mốc "đến" là thời điểm loại trừ, đúng như controller đẩy sang đầu ngày kế tiếp.
        var inRange = await service.GetAllBookingsAsync(null, anchor, anchor.AddDays(1));
        Assert.Single(inRange);

        var all = await service.GetAllBookingsAsync(null, null, null);
        Assert.Equal(3, all.Count);

        var fromOnly = await service.GetAllBookingsAsync(null, anchor, null);
        Assert.Equal(2, fromOnly.Count);
    }

    [Fact]
    public async Task GetAllBookings_LocTheoTrangThai()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddBookingAsync(db, seed, BookingStatus.Confirmed, PaymentStatus.Succeeded, EscrowStatus.Held);
        await AddBookingAsync(db, seed, BookingStatus.Cancelled, PaymentStatus.Failed, EscrowStatus.None);

        var rows = await new AdminService(db).GetAllBookingsAsync("Cancelled", null, null);

        Assert.Single(rows);
        Assert.Equal("Cancelled", rows[0].Status);
    }
}
