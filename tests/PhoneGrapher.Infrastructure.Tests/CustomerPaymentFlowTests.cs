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

public class CustomerPaymentFlowTests
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
        FakeEmailService? email = null,
        FakeNotificationService? notifications = null)
    {
        var options = MsOptions.Create(VietQrConfig());
        return new PaymentReconciliationService(
            db,
            new VietQrService(options),
            options,
            new PaymentSucceededNotifier(
                db,
                email ?? new FakeEmailService(),
                notifications ?? new FakeNotificationService(),
                NullLogger<PaymentSucceededNotifier>.Instance),
            NullLogger<PaymentReconciliationService>.Instance);
    }

    private static async Task<(SeedResult Seed, Guid BookingId)> ArrangeVietQrBookingAsync(PhoneGrapherDbContext db)
    {
        var seed = await TestDb.SeedAsync(db);
        var booking = new BookingService(
            db, new FakeVnPayService(), new FakeEmailService(), new FakeNotificationService(),
            NullLogger<BookingService>.Instance, MsOptions.Create(VietQrConfig()));

        var response = await booking.CreateBookingAsync(
            seed.CustomerId,
            new CreateBookingRequest(seed.GrapherProfileId, seed.ServicePackageId,
                DateTimeOffset.UtcNow.AddDays(3), "Ho Guom, Ha Noi", null, "vietqr"),
            "127.0.0.1");

        return (seed, response.Booking.Id);
    }

    [Fact]
    public async Task GetVietQrAsync_TraVeChuoiQrVaThongTinTaiKhoan()
    {
        using var db = TestDb.Create();
        var (seed, bookingId) = await ArrangeVietQrBookingAsync(db);

        var result = await CreateService(db).GetVietQrAsync(bookingId, seed.CustomerId);

        Assert.Equal("970405", result.BankBin);
        Assert.Equal("5402205457446", result.AccountNumber);
        Assert.Equal("NGUYEN VAN A", result.AccountName);
        Assert.Equal(1_500_000m, result.Amount);
        Assert.StartsWith("PIC", result.Memo, StringComparison.Ordinal);
        Assert.Contains(result.Memo, result.QrPayload, StringComparison.Ordinal);
        Assert.Equal("Pending", result.PaymentStatus);
    }

    [Fact]
    public async Task GetVietQrAsync_KhachKhac_NemUnauthorizedAccessException()
    {
        using var db = TestDb.Create();
        var (_, bookingId) = await ArrangeVietQrBookingAsync(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetVietQrAsync(bookingId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetVietQrAsync_BookingKhongTonTai_NemKeyNotFoundException()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetVietQrAsync(Guid.NewGuid(), seed.CustomerId));
    }

    [Fact]
    public async Task GetVietQrAsync_DonVnPay_NemInvalidOperationException()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var booking = new BookingService(
            db, new FakeVnPayService(), new FakeEmailService(), new FakeNotificationService(),
            NullLogger<BookingService>.Instance, MsOptions.Create(VietQrConfig()));
        var created = await booking.CreateBookingAsync(
            seed.CustomerId,
            new CreateBookingRequest(seed.GrapherProfileId, seed.ServicePackageId,
                DateTimeOffset.UtcNow.AddDays(3), "Ho Guom", null, "vnpay"),
            "127.0.0.1");
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetVietQrAsync(created.Booking.Id, seed.CustomerId));
    }

    [Fact]
    public async Task GetVietQrAsync_DonDaBiTuChoi_VanTraVeTrangThaiVaLyDo()
    {
        using var db = TestDb.Create();
        var (seed, bookingId) = await ArrangeVietQrBookingAsync(db);
        var payment = await db.PaymentTransactions.SingleAsync();
        payment.Status = PaymentStatus.Failed;
        payment.VerificationNote = "Không tìm thấy giao dịch trong sao kê.";
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetVietQrAsync(bookingId, seed.CustomerId);

        Assert.Equal("Failed", result.PaymentStatus);
        Assert.Equal("Không tìm thấy giao dịch trong sao kê.", result.VerificationNote);
        Assert.Equal(string.Empty, result.QrPayload);
    }

    [Fact]
    public async Task ClaimTransferredAsync_ChuyenSangAwaitingVerificationVaGhiThoiDiem()
    {
        using var db = TestDb.Create();
        var (seed, bookingId) = await ArrangeVietQrBookingAsync(db);

        var result = await CreateService(db).ClaimTransferredAsync(bookingId, seed.CustomerId);

        var payment = await db.PaymentTransactions.SingleAsync();

        Assert.Equal("AwaitingVerification", result.PaymentStatus);
        Assert.Equal(PaymentStatus.AwaitingVerification, payment.Status);
        Assert.NotNull(payment.CustomerClaimedAt);
    }

    [Fact]
    public async Task ClaimTransferredAsync_GoiLaiLanHai_KhongDoiThoiDiemDaGhi()
    {
        using var db = TestDb.Create();
        var (seed, bookingId) = await ArrangeVietQrBookingAsync(db);
        var service = CreateService(db);

        await service.ClaimTransferredAsync(bookingId, seed.CustomerId);
        var firstClaimedAt = (await db.PaymentTransactions.SingleAsync()).CustomerClaimedAt;

        await service.ClaimTransferredAsync(bookingId, seed.CustomerId);
        var secondClaimedAt = (await db.PaymentTransactions.SingleAsync()).CustomerClaimedAt;

        Assert.Equal(firstClaimedAt, secondClaimedAt);
    }

    [Fact]
    public async Task ClaimTransferredAsync_DonDaThanhToan_NemInvalidOperationException()
    {
        using var db = TestDb.Create();
        var (seed, bookingId) = await ArrangeVietQrBookingAsync(db);
        var payment = await db.PaymentTransactions.SingleAsync();
        payment.Status = PaymentStatus.Succeeded;
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ClaimTransferredAsync(bookingId, seed.CustomerId));
    }

    [Fact]
    public async Task GetStatusAsync_TraVeTrangThaiHienTaiVaGhiChuTuChoi()
    {
        using var db = TestDb.Create();
        var (seed, bookingId) = await ArrangeVietQrBookingAsync(db);
        var payment = await db.PaymentTransactions.SingleAsync();
        payment.Status = PaymentStatus.Failed;
        payment.VerificationNote = "Không tìm thấy giao dịch trong sao kê.";
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetStatusAsync(bookingId, seed.CustomerId);

        Assert.Equal("Failed", result.PaymentStatus);
        Assert.Equal("Không tìm thấy giao dịch trong sao kê.", result.VerificationNote);
    }
}
