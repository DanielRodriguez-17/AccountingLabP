namespace AccountingLab.Domain.Challenges.DP22Visitor;

public interface IAccountingAnalysisDocument { Guid Id { get; } }
public sealed record InvoiceAnalysisDocument(Guid Id, decimal NetAmount, decimal TaxAmount) : IAccountingAnalysisDocument;
public sealed record PaymentAnalysisDocument(Guid Id, decimal Amount) : IAccountingAnalysisDocument;
public sealed record AnalysisPostingLine(decimal Debit, decimal Credit);
public sealed record JournalAnalysisDocument(Guid Id, IReadOnlyList<AnalysisPostingLine> Lines) : IAccountingAnalysisDocument;
public sealed record DocumentAnalysisRequest(string View, IReadOnlyList<IAccountingAnalysisDocument> Documents);
public sealed record DocumentAnalysisResult(decimal TotalAmount, int DocumentCount, IReadOnlyDictionary<string, decimal> ByKind);
public interface IDocumentAnalysisChallenge { DocumentAnalysisResult Analyze(DocumentAnalysisRequest request); }

// ---- Elementos visitables: envuelven cada documento sin añadir lógica a los modelos ----------

public interface IVisitableDocument { void Accept(IDocumentVisitor visitor); }

public interface IDocumentVisitor
{
    void Visit(InvoiceElement element);
    void Visit(PaymentElement element);
    void Visit(JournalElement element);
}

public sealed class InvoiceElement(InvoiceAnalysisDocument document) : IVisitableDocument
{
    public InvoiceAnalysisDocument Document => document;
    public void Accept(IDocumentVisitor visitor) => visitor.Visit(this);
}

public sealed class PaymentElement(PaymentAnalysisDocument document) : IVisitableDocument
{
    public PaymentAnalysisDocument Document => document;
    public void Accept(IDocumentVisitor visitor) => visitor.Visit(this);
}

public sealed class JournalElement(JournalAnalysisDocument document) : IVisitableDocument
{
    public JournalAnalysisDocument Document => document;
    public void Accept(IDocumentVisitor visitor) => visitor.Visit(this);
}

public static class VisitableDocumentFactory
{
    public static IVisitableDocument Wrap(IAccountingAnalysisDocument document) => document switch
    {
        InvoiceAnalysisDocument invoice => new InvoiceElement(invoice),
        PaymentAnalysisDocument payment => new PaymentElement(payment),
        JournalAnalysisDocument journal => new JournalElement(journal),
        null => throw new ArgumentException("La colección contiene un documento nulo."),
        _ => throw new ArgumentException($"Tipo de documento no admitido: {document.GetType().Name}.")
    };
}

// ---- Visitantes: cada análisis es una clase nueva; los documentos no cambian -----------------

public abstract class DocumentAnalysisVisitor : IDocumentVisitor
{
    private readonly Dictionary<string, decimal> _byKind = new();

    public abstract void Visit(InvoiceElement element);
    public abstract void Visit(PaymentElement element);
    public abstract void Visit(JournalElement element);

    protected void Add(string kind, decimal amount) => _byKind[kind] = _byKind.GetValueOrDefault(kind) + amount;

    public DocumentAnalysisResult ToResult(int documentCount) =>
        new(_byKind.Values.Sum(), documentCount, new Dictionary<string, decimal>(_byKind));
}

/// <summary>Vista bruta: factura con impuesto, pagos y los importes débito de los asientos.</summary>
public sealed class GrossAmountVisitor : DocumentAnalysisVisitor
{
    public override void Visit(InvoiceElement element) => Add("invoice", element.Document.NetAmount + element.Document.TaxAmount);
    public override void Visit(PaymentElement element) => Add("payment", element.Document.Amount);
    public override void Visit(JournalElement element) =>
        Add("journal", (element.Document.Lines ?? Array.Empty<AnalysisPostingLine>()).Sum(line => line.Debit));
}

/// <summary>Vista de conteo: cuántos documentos hay de cada tipo.</summary>
public sealed class DocumentCountVisitor : DocumentAnalysisVisitor
{
    public override void Visit(InvoiceElement element) => Add("invoice", 1m);
    public override void Visit(PaymentElement element) => Add("payment", 1m);
    public override void Visit(JournalElement element) => Add("journal", 1m);
}

public sealed class DocumentAnalysisChallenge : IDocumentAnalysisChallenge
{
    private static readonly IReadOnlyDictionary<string, Func<DocumentAnalysisVisitor>> Views =
        new Dictionary<string, Func<DocumentAnalysisVisitor>>(StringComparer.OrdinalIgnoreCase)
        {
            ["gross"] = () => new GrossAmountVisitor(),
            ["count"] = () => new DocumentCountVisitor()
        };

    public DocumentAnalysisResult Analyze(DocumentAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.View is null || !Views.TryGetValue(request.View, out var createVisitor))
            throw new ArgumentException($"Vista de análisis no admitida: {request.View}.", nameof(request));
        ArgumentNullException.ThrowIfNull(request.Documents);

        var visitor = createVisitor();
        foreach (var document in request.Documents) VisitableDocumentFactory.Wrap(document).Accept(visitor);
        return visitor.ToResult(request.Documents.Count);
    }
}
