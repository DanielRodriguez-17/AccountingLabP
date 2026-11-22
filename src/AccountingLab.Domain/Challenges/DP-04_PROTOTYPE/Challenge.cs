namespace AccountingLab.Domain.Challenges.DP04Prototype;

/// <summary>Contrato del prototipo: un objeto que sabe producir una copia profunda de sí mismo.</summary>
public interface IPrototype<out T> { T Clone(); }

public sealed class TemplateLine : IPrototype<TemplateLine>
{
    public string AccountCode { get; set; } = "";
    public decimal Amount { get; set; }
    public List<string> Tags { get; set; } = new();

    public TemplateLine Clone() => new()
    {
        AccountCode = AccountCode,
        Amount = Amount,
        Tags = new List<string>(Tags ?? new List<string>())
    };
}

public sealed class RecurringTemplate : IPrototype<RecurringTemplate>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<TemplateLine> Lines { get; set; } = new();

    /// <summary>Copia profunda con los mismos valores: renglones y etiquetas son colecciones nuevas.</summary>
    public RecurringTemplate Clone() => new()
    {
        Id = Id,
        Name = Name,
        Lines = (Lines ?? new List<TemplateLine>()).Select(line => line.Clone()).ToList()
    };
}

public interface ITemplateCopyChallenge { RecurringTemplate Copy(RecurringTemplate source); }
public sealed class TemplateCopyChallenge : ITemplateCopyChallenge
{
    /// <summary>La plantilla duplicada conserva sus datos, pero es un documento nuevo con identidad propia.</summary>
    public RecurringTemplate Copy(RecurringTemplate source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = source.Clone();
        copy.Id = Guid.NewGuid();
        return copy;
    }
}
