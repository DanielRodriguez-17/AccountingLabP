namespace AccountingLab.Application.Challenges.DP12Proxy;

public sealed record SensitiveReportRequest(Guid CompanyId, bool MayRead, string ReportKey);
public sealed record SensitiveFinancialReport(Guid CompanyId, string ReportKey, decimal ConfidentialAmount);
public interface ISensitiveReportChallenge { SensitiveFinancialReport Get(SensitiveReportRequest request, Func<SensitiveFinancialReport> loadReport); }

/// <summary>Interfaz común del sujeto real y de su sustituto.</summary>
public interface ISensitiveReport { SensitiveFinancialReport Read(); }

/// <summary>Sujeto real: lee la fuente confidencial. No sabe nada de permisos.</summary>
public sealed class SensitiveReportSource(Func<SensitiveFinancialReport> load) : ISensitiveReport
{
    public SensitiveFinancialReport Read() => load();
}

/// <summary>
/// Proxy de protección: decide antes de tocar la fuente y verifica después que el resultado
/// pertenezca a la empresa solicitante.
/// </summary>
public sealed class SensitiveReportAccessProxy(SensitiveReportRequest request, ISensitiveReport subject) : ISensitiveReport
{
    public SensitiveFinancialReport Read()
    {
        if (!request.MayRead)
            throw new UnauthorizedAccessException("No tienes autorización para consultar este reporte confidencial.");

        var report = subject.Read() ?? throw new InvalidOperationException("La fuente no devolvió el reporte solicitado.");
        if (report.CompanyId != request.CompanyId)
            throw new InvalidOperationException("La fuente devolvió un reporte de otra empresa.");
        return report;
    }
}

public sealed class SensitiveReportChallenge : ISensitiveReportChallenge
{
    public SensitiveFinancialReport Get(SensitiveReportRequest request, Func<SensitiveFinancialReport> loadReport)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(loadReport);
        ISensitiveReport access = new SensitiveReportAccessProxy(request, new SensitiveReportSource(loadReport));
        return access.Read();
    }
}
