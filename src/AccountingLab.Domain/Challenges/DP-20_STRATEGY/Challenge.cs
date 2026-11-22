using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP20Strategy;

public sealed record FictionalTaxRule(string Name, string OperationKind, DateOnly ValidFrom, DateOnly ValidTo, string CalculationKind, decimal Rate, decimal FixedAmount);
public sealed record TaxCalculationRequest(decimal BaseAmount, string OperationKind, DateOnly AccountingDate, IReadOnlyList<FictionalTaxRule> Rules);
public sealed record TaxCalculationResult(decimal BaseAmount, decimal AppliedRate, decimal TaxAmount, string RuleName);
public interface IConfigurableTaxChallenge { TaxCalculationResult Calculate(TaxCalculationRequest request); }

public sealed record TaxComputation(decimal Amount, decimal AppliedRate);

/// <summary>Estrategia: una familia intercambiable de algoritmos de cálculo, elegida por la configuración de la regla.</summary>
public interface ITaxCalculationStrategy
{
    string Kind { get; }
    TaxComputation Calculate(decimal baseAmount, FictionalTaxRule rule);
}

public sealed class PercentageTaxStrategy : ITaxCalculationStrategy
{
    public string Kind => "percentage";

    public TaxComputation Calculate(decimal baseAmount, FictionalTaxRule rule)
    {
        if (rule.Rate < 0 || rule.Rate > 1)
            throw new ArgumentException($"La tasa de la regla '{rule.Name}' debe estar entre 0 y 1.", nameof(rule));
        return new TaxComputation(MoneyPrecision.Round(baseAmount * rule.Rate), rule.Rate);
    }
}

public sealed class FixedTaxStrategy : ITaxCalculationStrategy
{
    public string Kind => "fixed";

    public TaxComputation Calculate(decimal baseAmount, FictionalTaxRule rule)
    {
        MoneyPrecision.Require(rule.FixedAmount, nameof(rule));
        return new TaxComputation(MoneyPrecision.Round(rule.FixedAmount), 0m);
    }
}

/// <summary>Contexto: elige la regla vigente para la operación y delega el cálculo en la estrategia de esa regla.</summary>
public sealed class ConfigurableTaxChallenge : IConfigurableTaxChallenge
{
    private readonly IReadOnlyDictionary<string, ITaxCalculationStrategy> _strategies;

    public ConfigurableTaxChallenge() : this(new ITaxCalculationStrategy[] { new PercentageTaxStrategy(), new FixedTaxStrategy() })
    {
    }

    public ConfigurableTaxChallenge(IEnumerable<ITaxCalculationStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        _strategies = strategies.ToDictionary(strategy => strategy.Kind, StringComparer.OrdinalIgnoreCase);
    }

    public TaxCalculationResult Calculate(TaxCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Rules);
        MoneyPrecision.Require(request.BaseAmount, nameof(request));

        // Entre varias reglas vigentes prevalece la de vigencia más reciente; un empate es ambiguo.
        var applicable = request.Rules
            .Where(candidate => string.Equals(candidate.OperationKind, request.OperationKind, StringComparison.OrdinalIgnoreCase)
                                && candidate.ValidFrom <= request.AccountingDate
                                && request.AccountingDate <= candidate.ValidTo)
            .OrderByDescending(candidate => candidate.ValidFrom)
            .Take(2)
            .ToList();
        if (applicable.Count == 0)
            throw new InvalidOperationException($"No hay una regla vigente para la operación '{request.OperationKind}' en {request.AccountingDate:yyyy-MM-dd}.");
        if (applicable.Count == 2 && applicable[0].ValidFrom == applicable[1].ValidFrom)
            throw new InvalidOperationException($"Hay reglas vigentes ambiguas para la operación '{request.OperationKind}'.");

        var rule = applicable[0];
        if (rule.CalculationKind is null || !_strategies.TryGetValue(rule.CalculationKind, out var strategy))
            throw new ArgumentException($"La regla '{rule.Name}' usa un cálculo no admitido: {rule.CalculationKind}.", nameof(request));

        var computation = strategy.Calculate(request.BaseAmount, rule);
        return new TaxCalculationResult(request.BaseAmount, computation.AppliedRate, computation.Amount, rule.Name);
    }
}
