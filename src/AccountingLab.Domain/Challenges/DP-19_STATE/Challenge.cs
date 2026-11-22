using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP19State;

public sealed record InvoiceLifecycleRequest(string CurrentState, string Action, decimal OutstandingAmount, DateOnly DueDate, DateOnly BusinessDate);
public sealed record InvoiceTransitionResult(string State, bool PaymentAllowed);
public interface IInvoiceLifecycleChallenge { InvoiceTransitionResult Transition(InvoiceLifecycleRequest request); }

/// <summary>
/// Estado: cada acción devuelve el estado siguiente. Por defecto una acción no está permitida;
/// cada estado concreto declara únicamente las transiciones válidas desde él.
/// </summary>
public abstract class InvoiceState
{
    public abstract string Name { get; }
    public virtual bool PaymentAllowed => false;

    public virtual InvoiceState Emit(InvoiceLifecycleRequest request) => throw Refuse("emitirse");
    public virtual InvoiceState Pay(InvoiceLifecycleRequest request) => throw Refuse("pagarse");
    public virtual InvoiceState MarkOverdue(InvoiceLifecycleRequest request) => throw Refuse("marcarse como vencida");
    public virtual InvoiceState Cancel(InvoiceLifecycleRequest request) => throw Refuse("cancelarse");

    protected InvalidOperationException Refuse(string action) =>
        new($"Una factura en estado '{Name}' no puede {action}.");
}

public sealed class DraftInvoiceState : InvoiceState
{
    public override string Name => "draft";
    public override InvoiceState Emit(InvoiceLifecycleRequest request) => InvoiceStates.Issued;
    public override InvoiceState Cancel(InvoiceLifecycleRequest request) => InvoiceStates.Cancelled;
}

public sealed class IssuedInvoiceState : InvoiceState
{
    public override string Name => "issued";
    public override bool PaymentAllowed => true;
    // Un pago parcial deja la factura emitida; solo el saldo cero la cierra como pagada.
    public override InvoiceState Pay(InvoiceLifecycleRequest request) => request.OutstandingAmount == 0 ? InvoiceStates.Paid : this;
    public override InvoiceState MarkOverdue(InvoiceLifecycleRequest request) => request.BusinessDate > request.DueDate ? InvoiceStates.Overdue : this;
    public override InvoiceState Cancel(InvoiceLifecycleRequest request) => InvoiceStates.Cancelled;
}

public sealed class OverdueInvoiceState : InvoiceState
{
    public override string Name => "overdue";
    public override bool PaymentAllowed => true;
    public override InvoiceState Pay(InvoiceLifecycleRequest request) => request.OutstandingAmount == 0 ? InvoiceStates.Paid : this;
    public override InvoiceState MarkOverdue(InvoiceLifecycleRequest request) => this;
    public override InvoiceState Cancel(InvoiceLifecycleRequest request) => InvoiceStates.Cancelled;
}

public sealed class PaidInvoiceState : InvoiceState
{
    public override string Name => "paid";
}

public sealed class CancelledInvoiceState : InvoiceState
{
    public override string Name => "cancelled";
}

/// <summary>Los estados no guardan datos, por lo que se comparten como instancias únicas.</summary>
public static class InvoiceStates
{
    public static readonly InvoiceState Draft = new DraftInvoiceState();
    public static readonly InvoiceState Issued = new IssuedInvoiceState();
    public static readonly InvoiceState Overdue = new OverdueInvoiceState();
    public static readonly InvoiceState Paid = new PaidInvoiceState();
    public static readonly InvoiceState Cancelled = new CancelledInvoiceState();

    public static InvoiceState FromName(string? name) => name?.ToLowerInvariant() switch
    {
        "draft" => Draft,
        "issued" => Issued,
        "overdue" => Overdue,
        "paid" => Paid,
        "cancelled" => Cancelled,
        _ => throw new ArgumentException($"Estado de factura no admitido: {name}.")
    };
}

/// <summary>Contexto: traduce la acción solicitada en una llamada al estado actual.</summary>
public sealed class InvoiceLifecycleChallenge : IInvoiceLifecycleChallenge
{
    public InvoiceTransitionResult Transition(InvoiceLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        MoneyPrecision.Require(request.OutstandingAmount, nameof(request));

        var state = InvoiceStates.FromName(request.CurrentState);
        var next = request.Action?.ToLowerInvariant() switch
        {
            "emit" => state.Emit(request),
            "pay" => state.Pay(request),
            "mark-overdue" => state.MarkOverdue(request),
            "cancel" => state.Cancel(request),
            _ => throw new ArgumentException($"Acción de factura no admitida: {request.Action}.", nameof(request))
        };
        return new InvoiceTransitionResult(next.Name, next.PaymentAllowed);
    }
}
