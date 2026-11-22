using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP13ChainOfResponsibility;

public sealed record PaymentApprovalRequest(decimal Amount, decimal AvailableFunds, bool PolicyAllows, decimal AuthorizationLimit, IReadOnlyList<string> Checks);
public sealed record PaymentApprovalDecision(bool Approved, string? RejectedBy, IReadOnlyList<string> VisitedChecks);
public interface IPaymentApprovalChallenge { PaymentApprovalDecision Evaluate(PaymentApprovalRequest request); }

/// <summary>
/// Eslabón de la cadena: aplica su control; si lo supera, pasa la solicitud al siguiente.
/// Si lo incumple, la cadena se detiene y la decisión nombra a quien rechazó.
/// </summary>
public abstract class PaymentCheckHandler
{
    private PaymentCheckHandler? _next;

    public abstract string Name { get; }
    protected abstract bool Allows(PaymentApprovalRequest request);

    public PaymentCheckHandler SetNext(PaymentCheckHandler next)
    {
        _next = next;
        return next;
    }

    public PaymentApprovalDecision Handle(PaymentApprovalRequest request, IReadOnlyList<string> visited)
    {
        string[] path = [.. visited, Name];
        if (!Allows(request)) return new PaymentApprovalDecision(false, Name, path);
        return _next is null ? new PaymentApprovalDecision(true, null, path) : _next.Handle(request, path);
    }
}

public sealed class AvailableFundsHandler : PaymentCheckHandler
{
    public override string Name => "fondos";
    protected override bool Allows(PaymentApprovalRequest request) => request.Amount <= request.AvailableFunds;
}

public sealed class PaymentPolicyHandler : PaymentCheckHandler
{
    public override string Name => "politica";
    protected override bool Allows(PaymentApprovalRequest request) => request.PolicyAllows;
}

public sealed class AuthorizationLimitHandler : PaymentCheckHandler
{
    public override string Name => "limite";
    protected override bool Allows(PaymentApprovalRequest request) => request.Amount <= request.AuthorizationLimit;
}

public sealed class PaymentApprovalChallenge : IPaymentApprovalChallenge
{
    private static readonly IReadOnlyDictionary<string, Func<PaymentCheckHandler>> Handlers =
        new Dictionary<string, Func<PaymentCheckHandler>>(StringComparer.OrdinalIgnoreCase)
        {
            ["fondos"] = () => new AvailableFundsHandler(),
            ["politica"] = () => new PaymentPolicyHandler(),
            ["limite"] = () => new AuthorizationLimitHandler()
        };

    public PaymentApprovalDecision Evaluate(PaymentApprovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Checks);
        MoneyPrecision.Require(request.Amount, nameof(request));

        // El orden de la cadena lo define la configuración de la solicitud.
        var chain = new List<PaymentCheckHandler>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var check in request.Checks)
        {
            if (check is null || !Handlers.TryGetValue(check, out var create))
                throw new ArgumentException($"Control de aprobación no admitido: {check}.", nameof(request));
            if (!names.Add(check))
                throw new ArgumentException($"El control '{check}' se configuró más de una vez.", nameof(request));
            chain.Add(create());
        }

        if (chain.Count == 0) return new PaymentApprovalDecision(true, null, Array.Empty<string>());
        for (var i = 0; i < chain.Count - 1; i++) chain[i].SetNext(chain[i + 1]);
        return chain[0].Handle(request, Array.Empty<string>());
    }
}
