namespace UserManagement.Application.Services;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken ct) where TEvent : class;
}
