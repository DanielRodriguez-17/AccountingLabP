namespace AccountingLab.Domain.Challenges.DP18Observer;

public sealed record FinancialEvent(Guid EventId, string Kind, decimal Amount);
public interface IFinancialNotificationsChallenge
{
    void Subscribe(string subscriberId, Action<FinancialEvent> receive);
    void Unsubscribe(string subscriberId);
    IReadOnlyList<string> Publish(FinancialEvent financialEvent);
}

/// <summary>Observador: cualquier interesado en los hechos financieros.</summary>
public interface IFinancialObserver
{
    string Id { get; }
    void Update(FinancialEvent financialEvent);
}

public sealed class DelegateObserver(string id, Action<FinancialEvent> receive) : IFinancialObserver
{
    public string Id => id;
    public void Update(FinancialEvent financialEvent) => receive(financialEvent);
}

/// <summary>Sujeto: administra las suscripciones y notifica a cada observador activo exactamente una vez.</summary>
public sealed class FinancialEventPublisher
{
    private readonly object _gate = new();
    private readonly List<IFinancialObserver> _observers = new();

    public void Attach(IFinancialObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_gate)
        {
            if (_observers.Any(item => item.Id == observer.Id))
                throw new ArgumentException($"Ya existe una suscripción con la identidad '{observer.Id}'.", nameof(observer));
            _observers.Add(observer);
        }
    }

    public void Detach(string subscriberId)
    {
        lock (_gate) _observers.RemoveAll(item => item.Id == subscriberId);
    }

    public IReadOnlyList<string> Notify(FinancialEvent financialEvent)
    {
        IFinancialObserver[] current;
        lock (_gate) current = _observers.ToArray();

        var delivered = new List<string>();
        var failures = new List<Exception>();
        foreach (var observer in current)
        {
            try
            {
                observer.Update(financialEvent);
                delivered.Add(observer.Id);
            }
            catch (Exception error)
            {
                // Un receptor defectuoso no debe impedir que los demás reciban el hecho.
                failures.Add(error);
            }
        }
        if (failures.Count > 0)
            throw new AggregateException("Uno o más suscriptores fallaron al recibir el evento.", failures);
        return delivered;
    }
}

public sealed class FinancialNotificationsChallenge : IFinancialNotificationsChallenge
{
    private readonly FinancialEventPublisher _publisher = new();

    public void Subscribe(string subscriberId, Action<FinancialEvent> receive)
    {
        if (string.IsNullOrWhiteSpace(subscriberId))
            throw new ArgumentException("La suscripción requiere una identidad.", nameof(subscriberId));
        ArgumentNullException.ThrowIfNull(receive);
        _publisher.Attach(new DelegateObserver(subscriberId, receive));
    }

    public void Unsubscribe(string subscriberId) => _publisher.Detach(subscriberId);

    public IReadOnlyList<string> Publish(FinancialEvent financialEvent)
    {
        ArgumentNullException.ThrowIfNull(financialEvent);
        return _publisher.Notify(financialEvent);
    }
}
