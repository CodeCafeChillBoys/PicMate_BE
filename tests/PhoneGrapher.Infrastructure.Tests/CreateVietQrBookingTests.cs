using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PhoneGrapher.Application.Dtos;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Options;
using PhoneGrapher.Infrastructure.Persistence;
using PhoneGrapher.Infrastructure.Services;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace PhoneGrapher.Infrastructure.Tests;

public class CreateVietQrBookingTests
{
    private static BookingService CreateService(PhoneGrapherDbContext db) => new(
        db,
        new FakeVnPayService(),
        new FakeEmailService(),
        new FakeNotificationService(),
        NullLogger<BookingService>.Instance,
        MsOptions.Create(new VietQrOptions { ExpiryMinutes = 15 }));

    private static CreateBookingRequest Request(SeedResult seed, string method) => new(
        seed.GrapherProfileId,
        seed.ServicePackageId,
        DateTimeOffset.UtcNow.AddDays(3),
        "Ho Guom, Ha Noi",
        "Ghi chu test",
        method);

    [Fact]
    public async Task CreateBookingAsync_VietQr_TaoGiaoDichDungProviderVaTrangThai()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);

        var response = await CreateService(db).CreateBookingAsync(seed.CustomerId, Request(seed, "vietqr"), "127.0.0.1");

        var payment = await db.PaymentTransactions.SingleAsync();
        var booking = await db.Bookings.SingleAsync();

        Assert.Equal(PaymentProvider.VietQr, payment.Provider);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(string.Empty, response.PaymentUrl);
    }

    [Fact]
    public async Task CreateBookingAsync_VietQr_SinhMaGiaoDichDungDinhDangPicVaChinChuSo()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);

        await CreateService(db).CreateBookingAsync(seed.CustomerId, Request(seed, "vietqr"), "127.0.0.1");

        var payment = await db.PaymentTransactions.SingleAsync();

        Assert.Matches(new Regex(@"^PIC\d{9}$"), payment.TransactionCode);
    }

    [Fact]
    public async Task CreateBookingAsync_VietQr_DatHanThanhToanTheoCauHinh()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var before = DateTimeOffset.UtcNow;

        await CreateService(db).CreateBookingAsync(seed.CustomerId, Request(seed, "vietqr"), "127.0.0.1");

        var payment = await db.PaymentTransactions.SingleAsync();

        Assert.NotNull(payment.ExpiresAt);
        Assert.InRange(payment.ExpiresAt!.Value, before.AddMinutes(15), before.AddMinutes(16));
    }

    [Fact]
    public async Task CreateBookingAsync_VietQr_ChuaBaoThoViChuaCoTien()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        var notifications = new FakeNotificationService();
        var service = new BookingService(
            db, new FakeVnPayService(), new FakeEmailService(), notifications,
            NullLogger<BookingService>.Instance, MsOptions.Create(new VietQrOptions { ExpiryMinutes = 15 }));

        await service.CreateBookingAsync(seed.CustomerId, Request(seed, "vietqr"), "127.0.0.1");

        Assert.Empty(notifications.Created);
    }

    [Fact]
    public async Task CreateBookingAsync_VnPay_VanGiuNguyenHanhViCu()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);

        var response = await CreateService(db).CreateBookingAsync(seed.CustomerId, Request(seed, "vnpay"), "127.0.0.1");

        var payment = await db.PaymentTransactions.SingleAsync();

        Assert.Equal(PaymentProvider.VnPay, payment.Provider);
        Assert.StartsWith("PG", payment.TransactionCode, StringComparison.Ordinal);
        Assert.Null(payment.ExpiresAt);
        Assert.Equal("https://sandbox.vnpayment.vn/fake", response.PaymentUrl);
    }

    [Fact]
    public async Task CreateBookingAsync_Cod_VanGiuNguyenHanhViCu()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);

        var response = await CreateService(db).CreateBookingAsync(seed.CustomerId, Request(seed, "cod"), "127.0.0.1");

        var payment = await db.PaymentTransactions.SingleAsync();
        var booking = await db.Bookings.SingleAsync();

        Assert.Equal(PaymentProvider.Cod, payment.Provider);
        Assert.Equal(BookingStatus.PendingConfirmation, booking.Status);
        Assert.Equal(string.Empty, response.PaymentUrl);
    }
}
