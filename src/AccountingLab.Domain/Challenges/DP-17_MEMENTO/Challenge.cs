namespace AccountingLab.Domain.Challenges.DP17Memento;

public sealed class BudgetDraftLine
{
    public string AccountCode { get; set; } = "";
    public decimal Amount { get; set; }
}
public sealed class BudgetDraft
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool Approved { get; set; }
    public List<BudgetDraftLine> Lines { get; set; } = new();
}
public interface IBudgetSnapshot { }
public interface IBudgetHistoryChallenge
{
    IBudgetSnapshot Save(BudgetDraft draft);
    BudgetDraft Restore(BudgetDraft current, IBudgetSnapshot snapshot);
}

/// <summary>
/// Originador: crea y aplica instantáneas de un borrador. El memento es una clase privada anidada,
/// así que su estado solo es visible para esta clase; para el resto del sistema es un objeto opaco.
/// </summary>
public sealed class BudgetDraftOriginator(BudgetDraft draft)
{
    private sealed class Snapshot(Guid draftId, string name, IReadOnlyList<(string AccountCode, decimal Amount)> lines) : IBudgetSnapshot
    {
        internal Guid DraftId { get; } = draftId;
        internal string Name { get; } = name;
        internal IReadOnlyList<(string AccountCode, decimal Amount)> Lines { get; } = lines;
    }

    public IBudgetSnapshot Save()
    {
        EnsureEditable();
        var lines = (draft.Lines ?? new List<BudgetDraftLine>())
            .Select(line => (line.AccountCode, line.Amount))
            .ToArray();
        return new Snapshot(draft.Id, draft.Name, lines);
    }

    /// <summary>Devuelve el borrador a su estado guardado; los renglones restaurados son copias nuevas.</summary>
    public BudgetDraft Restore(IBudgetSnapshot snapshot)
    {
        EnsureEditable();
        if (snapshot is not Snapshot memento)
            throw new ArgumentException("La instantánea no fue creada por el historial de presupuestos.", nameof(snapshot));
        if (memento.DraftId != draft.Id)
            throw new InvalidOperationException("La instantánea pertenece a otro presupuesto.");

        draft.Name = memento.Name;
        draft.Lines = memento.Lines
            .Select(line => new BudgetDraftLine { AccountCode = line.AccountCode, Amount = line.Amount })
            .ToList();
        return draft;
    }

    private void EnsureEditable()
    {
        if (draft.Approved)
            throw new InvalidOperationException("Un presupuesto aprobado no admite instantáneas ni restauración de borrador.");
    }
}

/// <summary>Cuidador: conserva y entrega instantáneas sin conocer su contenido.</summary>
public sealed class BudgetHistoryChallenge : IBudgetHistoryChallenge
{
    public IBudgetSnapshot Save(BudgetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return new BudgetDraftOriginator(draft).Save();
    }

    public BudgetDraft Restore(BudgetDraft current, IBudgetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(snapshot);
        return new BudgetDraftOriginator(current).Restore(snapshot);
    }
}
