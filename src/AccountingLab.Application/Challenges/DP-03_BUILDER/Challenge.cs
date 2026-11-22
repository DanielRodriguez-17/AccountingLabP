namespace AccountingLab.Application.Challenges.DP03Builder;

public sealed record FinancialReportRequest(string Title, DateOnly From, DateOnly To, IReadOnlyList<string> Sections, IReadOnlyList<string> Annexes);
public sealed record FinancialReport(string Title, DateOnly From, DateOnly To, IReadOnlyList<string> Sections, IReadOnlyList<string> Annexes);
public interface IReportAssemblyChallenge { FinancialReport Build(FinancialReportRequest request); }

/// <summary>Contrato del constructor: pasos opcionales y encadenables, más un paso final que valida y entrega el producto.</summary>
public interface IFinancialReportBuilder
{
    IFinancialReportBuilder WithTitle(string title);
    IFinancialReportBuilder ForPeriod(DateOnly from, DateOnly to);
    IFinancialReportBuilder AddSection(string section);
    IFinancialReportBuilder AddAnnex(string annex);
    FinancialReport Build();
}

public sealed class FinancialReportBuilder : IFinancialReportBuilder
{
    private string? _title;
    private DateOnly? _from;
    private DateOnly? _to;
    private readonly List<string> _sections = new();
    private readonly List<string> _annexes = new();

    public IFinancialReportBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public IFinancialReportBuilder ForPeriod(DateOnly from, DateOnly to)
    {
        _from = from;
        _to = to;
        return this;
    }

    public IFinancialReportBuilder AddSection(string section)
    {
        if (string.IsNullOrWhiteSpace(section)) throw new ArgumentException("Una sección no puede estar vacía.", nameof(section));
        _sections.Add(section);
        return this;
    }

    public IFinancialReportBuilder AddAnnex(string annex)
    {
        if (string.IsNullOrWhiteSpace(annex)) throw new ArgumentException("Un anexo no puede estar vacío.", nameof(annex));
        _annexes.Add(annex);
        return this;
    }

    /// <summary>Valida el conjunto completo y entrega un informe inmutable; el constructor queda listo para reutilizarse.</summary>
    public FinancialReport Build()
    {
        if (string.IsNullOrWhiteSpace(_title)) throw new ArgumentException("El informe requiere un título.");
        if (_from is null || _to is null) throw new ArgumentException("El informe requiere un intervalo contable.");
        if (_from.Value > _to.Value) throw new ArgumentException("El intervalo contable está invertido: la fecha inicial es posterior a la final.");

        var report = new FinancialReport(_title, _from.Value, _to.Value, _sections.ToArray(), _annexes.ToArray());
        _title = null;
        _from = null;
        _to = null;
        _sections.Clear();
        _annexes.Clear();
        return report;
    }
}

/// <summary>Director: conoce el orden de los pasos y traduce una solicitud en llamadas al constructor.</summary>
public sealed class FinancialReportDirector
{
    public FinancialReport Construct(IFinancialReportBuilder builder, FinancialReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Sections);
        ArgumentNullException.ThrowIfNull(request.Annexes);

        builder.WithTitle(request.Title).ForPeriod(request.From, request.To);
        foreach (var section in request.Sections) builder.AddSection(section);
        foreach (var annex in request.Annexes) builder.AddAnnex(annex);
        return builder.Build();
    }
}

public sealed class ReportAssemblyChallenge : IReportAssemblyChallenge
{
    private readonly FinancialReportDirector _director = new();

    public FinancialReport Build(FinancialReportRequest request) => _director.Construct(new FinancialReportBuilder(), request);
}
