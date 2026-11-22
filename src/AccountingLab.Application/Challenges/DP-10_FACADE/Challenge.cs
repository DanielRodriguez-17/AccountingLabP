using AccountingLab.Domain.Model;

namespace AccountingLab.Application.Challenges.DP10Facade;

public sealed record ClosingAccountBalance(string AccountCode, decimal Debit, decimal Credit);
public sealed record ClosePeriodRequest(Guid CompanyId, DateOnly From, DateOnly To, IReadOnlyList<ClosingAccountBalance> Balances, int UnpostedDocuments);
public sealed record PeriodClosingSummary(Guid CompanyId, DateOnly From, DateOnly To, bool Closed, decimal TotalDebit, decimal TotalCredit);
public interface IPeriodClosingChallenge { PeriodClosingSummary Close(ClosePeriodRequest request); }

// ---- Subsistemas: cada uno comprueba una condición del cierre -------------------------------

public interface IPendingDocumentsCheck { void EnsureNoneUnposted(int unpostedDocuments); }
public sealed class PendingDocumentsCheck : IPendingDocumentsCheck
{
    public void EnsureNoneUnposted(int unpostedDocuments)
    {
        if (unpostedDocuments > 0)
            throw new InvalidOperationException($"No se puede cerrar el periodo: hay {unpostedDocuments} documento(s) sin contabilizar.");
    }
}

public sealed record ClosingTotals(decimal Debit, decimal Credit);
public interface IBalanceConsistencyCheck { ClosingTotals Verify(IReadOnlyList<ClosingAccountBalance> balances); }
public sealed class BalanceConsistencyCheck : IBalanceConsistencyCheck
{
    public ClosingTotals Verify(IReadOnlyList<ClosingAccountBalance> balances)
    {
        foreach (var balance in balances)
        {
            ArgumentNullException.ThrowIfNull(balance);
            MoneyPrecision.Require(balance.Debit, nameof(balances));
            MoneyPrecision.Require(balance.Credit, nameof(balances));
        }
        var totals = new ClosingTotals(balances.Sum(item => item.Debit), balances.Sum(item => item.Credit));
        if (totals.Debit != totals.Credit)
            throw new InvalidOperationException($"No se puede cerrar el periodo: los débitos ({totals.Debit}) y los créditos ({totals.Credit}) no coinciden.");
        return totals;
    }
}

// ---- Fachada: una sola entrada que coordina los subsistemas ------------------------------------

public sealed class PeriodClosingChallenge : IPeriodClosingChallenge
{
    private readonly IPendingDocumentsCheck _documents;
    private readonly IBalanceConsistencyCheck _balances;

    public PeriodClosingChallenge() : this(new PendingDocumentsCheck(), new BalanceConsistencyCheck())
    {
    }

    public PeriodClosingChallenge(IPendingDocumentsCheck documents, IBalanceConsistencyCheck balances)
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _balances = balances ?? throw new ArgumentNullException(nameof(balances));
    }

    /// <summary>Todo o nada: el resumen solo existe si todas las comprobaciones pasaron; no hay cierres parciales.</summary>
    public PeriodClosingSummary Close(ClosePeriodRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Balances);
        if (request.CompanyId == Guid.Empty) throw new ArgumentException("La empresa es obligatoria.", nameof(request));
        if (request.From > request.To) throw new ArgumentException("El intervalo del periodo está invertido.", nameof(request));
        if (request.UnpostedDocuments < 0) throw new ArgumentException("La cantidad de documentos pendientes no puede ser negativa.", nameof(request));

        _documents.EnsureNoneUnposted(request.UnpostedDocuments);
        var totals = _balances.Verify(request.Balances);
        return new PeriodClosingSummary(request.CompanyId, request.From, request.To, true, totals.Debit, totals.Credit);
    }
}
