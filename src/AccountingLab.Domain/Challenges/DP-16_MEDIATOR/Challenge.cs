using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP16Mediator;

public sealed record InvoiceProcessingRequest(Guid InvoiceId, decimal Amount, bool IsValid, IReadOnlyList<string> Participants);
public sealed record InvoiceProcessingResult(Guid InvoiceId, bool Published, IReadOnlyList<string> ParticipantsInvoked);
public interface IInvoiceCoordinationChallenge { InvoiceProcessingResult Process(InvoiceProcessingRequest request); }

public sealed record InvoiceProcessingContext(Guid InvoiceId, decimal Amount, bool IsValid);
public enum InvoiceSignal { Validated, Rejected, Accounted, Published }

public interface IInvoiceMediator { void Notify(InvoiceComponent sender, InvoiceSignal signal); }

/// <summary>Colega: solo conoce al mediador, nunca a los demás participantes.</summary>
public abstract class InvoiceComponent(string name)
{
    private IInvoiceMediator? _mediator;

    public string Name { get; } = name;
    public void Attach(IInvoiceMediator mediator) => _mediator = mediator;
    public abstract void Execute(InvoiceProcessingContext invoice);

    protected void Report(InvoiceSignal signal)
    {
        if (_mediator is null) throw new InvalidOperationException("El participante no está registrado en un mediador.");
        _mediator.Notify(this, signal);
    }
}

public sealed class ValidationComponent() : InvoiceComponent("validacion")
{
    public override void Execute(InvoiceProcessingContext invoice) =>
        Report(invoice.IsValid ? InvoiceSignal.Validated : InvoiceSignal.Rejected);
}

public sealed class AccountingComponent() : InvoiceComponent("contabilizacion")
{
    public override void Execute(InvoiceProcessingContext invoice) => Report(InvoiceSignal.Accounted);
}

public sealed class PublicationComponent() : InvoiceComponent("publicacion")
{
    public override void Execute(InvoiceProcessingContext invoice) => Report(InvoiceSignal.Published);
}

/// <summary>Mediador: concentra el orden del flujo; los participantes se limitan a informar lo que ocurrió.</summary>
public sealed class InvoiceProcessingMediator : IInvoiceMediator
{
    private static readonly string[] Stages = ["validacion", "contabilizacion", "publicacion"];

    private readonly Dictionary<string, InvoiceComponent> _components = new(StringComparer.Ordinal);
    private readonly List<string> _invoked = new();
    private InvoiceProcessingContext? _current;
    private bool _published;

    public void Register(InvoiceComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!_components.TryAdd(component.Name, component))
            throw new ArgumentException($"El participante '{component.Name}' ya está registrado.", nameof(component));
        component.Attach(this);
    }

    public InvoiceProcessingResult Process(InvoiceProcessingContext invoice)
    {
        _invoked.Clear();
        _published = false;
        _current = invoice;
        RunFrom(0);
        return new InvoiceProcessingResult(invoice.InvoiceId, _published, _invoked.ToArray());
    }

    public void Notify(InvoiceComponent sender, InvoiceSignal signal)
    {
        switch (signal)
        {
            case InvoiceSignal.Validated: RunFrom(1); break;
            case InvoiceSignal.Accounted: RunFrom(2); break;
            case InvoiceSignal.Published: _published = true; break;
            case InvoiceSignal.Rejected: break; // una factura rechazada no se contabiliza ni se publica
        }
    }

    private void RunFrom(int stage)
    {
        for (; stage < Stages.Length; stage++)
        {
            if (!_components.TryGetValue(Stages[stage], out var component)) continue;
            _invoked.Add(component.Name);
            component.Execute(_current!);
            return;
        }
    }
}

public sealed class InvoiceCoordinationChallenge : IInvoiceCoordinationChallenge
{
    public InvoiceProcessingResult Process(InvoiceProcessingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Participants);
        MoneyPrecision.Require(request.Amount, nameof(request));

        var mediator = new InvoiceProcessingMediator();
        foreach (var name in request.Participants) mediator.Register(CreateParticipant(name));
        return mediator.Process(new InvoiceProcessingContext(request.InvoiceId, request.Amount, request.IsValid));
    }

    private static InvoiceComponent CreateParticipant(string? name) => name?.ToLowerInvariant() switch
    {
        "validacion" => new ValidationComponent(),
        "contabilizacion" => new AccountingComponent(),
        "publicacion" => new PublicationComponent(),
        _ => throw new ArgumentException($"Participante no admitido: {name}.")
    };
}
