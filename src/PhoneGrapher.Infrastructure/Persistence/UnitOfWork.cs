using PhoneGrapher.Application.Abstractions;

namespace PhoneGrapher.Infrastructure.Persistence;

public sealed class UnitOfWork(PhoneGrapherDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
