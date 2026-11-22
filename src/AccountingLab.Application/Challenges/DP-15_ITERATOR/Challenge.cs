namespace AccountingLab.Application.Challenges.DP15Iterator;

public sealed record LedgerRecord(string EntryCode, DateOnly Date, decimal Debit, decimal Credit);
public sealed record LedgerPage(IReadOnlyList<LedgerRecord> Records);
public interface ILedgerCursor { bool MoveNext(); LedgerRecord Current { get; } }
public interface ILedgerTraversalChallenge { ILedgerCursor Open(IReadOnlyList<LedgerPage> pages); }

/// <summary>
/// Iterador concreto: recorre los registros de todas las páginas como una secuencia única y ordenada.
/// El consumidor no conoce las páginas ni su almacenamiento; las páginas vacías se saltan.
/// </summary>
public sealed class PagedLedgerCursor : ILedgerCursor
{
    private readonly IReadOnlyList<LedgerPage> _pages;
    private int _pageIndex;
    private int _recordIndex = -1;
    private LedgerRecord? _current;
    private bool _finished;

    public PagedLedgerCursor(IReadOnlyList<LedgerPage> pages)
    {
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
    }

    public LedgerRecord Current =>
        _current ?? throw new InvalidOperationException("El cursor no está posicionado sobre un registro.");

    public bool MoveNext()
    {
        if (_finished) return false;

        _recordIndex++;
        while (_pageIndex < _pages.Count)
        {
            var records = _pages[_pageIndex]?.Records;
            if (records is not null && _recordIndex < records.Count)
            {
                _current = records[_recordIndex];
                return true;
            }
            _pageIndex++;
            _recordIndex = 0;
        }

        _finished = true;
        _current = null;
        return false;
    }
}

public sealed class LedgerTraversalChallenge : ILedgerTraversalChallenge
{
    public ILedgerCursor Open(IReadOnlyList<LedgerPage> pages) => new PagedLedgerCursor(pages);
}
