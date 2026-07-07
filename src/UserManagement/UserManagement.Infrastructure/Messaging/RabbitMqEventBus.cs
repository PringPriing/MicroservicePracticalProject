using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Kernel.Messaging;
using UserManagement.Application.Services;

namespace UserManagement.Infrastructure.Messaging;

public sealed class RabbitMqEventBus(IOptions<RabbitMqOptions> options) : IEventBus, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken ct) where TEvent : class
    {
        IChannel channel = await GetChannelAsync(ct);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(@event);
        await channel.BasicPublishAsync(_options.ExchangeName, routingKey, body, ct);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is not null)
            return _channel;

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_channel is not null)
                return _channel;

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password
            };

            _connection = await factory.CreateConnectionAsync(ct);
            _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
            await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: ct);

            return _channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();

        _connectionLock.Dispose();
    }
}
