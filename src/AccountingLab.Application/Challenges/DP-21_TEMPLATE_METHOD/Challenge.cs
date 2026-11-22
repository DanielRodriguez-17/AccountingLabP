namespace AccountingLab.Application.Challenges.DP21TemplateMethod;

public sealed record BankStatementItem(string Id, string Reference, DateOnly Date, decimal Amount);
public sealed record ReconciliationLedgerItem(string Id, string Reference, DateOnly Date, decimal Amount);
public sealed record BankReconciliationRequest(string Mode, IReadOnlyList<BankStatementItem> BankItems, IReadOnlyList<ReconciliationLedgerItem> LedgerItems, decimal Tolerance);
public sealed record ReconciliationMatch(string BankItemId, string LedgerItemId, decimal Difference);
public sealed record BankReconciliationResult(IReadOnlyList<ReconciliationMatch> Matches, IReadOnlyList<string> UnmatchedBankItems, IReadOnlyList<string> UnmatchedLedgerItems);
public interface IBankReconciliationChallenge { BankReconciliationResult Reconcile(BankReconciliationRequest request); }

/// <summary>
/// Clase abstracta con el método plantilla: fija el esqueleto del proceso (validar, emparejar,
/// informar lo no conciliado) y deja a las subclases los pasos que varían.
/// </summary>
public abstract class BankReconciliationProcess
{
    public abstract string Mode { get; }

    /// <summary>Paso variable: diferencia máxima de importe que cada modalidad admite como coincidencia.</summary>
    protected abstract decimal AllowedDifference(decimal configuredTolerance);

    /// <summary>Paso con comportamiento por defecto: mismo documento por referencia y fecha. Puede redefinirse.</summary>
    protected virtual bool IsSameDocument(BankStatementItem bank, ReconciliationLedgerItem ledger) =>
        bank.Reference == ledger.Reference && bank.Date == ledger.Date;

    // ---- Método plantilla --------------------------------------------------------------------
    public BankReconciliationResult Reconcile(BankReconciliationRequest request)
    {
        Validate(request);

        var allowed = AllowedDifference(request.Tolerance);
        var usedLedger = new HashSet<int>();
        var matches = new List<ReconciliationMatch>();
        var unmatchedBank = new List<string>();

        foreach (var bank in request.BankItems)
        {
            var index = FindLedgerMatch(bank, request.LedgerItems, usedLedger, allowed);
            if (index < 0)
            {
                unmatchedBank.Add(bank.Id);
                continue;
            }
            usedLedger.Add(index);
            var ledger = request.LedgerItems[index];
            matches.Add(new ReconciliationMatch(bank.Id, ledger.Id, Math.Abs(bank.Amount - ledger.Amount)));
        }

        var unmatchedLedger = request.LedgerItems
            .Where((_, position) => !usedLedger.Contains(position))
            .Select(item => item.Id)
            .ToArray();
        return new BankReconciliationResult(matches, unmatchedBank, unmatchedLedger);
    }

    private static void Validate(BankReconciliationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.BankItems);
        ArgumentNullException.ThrowIfNull(request.LedgerItems);
        if (request.Tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(request), request.Tolerance, "La tolerancia no puede ser negativa.");
    }

    private int FindLedgerMatch(BankStatementItem bank, IReadOnlyList<ReconciliationLedgerItem> ledger, HashSet<int> used, decimal allowed)
    {
        for (var i = 0; i < ledger.Count; i++)
        {
            if (used.Contains(i) || !IsSameDocument(bank, ledger[i])) continue;
            if (Math.Abs(bank.Amount - ledger[i].Amount) <= allowed) return i;
        }
        return -1;
    }
}

public sealed class ExactReconciliation : BankReconciliationProcess
{
    public override string Mode => "exact";
    protected override decimal AllowedDifference(decimal configuredTolerance) => 0m;
}

public sealed class ToleranceReconciliation : BankReconciliationProcess
{
    public override string Mode => "tolerance";
    protected override decimal AllowedDifference(decimal configuredTolerance) => configuredTolerance;
}

public sealed class BankReconciliationChallenge : IBankReconciliationChallenge
{
    private readonly IReadOnlyDictionary<string, BankReconciliationProcess> _processes;

    public BankReconciliationChallenge() : this(new BankReconciliationProcess[] { new ExactReconciliation(), new ToleranceReconciliation() })
    {
    }

    public BankReconciliationChallenge(IEnumerable<BankReconciliationProcess> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);
        _processes = processes.ToDictionary(process => process.Mode, StringComparer.OrdinalIgnoreCase);
    }

    public BankReconciliationResult Reconcile(BankReconciliationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Mode is null || !_processes.TryGetValue(request.Mode, out var process))
            throw new ArgumentException($"Modalidad de conciliación no admitida: {request.Mode}.", nameof(request));
        return process.Reconcile(request);
    }
}
