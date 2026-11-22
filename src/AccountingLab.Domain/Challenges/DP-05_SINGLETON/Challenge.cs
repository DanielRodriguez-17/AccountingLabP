namespace AccountingLab.Domain.Challenges.DP05Singleton;

public sealed record GlobalAccountingRegistry(string Version, int MonetaryScale, string Scope);
public interface IGlobalRegistryChallenge { GlobalAccountingRegistry Acquire(); }

/// <summary>
/// Único dueño de la instancia. El inicializador estático lo ejecuta el CLR una sola vez y de forma
/// segura entre hilos, por lo que no se necesitan bloqueos. El registro es inmutable y no contiene
/// datos de empresas; su alcance es el proceso actual.
/// </summary>
internal static class GlobalRegistryHolder
{
    internal static readonly GlobalAccountingRegistry Instance = new("DEMO-1", 4, "process");
}

public sealed class GlobalRegistryChallenge : IGlobalRegistryChallenge
{
    public GlobalAccountingRegistry Acquire() => GlobalRegistryHolder.Instance;
}
