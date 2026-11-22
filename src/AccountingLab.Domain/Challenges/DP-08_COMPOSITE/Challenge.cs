using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP08Composite;

public sealed record AccountHierarchy(string Code, bool AllowsPosting, decimal OwnBalance, IReadOnlyList<AccountHierarchy> Children);
public interface IAccountTreeChallenge { decimal Total(AccountHierarchy account); }

/// <summary>Componente: cuentas individuales y agrupaciones se consultan con la misma operación.</summary>
public abstract class AccountNode(string code)
{
    public string Code { get; } = code;
    public abstract decimal Total();
}

/// <summary>Hoja: una cuenta de movimiento aporta su saldo propio.</summary>
public sealed class PostingAccountNode(string code, decimal balance) : AccountNode(code)
{
    public override decimal Total() => balance;
}

/// <summary>Compuesto: una agrupadora no tiene saldo propio; suma el de sus descendientes.</summary>
public sealed class AccountGroupNode(string code, IReadOnlyList<AccountNode> children) : AccountNode(code)
{
    public override decimal Total() => children.Sum(child => child.Total());
}

public static class AccountNodeFactory
{
    public static AccountNode From(AccountHierarchy account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var children = account.Children ?? Array.Empty<AccountHierarchy>();
        if (account.AllowsPosting)
        {
            if (children.Count > 0)
                throw new ArgumentException($"La cuenta {account.Code} acepta movimientos y no puede tener subcuentas.", nameof(account));
            MoneyPrecision.Require(account.OwnBalance, nameof(account), allowNegative: true);
            return new PostingAccountNode(account.Code, account.OwnBalance);
        }
        return new AccountGroupNode(account.Code, children.Select(From).ToArray());
    }
}

public sealed class AccountTreeChallenge : IAccountTreeChallenge
{
    public decimal Total(AccountHierarchy account) => AccountNodeFactory.From(account).Total();
}
