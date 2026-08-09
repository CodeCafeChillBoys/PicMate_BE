using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhoneGrapher.Application.Interfaces;
using PhoneGrapher.Domain.Entities;
using PhoneGrapher.Infrastructure.Persistence;

namespace PhoneGrapher.Infrastructure.Services;

internal sealed class PayoutService(
    PhoneGrapherDbContext dbContext,
    ILogger<PayoutService> logger) : IPayoutService
{
    public async Task<GrapherWalletResponse> GetMyWalletAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        return new GrapherWalletResponse(
            profile.Balance,
            profile.BankName,
            profile.BankAccountNumber,
            profile.BankAccountName);
    }

    public async Task UpdateMyBankInfoAsync(Guid userId, string bankName, string accountNumber, string accountName, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        profile.BankName = bankName;
        profile.BankAccountNumber = accountNumber;
        profile.BankAccountName = accountName;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PayoutRequestResponse> RequestPayoutAsync(Guid userId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount < 100000m)
            throw new InvalidOperationException("Minimum payout amount is 100,000 VND.");

        var profile = await dbContext.GrapherProfiles
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        if (string.IsNullOrWhiteSpace(profile.BankName) || 
            string.IsNullOrWhiteSpace(profile.BankAccountNumber) || 
            string.IsNullOrWhiteSpace(profile.BankAccountName))
        {
            throw new InvalidOperationException("Bank information is required before requesting a payout.");
        }

        if (profile.Balance < amount)
            throw new InvalidOperationException("Insufficient balance.");

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Deduct balance
        profile.Balance -= amount;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        // Create request
        var request = new PayoutRequest
        {
            GrapherProfileId = profile.Id,
            Amount = amount,
            Status = PayoutStatus.Pending,
            BankName = profile.BankName,
            BankAccountNumber = profile.BankAccountNumber,
            BankAccountName = profile.BankAccountName,
        };

        dbContext.PayoutRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return ToResponse(request, profile.User.FullName);
    }

    public async Task<IReadOnlyList<PayoutRequestResponse>> GetMyPayoutRequestsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.GrapherProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Grapher profile not found.");

        var requests = await dbContext.PayoutRequests
            .AsNoTracking()
            .Include(x => x.GrapherProfile).ThenInclude(p => p.User)
            .Where(x => x.GrapherProfileId == profile.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return requests.Select(x => ToResponse(x, x.GrapherProfile.User.FullName)).ToList();
    }

    public async Task<IReadOnlyList<PayoutRequestResponse>> GetAllPayoutRequestsAsync(PayoutStatus? status, CancellationToken cancellationToken = default)
    {
        var query = dbContext.PayoutRequests
            .AsNoTracking()
            .Include(x => x.GrapherProfile).ThenInclude(p => p.User)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var requests = await query
            .OrderBy(x => x.Status == PayoutStatus.Pending ? 0 : 1)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return requests.Select(x => ToResponse(x, x.GrapherProfile.User.FullName)).ToList();
    }

    public async Task ResolvePayoutRequestAsync(Guid requestId, string proofImageUrl, string? adminNote, CancellationToken cancellationToken = default)
    {
        var request = await dbContext.PayoutRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException("Payout request not found.");

        if (request.Status != PayoutStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be resolved.");

        request.Status = PayoutStatus.Paid;
        request.ProofImageUrl = proofImageUrl;
        request.AdminNote = adminNote;
        request.ProcessedAt = DateTimeOffset.UtcNow;
        request.ResolvedAt = DateTimeOffset.UtcNow;
        request.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectPayoutRequestAsync(Guid requestId, string rejectReason, string? adminNote, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rejectReason))
            throw new InvalidOperationException("Reject reason is required.");

        var request = await dbContext.PayoutRequests
            .Include(x => x.GrapherProfile)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException("Payout request not found.");

        if (request.Status != PayoutStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be rejected.");

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        request.Status = PayoutStatus.Rejected;
        request.RejectReason = rejectReason;
        request.AdminNote = adminNote;
        request.ProcessedAt = DateTimeOffset.UtcNow;
        request.ResolvedAt = DateTimeOffset.UtcNow;
        request.UpdatedAt = DateTimeOffset.UtcNow;

        // Refund the balance
        request.GrapherProfile.Balance += request.Amount;
        request.GrapherProfile.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private static PayoutRequestResponse ToResponse(PayoutRequest request, string grapherName)
    {
        return new PayoutRequestResponse(
            request.Id,
            request.GrapherProfileId,
            grapherName,
            request.Amount,
            request.Status,
            request.BankName,
            request.BankAccountNumber,
            request.BankAccountName,
            request.ProofImageUrl,
            request.RejectReason,
            request.AdminNote,
            request.CreatedAt,
            request.ProcessedAt ?? request.ResolvedAt);
    }
}
