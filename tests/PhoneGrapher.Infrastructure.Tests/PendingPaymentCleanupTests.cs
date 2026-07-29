using Microsoft.EntityFrameworkCore;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;
using PhoneGrapher.Infrastructure.Services;

namespace PhoneGrapher.Infrastructure.Tests;

public class PendingPaymentCleanupTests
{
    private static async Task<PaymentTransaction> AddPaymentAsync(
        PhoneGrapherDbContext db,
        SeedResult seed,
        PaymentProvider provider,
        PaymentStatus status,
        DateTimeOffset? expiresAt,
        DateTimeOffset? claimedAt)
    {
        var booking = new Booking
        {
            CustomerId = seed.CustomerId,
            GrapherProfileId = seed.GrapherProfileId,
            ServicePackageId = seed.ServicePackageId,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(3),
            DurationMinutes = 60,
            Location = "Ho Guom",
            Status = BookingStatus.PendingPayment,
            TotalAmount = 1_500_000m,
            PlatformFeeAmount = 225_000m,
            GrapherPayoutAmount = 1_275_000m
        };

        var payment = new PaymentTransaction
        {
            Booking = booking,
            Provider = provider,
            Status = status,
            Amount = booking.TotalAmount,
            PlatformFeeAmount = booking.PlatformFeeAmount,
            GrapherPayoutAmount = booking.GrapherPayoutAmount,
            TransactionCode = $"PIC{Random.Shared.Next(100_000_000, 1_000_000_000)}",
            ExpiresAt = expiresAt,
            CustomerClaimedAt = claimedAt
        };

        db.Bookings.Add(booking);
        db.PaymentTransactions.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    [Fact]
    public async Task CancelExpiredAsync_HuyDonVietQrQuaHanChuaBaoChuyenKhoan()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var payment = await AddPaymentAsync(db, seed, PaymentProvider.VietQr, PaymentStatus.Pending,
            DateTimeOffset.UtcNow.AddMinutes(-1), null);

        var cancelled = await PendingPaymentCleanupService.CancelExpiredAsync(db, CancellationToken.None);

        var updated = await db.PaymentTransactions.Include(x => x.Booking).SingleAsync(x => x.Id == payment.Id);

        Assert.Equal(1, cancelled);
        Assert.Equal(PaymentStatus.Failed, updated.Status);
        Assert.Equal(BookingStatus.Cancelled, updated.Booking.Status);
        Assert.Equal("Quá hạn thanh toán", updated.Booking.CancellationReason);
    }

    [Fact]
    public async Task CancelExpiredAsync_KhongDungDonKhachDaBaoDaChuyenKhoan()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var payment = await AddPaymentAsync(db, seed, PaymentProvider.VietQr, PaymentStatus.AwaitingVerification,
            DateTimeOffset.UtcNow.AddMinutes(-30), DateTimeOffset.UtcNow.AddMinutes(-25));

        var cancelled = await PendingPaymentCleanupService.CancelExpiredAsync(db, CancellationToken.None);

        var updated = await db.PaymentTransactions.Include(x => x.Booking).SingleAsync(x => x.Id == payment.Id);

        Assert.Equal(0, cancelled);
        Assert.Equal(PaymentStatus.AwaitingVerification, updated.Status);
        Assert.Equal(BookingStatus.PendingPayment, updated.Booking.Status);
    }

    [Fact]
    public async Task CancelExpiredAsync_KhongDungDonPendingCoClaimedAt()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddPaymentAsync(db, seed, PaymentProvider.VietQr, PaymentStatus.Pending,
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(-2));

        var cancelled = await PendingPaymentCleanupService.CancelExpiredAsync(db, CancellationToken.None);

        Assert.Equal(0, cancelled);
    }

    [Fact]
    public async Task CancelExpiredAsync_KhongDungDonChuaQuaHan()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddPaymentAsync(db, seed, PaymentProvider.VietQr, PaymentStatus.Pending,
            DateTimeOffset.UtcNow.AddMinutes(10), null);

        var cancelled = await PendingPaymentCleanupService.CancelExpiredAsync(db, CancellationToken.None);

        Assert.Equal(0, cancelled);
    }

    [Theory]
    [InlineData(PaymentProvider.VnPay)]
    [InlineData(PaymentProvider.Cod)]
    public async Task CancelExpiredAsync_KhongDungDonVnPayHoacCod(PaymentProvider provider)
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddPaymentAsync(db, seed, provider, PaymentStatus.Pending,
            DateTimeOffset.UtcNow.AddMinutes(-1), null);

        var cancelled = await PendingPaymentCleanupService.CancelExpiredAsync(db, CancellationToken.None);

        Assert.Equal(0, cancelled);
    }
}
