using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP01FactoryMethod;

public sealed record VoucherCreationRequest(Guid CompanyId, string Kind, decimal Amount, string Currency);
public sealed record VoucherDraft(Guid Id, Guid CompanyId, string Kind, decimal Amount, string Currency);
public interface IVoucherCreationChallenge { VoucherDraft Create(VoucherCreationRequest request); }

// ---- Producto: el comprobante abstracto y sus variantes concretas ----------------------------

public abstract class Voucher
{
    protected Voucher(VoucherCreationRequest request)
    {
        CompanyId = request.CompanyId;
        Amount = request.Amount;
        Currency = request.Currency;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public Guid CompanyId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public abstract string Kind { get; }

    public VoucherDraft ToDraft() => new(Id, CompanyId, Kind, Amount, Currency);
}

public sealed class IncomeVoucher(VoucherCreationRequest request) : Voucher(request) { public override string Kind => "ingreso"; }
public sealed class ExpenseVoucher(VoucherCreationRequest request) : Voucher(request) { public override string Kind => "egreso"; }
public sealed class AdjustmentVoucher(VoucherCreationRequest request) : Voucher(request) { public override string Kind => "ajuste"; }
public sealed class TransferVoucher(VoucherCreationRequest request) : Voucher(request) { public override string Kind => "traslado"; }

// ---- Creador: el método fábrica (CreateVoucher) es el punto de variación -------------------

public abstract class VoucherCreator
{
    public abstract string Kind { get; }

    /// <summary>Método fábrica: cada creador concreto decide qué comprobante instancia.</summary>
    protected abstract Voucher CreateVoucher(VoucherCreationRequest request);

    /// <summary>Operación común: valida las reglas contables y delega la creación en el método fábrica.</summary>
    public VoucherDraft Create(VoucherCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CompanyId == Guid.Empty) throw new ArgumentException("La empresa es obligatoria.", nameof(request));
        MoneyPrecision.Require(request.Amount, nameof(request));
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3 || !request.Currency.All(char.IsLetter))
            throw new ArgumentException("La moneda debe ser un código de tres letras.", nameof(request));
        return CreateVoucher(request).ToDraft();
    }
}

public sealed class IncomeVoucherCreator : VoucherCreator
{
    public override string Kind => "ingreso";
    protected override Voucher CreateVoucher(VoucherCreationRequest request) => new IncomeVoucher(request);
}
public sealed class ExpenseVoucherCreator : VoucherCreator
{
    public override string Kind => "egreso";
    protected override Voucher CreateVoucher(VoucherCreationRequest request) => new ExpenseVoucher(request);
}
public sealed class AdjustmentVoucherCreator : VoucherCreator
{
    public override string Kind => "ajuste";
    protected override Voucher CreateVoucher(VoucherCreationRequest request) => new AdjustmentVoucher(request);
}
public sealed class TransferVoucherCreator : VoucherCreator
{
    public override string Kind => "traslado";
    protected override Voucher CreateVoucher(VoucherCreationRequest request) => new TransferVoucher(request);
}

// ---- Cliente: resuelve el creador por tipo; un tipo nuevo solo exige otro creador registrado -

public sealed class VoucherCreationChallenge : IVoucherCreationChallenge
{
    private readonly IReadOnlyDictionary<string, VoucherCreator> _creators;

    public VoucherCreationChallenge() : this(new VoucherCreator[]
    {
        new IncomeVoucherCreator(), new ExpenseVoucherCreator(), new AdjustmentVoucherCreator(), new TransferVoucherCreator()
    })
    {
    }

    public VoucherCreationChallenge(IEnumerable<VoucherCreator> creators)
    {
        ArgumentNullException.ThrowIfNull(creators);
        _creators = creators.ToDictionary(creator => creator.Kind, StringComparer.OrdinalIgnoreCase);
    }

    public VoucherDraft Create(VoucherCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind is null || !_creators.TryGetValue(request.Kind, out var creator))
            throw new ArgumentException($"Tipo de comprobante no admitido: {request.Kind}.", nameof(request));
        return creator.Create(request);
    }
}
