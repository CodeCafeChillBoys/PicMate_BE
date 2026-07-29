using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhoneGrapher.Infrastructure.Persistence;

public sealed class PhoneGrapherDbContextFactory : IDesignTimeDbContextFactory<PhoneGrapherDbContext>
{
    public PhoneGrapherDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PhoneGrapherDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=PicMateDB;Username=postgres;Password=12345")
            .Options;

        return new PhoneGrapherDbContext(options);
    }
}
