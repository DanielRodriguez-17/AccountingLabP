using System.Collections.Concurrent;
using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP11Flyweight;

public sealed record MovementClassificationInput(Guid Id, decimal Amount, string Code, string Label, string FictionalTaxScope);

/// <summary>Estado intrínseco: inmutable y compartible entre todos los movimientos con la misma clasificación.</summary>
public sealed record ClassificationMetadata(string Code, string Label, string FictionalTaxScope);

/// <summary>Estado extrínseco (identidad e importe) propio de cada movimiento, más la referencia compartida.</summary>
public sealed record ClassifiedMovement(Guid Id, decimal Amount, ClassificationMetadata Metadata);

public interface ISharedClassificationChallenge { IReadOnlyList<ClassifiedMovement> Prepare(IReadOnlyList<MovementClassificationInput> movements); }

/// <summary>Fábrica de pesos ligeros: entrega siempre la misma instancia para la misma clasificación.</summary>
public sealed class ClassificationFlyweightFactory
{
    private readonly ConcurrentDictionary<(string Code, string Label, string Scope), ClassificationMetadata> _pool = new();

    public int PooledCount => _pool.Count;

    public ClassificationMetadata Get(string code, string label, string scope) =>
        _pool.GetOrAdd((code, label, scope), key => new ClassificationMetadata(key.Code, key.Label, key.Scope));
}

public sealed class SharedClassificationChallenge : ISharedClassificationChallenge
{
    private readonly ClassificationFlyweightFactory _flyweights = new();

    public IReadOnlyList<ClassifiedMovement> Prepare(IReadOnlyList<MovementClassificationInput> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);
        var result = new List<ClassifiedMovement>(movements.Count);
        foreach (var movement in movements)
        {
            ArgumentNullException.ThrowIfNull(movement);
            if (string.IsNullOrWhiteSpace(movement.Code))
                throw new ArgumentException("La clasificación requiere un código.", nameof(movements));
            MoneyPrecision.Require(movement.Amount, nameof(movements), allowNegative: true);
            result.Add(new ClassifiedMovement(
                movement.Id,
                movement.Amount,
                _flyweights.Get(movement.Code, movement.Label ?? "", movement.FictionalTaxScope ?? "")));
        }
        return result;
    }
}
