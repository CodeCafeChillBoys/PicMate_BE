using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PhoneGrapher.Domain.Entities;

namespace PhoneGrapher.Application.Interfaces;

public record PayoutRequestResponse(
    Guid Id,
    Guid GrapherProfileId,
    string GrapherName,
    decimal Amount,
    PayoutStatus Status,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName,
    string? ProofImageUrl,
    string? RejectReason,
    string? AdminNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);

public record GrapherWalletResponse(
    decimal Balance,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName);

public interface IPayoutService
{
    Task<GrapherWalletResponse> GetMyWalletAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateMyBankInfoAsync(Guid userId, string bankName, string accountNumber, string accountName, CancellationToken cancellationToken = default);
    Task<PayoutRequestResponse> RequestPayoutAsync(Guid userId, decimal amount, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayoutRequestResponse>> GetMyPayoutRequestsAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<PayoutRequestResponse>> GetAllPayoutRequestsAsync(PayoutStatus? status, CancellationToken cancellationToken = default);
    Task ResolvePayoutRequestAsync(Guid requestId, string proofImageUrl, string? adminNote, CancellationToken cancellationToken = default);
    Task RejectPayoutRequestAsync(Guid requestId, string rejectReason, string? adminNote, CancellationToken cancellationToken = default);
}
