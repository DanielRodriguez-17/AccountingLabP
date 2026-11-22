using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP14Command;

public sealed record AccountingOperationRequest(Guid CompanyId, Guid OperationId, string Kind, Guid TargetId, decimal Amount);
public sealed record AccountingOperationReceipt(Guid OperationId, Guid TargetId, string Kind, decimal AppliedAmount, decimal RemainingAmount, Guid LinkedJournalEntryId, bool Replayed);
public sealed record AccountingOperationContext(decimal OutstandingAmount, bool TargetIsPublished, IReadOnlyList<AccountingOperationReceipt> CompletedOperations);
public interface IIdentifiedOperationChallenge { AccountingOperationReceipt Execute(AccountingOperationRequest request, AccountingOperationContext context); }

/// <summary>Comando: una solicitud empaquetada con todo lo necesario para ejecutarse más tarde y en otro lugar.</summary>
public abstract class AccountingCommand(AccountingOperationRequest request)
{
    public AccountingOperationRequest Request { get; } = request;
    public abstract string Kind { get; }
    public abstract AccountingOperationReceipt Execute(AccountingOperationContext context);

    protected void RequirePositiveAmount()
    {
        MoneyPrecision.Require(Request.Amount, nameof(Request));
        if (Request.Amount == 0) throw new ArgumentException("El importe de la operación debe ser mayor que cero.", nameof(Request));
    }
}

/// <summary>Aplica un cobro o pago a la obligación: reduce el saldo y deja un asiento vinculado.</summary>
public sealed class ApplyPaymentCommand(AccountingOperationRequest request) : AccountingCommand(request)
{
    public override string Kind => "apply-payment";

    public override AccountingOperationReceipt Execute(AccountingOperationContext context)
    {
        RequirePositiveAmount();
        if (Request.Amount > context.OutstandingAmount)
            throw new InvalidOperationException("El importe excede el saldo pendiente de la obligación.");
        return new AccountingOperationReceipt(
            Request.OperationId, Request.TargetId, Kind, Request.Amount,
            context.OutstandingAmount - Request.Amount, Guid.NewGuid(), false);
    }
}

/// <summary>Reversión vinculada: solo procede sobre un comprobante publicado y conserva el vínculo con el original.</summary>
public sealed class ReverseVoucherCommand(AccountingOperationRequest request) : AccountingCommand(request)
{
    public override string Kind => "reverse-voucher";

    public override AccountingOperationReceipt Execute(AccountingOperationContext context)
    {
        RequirePositiveAmount();
        if (!context.TargetIsPublished)
            throw new InvalidOperationException("Solo un comprobante publicado puede revertirse; un borrador no es un hecho contabilizado.");
        return new AccountingOperationReceipt(
            Request.OperationId, Request.TargetId, Kind, Request.Amount,
            context.OutstandingAmount, Guid.NewGuid(), false);
    }
}

public static class AccountingCommandFactory
{
    public static AccountingCommand Create(AccountingOperationRequest request) => request.Kind switch
    {
        "apply-payment" => new ApplyPaymentCommand(request),
        "reverse-voucher" => new ReverseVoucherCommand(request),
        _ => throw new ArgumentException($"Tipo de operación no admitido: {request.Kind}.", nameof(request))
    };
}

/// <summary>
/// Invocador: reconoce operaciones ya ejecutadas por su identificación y devuelve su efecto original
/// en lugar de volver a producirlo; solo ejecuta el comando cuando la operación es nueva.
/// </summary>
public sealed class IdentifiedOperationChallenge : IIdentifiedOperationChallenge
{
    public AccountingOperationReceipt Execute(AccountingOperationRequest request, AccountingOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (request.CompanyId == Guid.Empty || request.OperationId == Guid.Empty || request.TargetId == Guid.Empty)
            throw new ArgumentException("La empresa, la operación y el objetivo son obligatorios.", nameof(request));

        var command = AccountingCommandFactory.Create(request);

        var previous = (context.CompletedOperations ?? Array.Empty<AccountingOperationReceipt>())
            .FirstOrDefault(receipt => receipt.OperationId == request.OperationId);
        if (previous is not null)
        {
            if (previous.Kind != command.Kind || previous.TargetId != request.TargetId || previous.AppliedAmount != request.Amount)
                throw new InvalidOperationException("La identificación de la operación ya se utilizó en una solicitud distinta.");
            return previous with { Replayed = true };
        }

        return command.Execute(context);
    }
}
