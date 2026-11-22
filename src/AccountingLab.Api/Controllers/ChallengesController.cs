using AccountingLab.Api.Transport;
using AccountingLab.Domain.Education;
using AccountingLab.Domain.Model;
using AccountingLab.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using D01 = AccountingLab.Domain.Challenges.DP01FactoryMethod;
using D02 = AccountingLab.Domain.Challenges.DP02AbstractFactory;
using D03 = AccountingLab.Application.Challenges.DP03Builder;
using D04 = AccountingLab.Domain.Challenges.DP04Prototype;
using D05 = AccountingLab.Domain.Challenges.DP05Singleton;
using D06 = AccountingLab.Infrastructure.Challenges.DP06Adapter;
using D07 = AccountingLab.Application.Challenges.DP07Bridge;
using D08 = AccountingLab.Domain.Challenges.DP08Composite;
using D09 = AccountingLab.Application.Challenges.DP09Decorator;
using D10 = AccountingLab.Application.Challenges.DP10Facade;
using D11 = AccountingLab.Domain.Challenges.DP11Flyweight;
using D12 = AccountingLab.Application.Challenges.DP12Proxy;
using D13 = AccountingLab.Domain.Challenges.DP13ChainOfResponsibility;
using D14 = AccountingLab.Domain.Challenges.DP14Command;
using D15 = AccountingLab.Application.Challenges.DP15Iterator;
using D16 = AccountingLab.Domain.Challenges.DP16Mediator;
using D17 = AccountingLab.Domain.Challenges.DP17Memento;
using D18 = AccountingLab.Domain.Challenges.DP18Observer;
using D19 = AccountingLab.Domain.Challenges.DP19State;
using D20 = AccountingLab.Domain.Challenges.DP20Strategy;
using D21 = AccountingLab.Application.Challenges.DP21TemplateMethod;
using D22 = AccountingLab.Domain.Challenges.DP22Visitor;

namespace AccountingLab.Api.Controllers;

public sealed record IdentifiedOperationInput(D14.AccountingOperationRequest Request, D14.AccountingOperationContext Context);

[ApiController, Route("api"), Authorize, ServiceFilter(typeof(TenantAccessFilter))]
[ProducesResponseType(typeof(ProblemDetails), 501)]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403)]
public sealed class ChallengesController(TenantContext tenant, CatalogService service) : ControllerBase
{
    // Las excepciones de validación de los patrones se traducen a los errores HTTP que ya conoce la API:
    // permisos -> 403 y entradas o reglas incumplidas -> 400. Un PatternPendingException sigue siendo 501.
    private IActionResult Run<T>(Func<T> operation)
    {
        try
        {
            return Ok(operation());
        }
        catch (UnauthorizedAccessException error)
        {
            throw new AccessDeniedException(error.Message);
        }
        catch (Exception error) when (error is ArgumentException or FormatException or InvalidOperationException)
        {
            throw new BusinessRuleException(error.Message);
        }
    }

    [HttpGet("challenges")]
    public IActionResult Catalog() => Ok(ChallengeCatalog.Items.Select(x => new { x.Id, x.Name, x.Family, x.Context, x.ReferenceUrl, Available = true, Verification = "NotExecuted" }));

    [HttpPost("vouchers/generate")]
    public IActionResult Voucher(D01.VoucherCreationRequest input) => Run(() => new D01.VoucherCreationChallenge().Create(input with { CompanyId = tenant.CompanyId }));
    [HttpPost("companies/profile-services")]
    public IActionResult Profile(D02.AccountingProfileRequest input) => Run(() => new D02.AccountingProfileChallenge().CreateFamily(input));
    [HttpPost("reports/build")]
    public IActionResult Report(D03.FinancialReportRequest input) => Run(() => new D03.ReportAssemblyChallenge().Build(input));
    [HttpGet("reports/balance-sheet")]
    public IActionResult BalanceSheet(DateOnly? from, DateOnly? to) => Report(new("Balance general", from ?? new DateOnly(2026, 1, 1), to ?? new DateOnly(2026, 12, 31), ["Activo", "Pasivo", "Patrimonio"], []));
    [HttpPost("templates/copy")]
    public IActionResult CopyTemplate(D04.RecurringTemplate input) => Run(() => new D04.TemplateCopyChallenge().Copy(input));
    [HttpPost("configuration/registry")]
    public IActionResult Registry() => Run(() => new D05.GlobalRegistryChallenge().Acquire());
    [HttpPost("bank-statements/import")]
    public IActionResult Import(D06.BankImportRequest input) => Run(() => new D06.BankImportChallenge().Import(input));
    [HttpPost("reports/render")]
    public IActionResult Render(D07.ReportPresentationRequest input) => Run(() => new D07.FinancialRepresentationChallenge().Render(input));
    [HttpPost("accounts/tree-total")]
    public IActionResult Tree(D08.AccountHierarchy input) => Run(() => new D08.AccountTreeChallenge().Total(input));
    [HttpPost("exports/enriched")]
    public IActionResult Enrich(D09.DocumentExportRequest input) => Run(() => new D09.DocumentEnrichmentChallenge().Export(input));
    [HttpPost("accounting-periods/close")]
    public IActionResult Close(D10.ClosePeriodRequest input) => Run(() => new D10.PeriodClosingChallenge().Close(input with { CompanyId = tenant.CompanyId }));
    [HttpPost("movements/classifications")]
    public IActionResult Classifications(IReadOnlyList<D11.MovementClassificationInput> input) => Run(() => new D11.SharedClassificationChallenge().Prepare(input));
    [HttpPost("reports/confidential")]
    public async Task<IActionResult> Confidential(D12.SensitiveReportRequest input, CancellationToken ct = default)
    {
        if (tenant.Role != "Administrador") throw new AccessDeniedException("El informe confidencial requiere rol Administrador.");
        var summary = await service.SummaryAsync(tenant.CompanyId, ct);
        return Run(() => new D12.SensitiveReportChallenge().Get(input with { CompanyId = tenant.CompanyId, MayRead = true },
            () => new D12.SensitiveFinancialReport(tenant.CompanyId, input.ReportKey, summary.Debits)));
    }
    [HttpPost("payments/approve")]
    public IActionResult Approve(D13.PaymentApprovalRequest input) => Run(() => new D13.PaymentApprovalChallenge().Evaluate(input));
    [HttpPost("operations/execute")]
    public IActionResult ExecuteOperation(IdentifiedOperationInput input) => Run(() => new D14.IdentifiedOperationChallenge().Execute(input.Request with { CompanyId = tenant.CompanyId }, input.Context));
    [HttpPost("ledger/traverse")]
    public IActionResult Traverse(IReadOnlyList<D15.LedgerPage> input) => Run(() =>
    {
        // El cursor es un objeto con estado, no un dato: se consume aquí y se devuelve el recorrido completo.
        var cursor = new D15.LedgerTraversalChallenge().Open(input);
        var visited = new List<D15.LedgerRecord>();
        while (cursor.MoveNext()) visited.Add(cursor.Current);
        return visited;
    });
    [HttpPost("invoices/process")]
    public IActionResult Process(D16.InvoiceProcessingRequest input) => Run(() => new D16.InvoiceCoordinationChallenge().Process(input));
    [HttpPost("budgets/snapshots")]
    public IActionResult Snapshot(D17.BudgetDraft input) => Run(() =>
    {
        // La instantánea es opaca por diseño: la API confirma su creación sin exponer su estado interno.
        _ = new D17.BudgetHistoryChallenge().Save(input);
        return new { Saved = true, BudgetId = input.Id, Lines = input.Lines.Count };
    });
    [HttpPost("notifications/publish")]
    public IActionResult Publish(D18.FinancialEvent input) => Run(() => new D18.FinancialNotificationsChallenge().Publish(input));
    // Demostración: suscribe dos observadores, publica un hecho y devuelve quién lo recibió y qué vio.
    private IActionResult NotifyDemo() => Run(() =>
    {
        var received = new List<string>();
        var challenge = new D18.FinancialNotificationsChallenge();
        challenge.Subscribe("contabilidad", e => received.Add($"contabilidad recibió {e.Kind} por {e.Amount}"));
        challenge.Subscribe("tesoreria", e => received.Add($"tesoreria recibió {e.Kind} por {e.Amount}"));
        var delivered = challenge.Publish(new(Guid.NewGuid(), "pago-registrado", 100m));
        challenge.Unsubscribe("tesoreria");
        var afterUnsubscribe = challenge.Publish(new(Guid.NewGuid(), "pago-registrado", 50m));
        return new { Notificados = delivered, Mensajes = received, TrasDarDeBajaTesoreria = afterUnsubscribe };
    });
    [HttpPost("invoices/transition")]
    public IActionResult Transition(D19.InvoiceLifecycleRequest input) => Run(() => new D19.InvoiceLifecycleChallenge().Transition(input));
    [HttpPost("taxes/calculate")]
    public IActionResult Taxes(D20.TaxCalculationRequest input) => Run(() => new D20.ConfigurableTaxChallenge().Calculate(input));
    [HttpPost("bank-reconciliation/reconcile")]
    public IActionResult Reconcile(D21.BankReconciliationRequest input) => Run(() => new D21.BankReconciliationChallenge().Reconcile(input));
    [HttpPost("documents/analyze")]
    public IActionResult Analyze(D22.DocumentAnalysisRequest input) => Run(() => new D22.DocumentAnalysisChallenge().Analyze(input));

    // Invocaciones educativas con entradas ficticias explícitas. Cada una atraviesa el mismo contrato real
    // de su módulo y devuelve el resultado que produce la implementación; no son operaciones contables.
    [HttpPost("challenges/{id}/execute")]
    public async Task<IActionResult> Demonstrate(string id)
    {
        var companyId = tenant.CompanyId;
        var date = new DateOnly(2026, 1, 15);
        return id.ToUpperInvariant() switch
        {
            "DP-01" => Voucher(new(companyId, "ingreso", 100m, "COP")),
            "DP-02" => Profile(new("comercial", "DEMO-A")),
            "DP-03" => Report(new("Informe educativo", new(2026, 1, 1), new(2026, 12, 31), ["Balance"], ["Notas"])),
            "DP-04" => CopyTemplate(new D04.RecurringTemplate
            {
                Id = Guid.NewGuid(), Name = "Arriendo mensual",
                Lines = [new D04.TemplateLine { AccountCode = "5105", Amount = 450m, Tags = ["mensual"] }]
            }),
            "DP-05" => Registry(),
            "DP-06" => Import(new("BancoA", "[{\"id\":\"A-1\",\"date\":\"2026-01-12\",\"description\":\"Cobro demo\",\"debit\":\"0.0000\",\"credit\":\"25.1250\",\"currency\":\"COP\"}]")),
            "DP-07" => Render(new("balance", "csv", 100m)),
            "DP-08" => Tree(new("1", false, 0m, [new("1105", true, 25m, []), new("1110", true, 75.1250m, [])])),
            "DP-09" => Enrich(new("Documento educativo", ["auditoria", "metadatos"], new Dictionary<string, string> { ["empresa"] = "DEMO" })),
            "DP-10" => Close(new(companyId, new(2026, 1, 1), new(2026, 1, 31), [new("1105", 100m, 0m), new("3105", 0m, 100m)], 0)),
            "DP-11" => Classifications([new(Guid.NewGuid(), 1m, "VENTA", "Venta demo", "DEMO-A"), new(Guid.NewGuid(), 2m, "VENTA", "Venta demo", "DEMO-A")]),
            "DP-12" => await Confidential(new(companyId, true, "Balance")),
            "DP-13" => Approve(new(100m, 500m, true, 200m, ["fondos", "politica", "limite"])),
            "DP-14" => ExecuteOperation(new(new(companyId, Guid.NewGuid(), "apply-payment", Guid.NewGuid(), 100m), new(200m, false, []))),
            "DP-15" => Traverse([new(new D15.LedgerRecord[] { new("A-1", date, 1m, 0m) }), new(Array.Empty<D15.LedgerRecord>()), new(new D15.LedgerRecord[] { new("A-2", date, 0m, 1m) })]),
            "DP-16" => Process(new(Guid.NewGuid(), 100m, true, ["validacion", "contabilizacion", "publicacion"])),
            "DP-17" => Snapshot(new D17.BudgetDraft { Id = Guid.NewGuid(), Name = "Borrador educativo" }),
            "DP-18" => NotifyDemo(),
            "DP-19" => Transition(new("draft", "emit", 100m, new(2026, 12, 31), date)),
            "DP-20" => Taxes(new(100m, "venta", date, [new("DEMO-P", "venta", new(2026, 1, 1), new(2026, 12, 31), "percentage", 0.1375m, 0m)])),
            "DP-21" => Reconcile(new("exact", [new("B-1", "R-1", date, 50m)], [new("L-1", "R-1", date, 50m)], 0m)),
            "DP-22" => Analyze(new("gross", [new D22.InvoiceAnalysisDocument(Guid.NewGuid(), 100m, 13m), new D22.PaymentAnalysisDocument(Guid.NewGuid(), 20m)])),
            _ => throw new ResourceNotFoundException("No existe ese desafío académico.")
        };
    }
}
