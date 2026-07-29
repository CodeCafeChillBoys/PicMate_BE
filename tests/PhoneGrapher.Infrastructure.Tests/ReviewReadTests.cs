using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;
using PhoneGrapher.Infrastructure.Services;

namespace PhoneGrapher.Infrastructure.Tests;

public class ReviewReadTests
{
    /// <summary>Tạo một booking đã hoàn thành kèm review, để có đủ chuỗi Review → Booking → ServicePackage.</summary>
    private static async Task<Review> AddReviewAsync(
        PhoneGrapherDbContext db,
        SeedResult seed,
        int rating,
        string comment,
        DateTimeOffset createdAt,
        Guid? grapherProfileId = null,
        string packageName = "Goi test")
    {
        var package = new GrapherServicePackage
        {
            GrapherProfileId = grapherProfileId ?? seed.GrapherProfileId,
            Name = packageName,
            Description = "Mo ta",
            Price = 500_000m,
            DurationMinutes = 60,
            IsActive = true
        };

        var booking = new Booking
        {
            CustomerId = seed.CustomerId,
            GrapherProfileId = grapherProfileId ?? seed.GrapherProfileId,
            ServicePackage = package,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-5),
            DurationMinutes = 60,
            Location = "Ho Guom",
            Status = BookingStatus.Completed,
            TotalAmount = 500_000m,
            PlatformFeeAmount = 75_000m,
            GrapherPayoutAmount = 425_000m
        };

        var review = new Review
        {
            Booking = booking,
            CustomerId = seed.CustomerId,
            GrapherProfileId = grapherProfileId ?? seed.GrapherProfileId,
            Rating = rating,
            Comment = comment,
            CreatedAt = createdAt
        };

        db.GrapherServicePackages.Add(package);
        db.Bookings.Add(booking);
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
        return review;
    }

    // ── GetFeaturedAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetFeaturedAsync_LoaiReviewDuoiBonSao()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddReviewAsync(db, seed, 3, "Tam duoc", DateTimeOffset.UtcNow);
        await AddReviewAsync(db, seed, 4, "Kha on", DateTimeOffset.UtcNow);
        await AddReviewAsync(db, seed, 5, "Rat tot", DateTimeOffset.UtcNow);

        var result = await new ReviewService(db).GetFeaturedAsync();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, x => x.Rating < 4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetFeaturedAsync_LoaiReviewKhongCoNoiDung(string comment)
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddReviewAsync(db, seed, 5, comment, DateTimeOffset.UtcNow);

        var result = await new ReviewService(db).GetFeaturedAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFeaturedAsync_SapXepMoiNhatTruoc()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddReviewAsync(db, seed, 5, "Cu nhat", DateTimeOffset.UtcNow.AddDays(-10));
        await AddReviewAsync(db, seed, 5, "Moi nhat", DateTimeOffset.UtcNow);
        await AddReviewAsync(db, seed, 5, "O giua", DateTimeOffset.UtcNow.AddDays(-5));

        var result = await new ReviewService(db).GetFeaturedAsync();

        Assert.Equal(["Moi nhat", "O giua", "Cu nhat"], result.Select(x => x.Text));
    }

    [Fact]
    public async Task GetFeaturedAsync_KhongTraQuaSoLuongYeuCau()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        for (var i = 0; i < 9; i++)
        {
            await AddReviewAsync(db, seed, 5, $"Review {i}", DateTimeOffset.UtcNow.AddMinutes(-i));
        }

        var result = await new ReviewService(db).GetFeaturedAsync();

        Assert.Equal(6, result.Count);
    }

    [Fact]
    public async Task GetFeaturedAsync_RoleLaTenGoiDichVuDaDat()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddReviewAsync(db, seed, 5, "Anh dep", DateTimeOffset.UtcNow, packageName: "Chup dao pho");

        var result = await new ReviewService(db).GetFeaturedAsync();

        Assert.Equal("Chup dao pho", result[0].Role);
        Assert.Equal("Khach Hang Test", result[0].Name);
    }

    // ── GetForGrapherAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetForGrapherAsync_ChiTraReviewCuaDungTho()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);

        var otherGrapherUser = new User
        {
            FullName = "Tho Khac",
            Email = "thokhac@test.local",
            PhoneNumber = "0900000009",
            PasswordHash = "hash",
            Role = UserRole.Grapher
        };
        var otherProfile = new GrapherProfile
        {
            User = otherGrapherUser,
            Bio = "Bio",
            Location = "Da Nang",
            IsVerified = true
        };
        db.GrapherProfiles.Add(otherProfile);
        await db.SaveChangesAsync();

        await AddReviewAsync(db, seed, 5, "Cua tho A", DateTimeOffset.UtcNow);
        await AddReviewAsync(db, seed, 5, "Cua tho B", DateTimeOffset.UtcNow, otherProfile.Id);

        var result = await new ReviewService(db).GetForGrapherAsync(seed.GrapherProfileId);

        Assert.Single(result);
        Assert.Equal("Cua tho A", result[0].Text);
    }

    [Fact]
    public async Task GetForGrapherAsync_TraCaReviewDiemThapVaSapXepMoiNhatTruoc()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);
        await AddReviewAsync(db, seed, 1, "Khong hai long", DateTimeOffset.UtcNow);
        await AddReviewAsync(db, seed, 5, "Rat tot", DateTimeOffset.UtcNow.AddDays(-3));

        var result = await new ReviewService(db).GetForGrapherAsync(seed.GrapherProfileId);

        Assert.Equal(2, result.Count);
        Assert.Equal("Khong hai long", result[0].Text);
        Assert.Equal("Khach Hang Test", result[0].User);
    }

    [Fact]
    public async Task GetForGrapherAsync_ThoChuaCoReview_TraMangRong()
    {
        using var db = TestDb.Create();
        var seed = await TestDb.SeedAsync(db);

        var result = await new ReviewService(db).GetForGrapherAsync(seed.GrapherProfileId);

        Assert.Empty(result);
    }
}
