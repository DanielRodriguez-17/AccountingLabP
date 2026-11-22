namespace AccountingLab.Application.Challenges.DP09Decorator;

public sealed record DocumentExportRequest(string Body, IReadOnlyList<string> Features, IReadOnlyDictionary<string, string> Metadata);
public sealed record ExportedAccountingDocument(string Content, IReadOnlyList<string> AppliedFeatures, IReadOnlyDictionary<string, string> Metadata);
public interface IDocumentEnrichmentChallenge { ExportedAccountingDocument Export(DocumentExportRequest request); }

/// <summary>Componente: cualquier cosa que produzca un documento exportado.</summary>
public interface IDocumentExport { ExportedAccountingDocument Export(); }

/// <summary>Componente concreto: el documento base, sin características adicionales.</summary>
public sealed class PlainDocumentExport(string body) : IDocumentExport
{
    public ExportedAccountingDocument Export() =>
        new(body, Array.Empty<string>(), new Dictionary<string, string>());
}

/// <summary>Decorador base: envuelve otro exportador y registra su característica sin modificar el original.</summary>
public abstract class DocumentExportDecorator(IDocumentExport inner) : IDocumentExport
{
    protected abstract string Feature { get; }
    protected abstract ExportedAccountingDocument Decorate(ExportedAccountingDocument document);

    public ExportedAccountingDocument Export()
    {
        var decorated = Decorate(inner.Export());
        return decorated with { AppliedFeatures = decorated.AppliedFeatures.Append(Feature).ToArray() };
    }
}

public sealed class AuditMarkDecorator(IDocumentExport inner) : DocumentExportDecorator(inner)
{
    protected override string Feature => "auditoria";

    protected override ExportedAccountingDocument Decorate(ExportedAccountingDocument document) =>
        document with { Content = document.Content + "\n[AUDIT] Documento exportado con marca de auditoría." };
}

public sealed class MetadataDecorator(IDocumentExport inner, IReadOnlyDictionary<string, string> metadata) : DocumentExportDecorator(inner)
{
    protected override string Feature => "metadatos";

    protected override ExportedAccountingDocument Decorate(ExportedAccountingDocument document)
    {
        var merged = new Dictionary<string, string>(document.Metadata);
        foreach (var (key, value) in metadata) merged[key] = value;
        return document with { Metadata = merged };
    }
}

public sealed class DocumentEnrichmentChallenge : IDocumentEnrichmentChallenge
{
    private delegate IDocumentExport Wrap(IDocumentExport inner, IReadOnlyDictionary<string, string> metadata);

    private static readonly IReadOnlyDictionary<string, Wrap> Decorators =
        new Dictionary<string, Wrap>(StringComparer.OrdinalIgnoreCase)
        {
            ["auditoria"] = (inner, _) => new AuditMarkDecorator(inner),
            ["metadatos"] = (inner, metadata) => new MetadataDecorator(inner, metadata)
        };

    public ExportedAccountingDocument Export(DocumentExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Body);
        ArgumentNullException.ThrowIfNull(request.Features);
        var metadata = request.Metadata ?? new Dictionary<string, string>();

        IDocumentExport export = new PlainDocumentExport(request.Body);
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in request.Features)
        {
            if (feature is null || !Decorators.TryGetValue(feature, out var wrap))
                throw new ArgumentException($"Característica de exportación no admitida: {feature}.", nameof(request));
            if (!applied.Add(feature))
                throw new ArgumentException($"La característica '{feature}' se solicitó más de una vez.", nameof(request));
            export = wrap(export, metadata);
        }
        return export.Export();
    }
}
