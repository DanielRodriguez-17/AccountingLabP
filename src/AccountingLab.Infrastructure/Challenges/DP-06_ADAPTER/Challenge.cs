using System.Globalization;
using System.Text.Json;
using AccountingLab.Domain.Model;

namespace AccountingLab.Infrastructure.Challenges.DP06Adapter;

public sealed record BankImportRequest(string Provider, string Payload);
public sealed record ImportedBankMovement(string ExternalId, DateOnly Date, string Description, decimal Debit, decimal Credit, string Currency);
public interface IBankImportChallenge { IReadOnlyList<ImportedBankMovement> Import(BankImportRequest request); }

// ---- Interfaz objetivo: lo único que el sistema conoce de un banco --------------------------

public interface IBankStatementAdapter
{
    string Provider { get; }
    IReadOnlyList<ImportedBankMovement> Adapt(string payload);
}

// ---- Adaptados: los contratos incompatibles de cada banco ficticio -----------------------------

public sealed record BancoALine(string Id, DateOnly Date, string Description, decimal Debit, decimal Credit, string Currency)
{
    public static BancoALine From(JsonElement item) => new(
        ProviderJson.Text(item, "id"), ProviderJson.Date(item, "date"), ProviderJson.Text(item, "description"),
        ProviderJson.Money(item, "debit"), ProviderJson.Money(item, "credit"), ProviderJson.Text(item, "currency"));
}

public sealed record BancoBLine(string Reference, DateOnly BookedOn, string Concept, decimal SignedAmount, string IsoCurrency)
{
    public static BancoBLine From(JsonElement item) => new(
        ProviderJson.Text(item, "reference"), ProviderJson.Date(item, "bookedOn"), ProviderJson.Text(item, "concept"),
        ProviderJson.Money(item, "signedAmount", allowNegative: true), ProviderJson.Text(item, "isoCurrency"));
}

// ---- Adaptadores: traducen cada contrato al modelo común --------------------------------------

public sealed class BancoAAdapter : IBankStatementAdapter
{
    public string Provider => "BancoA";

    public IReadOnlyList<ImportedBankMovement> Adapt(string payload) =>
        ProviderJson.ParseArray(payload, BancoALine.From)
            .Select(line => new ImportedBankMovement(line.Id, line.Date, line.Description, line.Debit, line.Credit, line.Currency))
            .ToArray();
}

public sealed class BancoBAdapter : IBankStatementAdapter
{
    public string Provider => "BancoB";

    /// <summary>En BancoB un importe negativo es un débito y uno positivo es un crédito.</summary>
    public IReadOnlyList<ImportedBankMovement> Adapt(string payload) =>
        ProviderJson.ParseArray(payload, BancoBLine.From)
            .Select(line => new ImportedBankMovement(
                line.Reference, line.BookedOn, line.Concept,
                line.SignedAmount < 0 ? -line.SignedAmount : 0m,
                line.SignedAmount > 0 ? line.SignedAmount : 0m,
                line.IsoCurrency))
            .ToArray();
}

// ---- Lectura estricta de JSON: toda anomalía se informa como FormatException -----------------

internal static class ProviderJson
{
    public static IReadOnlyList<T> ParseArray<T>(string payload, Func<JsonElement, T> map)
    {
        ArgumentNullException.ThrowIfNull(payload);
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new FormatException("El extracto debe ser un arreglo JSON.");
            var result = new List<T>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new FormatException("Cada movimiento del extracto debe ser un objeto JSON.");
                result.Add(map(item));
            }
            return result;
        }
        catch (JsonException error)
        {
            throw new FormatException("El extracto no es un JSON válido.", error);
        }
    }

    public static string Text(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)) throw new FormatException($"Falta el campo '{name}'.");
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            _ => throw new FormatException($"El campo '{name}' tiene un tipo no admitido.")
        };
    }

    public static decimal Money(JsonElement item, string name, bool allowNegative = false)
    {
        var text = Text(item, name);
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || !MoneyPrecision.HasValidScale(value)
            || (!allowNegative && value < 0))
            throw new FormatException($"El importe del campo '{name}' no es válido.");
        return value;
    }

    public static DateOnly Date(JsonElement item, string name)
    {
        var text = Text(item, name);
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            throw new FormatException($"La fecha del campo '{name}' debe tener el formato yyyy-MM-dd.");
        return value;
    }
}

// ---- Cliente: elige el adaptador del banco y trabaja solo con la interfaz objetivo ----------

public sealed class BankImportChallenge : IBankImportChallenge
{
    private readonly IReadOnlyDictionary<string, IBankStatementAdapter> _adapters;

    public BankImportChallenge() : this(new IBankStatementAdapter[] { new BancoAAdapter(), new BancoBAdapter() })
    {
    }

    public BankImportChallenge(IEnumerable<IBankStatementAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = adapters.ToDictionary(adapter => adapter.Provider, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ImportedBankMovement> Import(BankImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Provider is null || !_adapters.TryGetValue(request.Provider, out var adapter))
            throw new ArgumentException($"Proveedor bancario no admitido: {request.Provider}.", nameof(request));
        return adapter.Adapt(request.Payload);
    }
}
