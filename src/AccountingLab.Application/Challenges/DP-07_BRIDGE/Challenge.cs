using System.Globalization;
using System.Text.Json;

namespace AccountingLab.Application.Challenges.DP07Bridge;

public sealed record ReportPresentationRequest(string ReportType, string Format, decimal Amount);
public sealed record RenderedFinancialReport(string ReportType, string Format, string ContentType, string Content);
public interface IFinancialRepresentationChallenge { RenderedFinancialReport Render(ReportPresentationRequest request); }

// ---- Implementación: cómo se representa un reporte (jerarquía 1) -----------------------------

public interface IReportRepresentation
{
    string Format { get; }
    string ContentType { get; }
    string Render(string reportType, decimal amount);
}

public static class ReportAmountText
{
    /// <summary>Importe como cadena decimal exacta con cuatro posiciones, igual que el transporte HTTP.</summary>
    public static string Format(decimal amount) => amount.ToString("F4", CultureInfo.InvariantCulture);
}

public sealed class JsonReportRepresentation : IReportRepresentation
{
    public string Format => "json";
    public string ContentType => "application/json";

    public string Render(string reportType, decimal amount) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["reportType"] = reportType,
            ["amount"] = ReportAmountText.Format(amount)
        });
}

public sealed class CsvReportRepresentation : IReportRepresentation
{
    public string Format => "csv";
    public string ContentType => "text/csv";

    public string Render(string reportType, decimal amount) =>
        "reportType,amount\n" + reportType + "," + ReportAmountText.Format(amount);
}

// ---- Abstracción: qué reporte es (jerarquía 2); delega la forma en el puente -----------------

public abstract class FinancialReportView(IReportRepresentation representation, decimal amount)
{
    public abstract string ReportType { get; }

    public RenderedFinancialReport Render() =>
        new(ReportType, representation.Format, representation.ContentType, representation.Render(ReportType, amount));
}

public sealed class BalanceReportView(IReportRepresentation representation, decimal amount) : FinancialReportView(representation, amount)
{
    public override string ReportType => "balance";
}

public sealed class IncomeStatementReportView(IReportRepresentation representation, decimal amount) : FinancialReportView(representation, amount)
{
    public override string ReportType => "resultados";
}

// ---- Cliente: combina cualquier reporte con cualquier representación -------------------------

public sealed class FinancialRepresentationChallenge : IFinancialRepresentationChallenge
{
    private static readonly IReadOnlyDictionary<string, IReportRepresentation> Representations =
        new IReportRepresentation[] { new JsonReportRepresentation(), new CsvReportRepresentation() }
            .ToDictionary(item => item.Format, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, Func<IReportRepresentation, decimal, FinancialReportView>> Reports =
        new Dictionary<string, Func<IReportRepresentation, decimal, FinancialReportView>>(StringComparer.OrdinalIgnoreCase)
        {
            ["balance"] = (representation, amount) => new BalanceReportView(representation, amount),
            ["resultados"] = (representation, amount) => new IncomeStatementReportView(representation, amount)
        };

    public RenderedFinancialReport Render(ReportPresentationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ReportType is null || !Reports.TryGetValue(request.ReportType, out var createReport))
            throw new ArgumentException($"Tipo de reporte no admitido: {request.ReportType}.", nameof(request));
        if (request.Format is null || !Representations.TryGetValue(request.Format, out var representation))
            throw new ArgumentException($"Representación no disponible: {request.Format}.", nameof(request));
        if (Math.Round(request.Amount, 4) != request.Amount)
            throw new ArgumentException("El importe admite como máximo cuatro decimales.", nameof(request));
        return createReport(representation, request.Amount).Render();
    }
}
