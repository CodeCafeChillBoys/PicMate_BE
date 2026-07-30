using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Options;
using PhoneGrapher.Infrastructure.Payments;
using PhoneGrapher.Infrastructure.Persistence;
using PhoneGrapher.Infrastructure.Services;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace PhoneGrapher.Infrastructure.Tests;

public class AdminReconciliationTests
{
    private static VietQrOptions VietQrConfig() => new()
    {
        Enabled = true,
        BankBin = "970405",
        BankName = "Agribank",
        AccountNumber = "5402205457446",
        AccountName = "NGUYEN VAN A",
        ExpiryMinutes = 15
    };

    private static PaymentReconciliationService CreateService(
        PhoneGrapherDbContext db,
        FakeEmailService email,
        FakeNotificationService notifications)
    {
        var options = MsOptions.Create(VietQrConfig());
        return new PaymentReconciliationService(
            db,
            new VietQrService(options),
            options,
            new PaymentSucceededNotifier(db, email, notifications, NullLogger<PaymentSucceededNotifier>.Instance),
            NullLogger<PaymentReconciliationService>.Instance);
    }

    private static async Task<(SeedResult Seed, Guid BookingId)> ArrangeClaimedBookingAsync(PhoneGrapherDbContext db)
    {
        var seed = await TestDb.SeedAsync(db);
        var bookingService = new BookingService(
            db, new FakeVnPayService(), new FakeEmailService(), new FakeNotificationService(),
            NullLogger<BookingService>.Instance, MsOptions.Create(VietQrConfig()));

        var created = await bookingService.CreateBookingAsync(
            seed.CustomerId,
            new CreateBookingRequest(seed.GrapherProfileId, seed.ServicePackageId,
                DateTimeOffset.UtcNow.AddDays(3), "Ho Guom, Ha Noi", null, "vietqr"),
            "127.0.0.1");

        var payment = await db.PaymentTransactions.SingleAsync();
        payment.Status = PaymentStatus.AwaitingVerification;
        payment.CustomerClaimedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return (seed, created.Booking.Id);
    }

    [Fact]
    public async Task VerifyAsync_Duyet_ChuyenSangSucceededHeldVaPendingConfirmation()
    {
        using var db = TestDb.Create();
        var (seed, _) = await ArrangeClaimedBookingAsync(db);
        var payment = await db.PaymentTransactions.SingleAsync();
        var service = CreateService(db, new FakeEmailService(), new FakeNotificationService());

        var result = await service.VerifyAsync(payment.Id, seed.AdminId, new VerifyPaymentRequest(true, null));

        var updated = await db.PaymentTransactions.Include(x => x.Booking).SingleAsync();

        Assert.Equal("Succeeded", result.PaymentStatus);
        Assert.Equal(PaymentStatus.Succeeded, updated.Status);
        Assert.Equal(EscrowStatus.Held, updated.EscrowStatus);
        Assert.Equal(BookingStatus.PendingConfirmation, updated.Booking.Status);
        Assert.NotNull(updated.PaidAt);
        Assert.Equal(seed.AdminId, updated.VerifiedByUserId);
        Assert.NotNull(updated.VerifiedAt);
    }

    [Fact]
    public async Task VerifyAsync_Duyet_GuiEmailChoKhachVaBaoChoTho()
    {
        using var db = TestDb.Create();
        var (seed, bookingId) = await ArrangeClaimedBookingAsync(db);
        var payment = await db.PaymentTransactions.SingleAsync();
        var email = new FakeEmailService();
        var notifications = new FakeNotificationService();

        await CreateService(db, email, notifications)
            .VerifyAsync(payment.Id, seed.AdminId, new VerifyPaymentRequest(true, null));

        Assert.Single(email.Sent);
        Assert.Equal("khach@test.local", email.Sent[0].ToEmail);
        Assert.Single(notifications.Created);
        Assert.Equal(seed.GrapherUserId, notifications.Created[0].UserId);
        Assert.Equal("booking_new", notifications.Created[0].Type);
        Assert.Equal(bookingId, notifications.Created[0].BookingId);
    }

    [Fact]
    public async Task VerifyAsync_DuyetLanHai_KhongGuiEmailLai()
    {
        using var db = TestDb.Create();
        var (seed, _) = await ArrangeClaimedBookingAsync(db);
        var payment = await db.PaymentTransactions.SingleAsync();
        var email = new FakeEmailService();
        var notifications = new FakeNotificationService();
        var service = CreateService(db, email, notifications);

        await service.VerifyAsync(payment.Id, seed.AdminId, new VerifyPaymentRequest(true, null));
        await service.VerifyAsync(payment.Id, seed.AdminId, new VerifyPaymentRequest(true, null));

        Assert.Single(email.Sent);
        Assert.Single(notifications.Created);
    }

    [Fact]
    public async Task VerifyAsync_TuChoi_ChuyenSangFailedVaLuuLyDo()
    {
        using var db = TestDb.Create();
        var (seed, _) = await ArrangeClaimedBookingAsync(db);
        var payment = await db.PaymentTransactions.SingleAsync();
        var email = new FakeEmailService();
        var service = CreateService(db, email, new FakeNotificationService());

        var result = await service.VerifyAsync(
            payment.Id, seed.AdminId, new VerifyPaymentRequest(false, "Không thấy giao dịch trong sao kê."));

        var updated = await db.PaymentTransactions.Include(x => x.Booking).SingleAsync();

        Assert.Equal("Failed", result.PaymentStatus);
        Assert.Equal(PaymentStatus.Failed, updated.Status);
        Assert.Equal(EscrowStatus.None, updated.EscrowStatus);
        Assert.Equal(BookingStatus.PendingPayment, updated.Booking.Status);
        Assert.Equal("Không thấy giao dịch trong sao kê.", updated.VerificationNote);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task VerifyAsync_TuChoiKhongCoLyDo_NemInvalidOperationException()
    {
        using var db = TestDb.Create();
        var (seed, _) = await ArrangeClaimedBookingAsync(db);
        var payment = await db.PaymentTransactions.SingleAsync();
        var service = CreateService(db, new FakeEmailService(), new FakeNotificationService());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.VerifyAsync(payment.Id, seed.AdminId, new VerifyPaymentRequest(false, "   ")));
    }

    [Fact]
    public async Task VerifyAsync_PaymentKhongTonTai_NemKeyNotFoundException()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var service = CreateService(db, new FakeEmailService(), new FakeNotificationService());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.VerifyAsync(Guid.NewGuid(), seed.AdminId, new VerifyPaymentRequest(true, null)));
    }

    [Fact]
    public async Task GetPendingAsync_TraVeDonDaClaimVaDonChuaQuaHan()
    {
        using var db = TestDb.Create();
        await ArrangeClaimedBookingAsync(db);
        var service = CreateService(db, new FakeEmailService(), new FakeNotificationService());

        var pending = await service.GetPendingAsync();

        Assert.Single(pending);
        Assert.Equal("AwaitingVerification", pending[0].PaymentStatus);
        Assert.Equal("Khach Hang Test", pending[0].CustomerName);
        Assert.Equal("Tho Chup Test", pending[0].GrapherName);
        Assert.Equal("Goi test", pending[0].ServiceName);
        Assert.Equal(1_500_000m, pending[0].Amount);
    }

    [Fact]
    public async Task GetPendingAsync_BoQuaDonDaDuyet()
    {
        using var db = TestDb.Create();
        var (seed, _) = await ArrangeClaimedBookingAsync(db);
        var payment = await db.PaymentTransactions.SingleAsync();
        var service = CreateService(db, new FakeEmailService(), new FakeNotificationService());
        await service.VerifyAsync(payment.Id, seed.AdminId, new VerifyPaymentRequest(true, null));

        var pending = await service.GetPendingAsync();

        Assert.Empty(pending);
    }

    [Fact]
    public async Task GetRecentlyExpiredAsync_ChiTraVeDonTuHuyTrong24Gio()
    {
        using var db = TestDb.Create();
        await ArrangeClaimedBookingAsync(db);
        var payment = await db.PaymentTransactions.Include(x => x.Booking).SingleAsync();
        payment.Status = PaymentStatus.Failed;
        payment.CustomerClaimedAt = null;
        payment.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        payment.Booking.Status = BookingStatus.Cancelled;
        payment.Booking.CancellationReason = "Quá hạn thanh toán";
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeEmailService(), new FakeNotificationService());

        var expired = await service.GetRecentlyExpiredAsync();

        Assert.Single(expired);
        Assert.Equal("Cancelled", expired[0].BookingStatus);
    }

    [Fact]
    public async Task GetRecentlyExpiredAsync_BoQuaDonHuyQua24Gio()
    {
        using var db = TestDb.Create();
        await ArrangeClaimedBookingAsync(db);
        var payment = await db.PaymentTransactions.Include(x => x.Booking).SingleAsync();
        payment.Status = PaymentStatus.Failed;
        payment.CustomerClaimedAt = null;
        payment.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-30);
        payment.Booking.Status = BookingStatus.Cancelled;
        payment.Booking.CancellationReason = "Quá hạn thanh toán";
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeEmailService(), new FakeNotificationService());

        var expired = await service.GetRecentlyExpiredAsync();

        Assert.Empty(expired);
    }
}
