namespace AccountingLab.Domain.Challenges.DP02AbstractFactory;

public sealed record AccountingProfileRequest(string Profile, string FictionalRegime);
public sealed record AccountingFamilyMember(string Role, string CompatibilityKey);
public sealed record AccountingServiceFamily(string Profile, string FictionalRegime, IReadOnlyList<AccountingFamilyMember> Members);
public interface IAccountingProfileChallenge { AccountingServiceFamily CreateFamily(AccountingProfileRequest request); }

// ---- Productos abstractos: una variedad por rol de la familia -------------------------------

public abstract class AccountingService(string role, string compatibilityKey)
{
    public string Role { get; } = role;
    public string CompatibilityKey { get; } = compatibilityKey;
}
public abstract class CalculationService(string compatibilityKey) : AccountingService("calculo", compatibilityKey);
public abstract class ValidationService(string compatibilityKey) : AccountingService("validacion", compatibilityKey);
public abstract class ReportService(string compatibilityKey) : AccountingService("reporte", compatibilityKey);

// ---- Fábrica abstracta: una operación de creación por producto de la familia ---------------

public abstract class AccountingServiceFactory(string profile, string regime)
{
    public string Profile { get; } = profile;
    public string Regime { get; } = regime;

    /// <summary>Una única clave por perfil y régimen: los tres productos de una fábrica son compatibles entre sí.</summary>
    protected string CompatibilityKey { get; } = profile + "/" + regime;

    public abstract CalculationService CreateCalculation();
    public abstract ValidationService CreateValidation();
    public abstract ReportService CreateReport();
}

// ---- Familia comercial ----------------------------------------------------------------------

public sealed class CommercialCalculationService(string key) : CalculationService(key);
public sealed class CommercialValidationService(string key) : ValidationService(key);
public sealed class CommercialReportService(string key) : ReportService(key);

public sealed class CommercialServiceFactory(string regime) : AccountingServiceFactory("comercial", regime)
{
    public override CalculationService CreateCalculation() => new CommercialCalculationService(CompatibilityKey);
    public override ValidationService CreateValidation() => new CommercialValidationService(CompatibilityKey);
    public override ReportService CreateReport() => new CommercialReportService(CompatibilityKey);
}

// ---- Familia de servicios -------------------------------------------------------------------

public sealed class ServicesCalculationService(string key) : CalculationService(key);
public sealed class ServicesValidationService(string key) : ValidationService(key);
public sealed class ServicesReportService(string key) : ReportService(key);

public sealed class ServicesServiceFactory(string regime) : AccountingServiceFactory("servicios", regime)
{
    public override CalculationService CreateCalculation() => new ServicesCalculationService(CompatibilityKey);
    public override ValidationService CreateValidation() => new ServicesValidationService(CompatibilityKey);
    public override ReportService CreateReport() => new ServicesReportService(CompatibilityKey);
}

// ---- Cliente: elige la fábrica por perfil y solo conoce las abstracciones -------------------

public sealed class AccountingProfileChallenge : IAccountingProfileChallenge
{
    private static readonly IReadOnlyDictionary<string, Func<string, AccountingServiceFactory>> Factories =
        new Dictionary<string, Func<string, AccountingServiceFactory>>(StringComparer.OrdinalIgnoreCase)
        {
            ["comercial"] = regime => new CommercialServiceFactory(regime),
            ["servicios"] = regime => new ServicesServiceFactory(regime)
        };

    public AccountingServiceFamily CreateFamily(AccountingProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Profile is null || !Factories.TryGetValue(request.Profile, out var create))
            throw new ArgumentException($"Perfil de empresa no admitido: {request.Profile}.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.FictionalRegime))
            throw new ArgumentException("El régimen ficticio es obligatorio.", nameof(request));

        var factory = create(request.FictionalRegime);
        AccountingService[] products = [factory.CreateCalculation(), factory.CreateValidation(), factory.CreateReport()];
        return new AccountingServiceFamily(
            factory.Profile,
            factory.Regime,
            products.Select(product => new AccountingFamilyMember(product.Role, product.CompatibilityKey)).ToArray());
    }
}
