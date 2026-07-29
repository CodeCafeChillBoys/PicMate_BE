using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Domain.Enums;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Tests;

internal sealed record SeedResult(
    Guid CustomerId,
    Guid GrapherProfileId,
    Guid GrapherUserId,
    Guid ServicePackageId,
    Guid AdminId);

internal static class TestDb
{
    public static PhoneGrapherDbContext Create()
    {
        var options = new DbContextOptionsBuilder<PhoneGrapherDbContext>()
            .UseInMemoryDatabase($"picmate-{Guid.NewGuid()}")
            // Provider InMemory không có transaction thật; code nghiệp vụ vẫn gọi
            // BeginTransactionAsync nên phải tắt cảnh báo này, nếu không test sẽ ném exception.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PhoneGrapherDbContext(options);
    }

    public static async Task<SeedResult> SeedAsync(PhoneGrapherDbContext db)
    {
        var customer = new User
        {
            FullName = "Khach Hang Test",
            Email = "khach@test.local",
            PhoneNumber = "0900000001",
            PasswordHash = "hash",
            Role = UserRole.Customer
        };

        var grapherUser = new User
        {
            FullName = "Tho Chup Test",
            Email = "tho@test.local",
            PhoneNumber = "0900000002",
            PasswordHash = "hash",
            Role = UserRole.Grapher
        };

        var admin = new User
        {
            FullName = "Admin Test",
            Email = "admin@test.local",
            PhoneNumber = "0900000003",
            PasswordHash = "hash",
            Role = UserRole.Admin
        };

        var profile = new GrapherProfile
        {
            User = grapherUser,
            Bio = "Bio test",
            Location = "Ha Noi",
            IsVerified = true,
            KycStatus = KycStatus.Approved
        };

        var package = new GrapherServicePackage
        {
            GrapherProfile = profile,
            Name = "Goi test",
            Description = "Mo ta test",
            Price = 1_500_000m,
            DurationMinutes = 60,
            IsActive = true
        };

        db.Users.AddRange(customer, grapherUser, admin);
        db.GrapherProfiles.Add(profile);
        db.GrapherServicePackages.Add(package);
        db.SystemSettings.Add(new SystemSettings());
        await db.SaveChangesAsync();

        return new SeedResult(customer.Id, profile.Id, grapherUser.Id, package.Id, admin.Id);
    }
}
