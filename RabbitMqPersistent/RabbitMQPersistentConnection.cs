using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.IO;
using System.Net.Sockets;

namespace RabbitMqEventBus.RabbitMqPersistent
{
    public class RabbitMQPersistentConnection : IRabbitMQPersistentConnection
    {
        bool _dispose;
        object lock_obj = new object();
        private IConnection _connection;
        private readonly IConnectionFactory _connectionFactory;
        private readonly int _retryCount;
        private readonly ILogger<RabbitMQPersistentConnection> _logger;
        public RabbitMQPersistentConnection(IConnectionFactory connectionFactory, ILogger<RabbitMQPersistentConnection> logger, int RetryCount)
        {
            if (connectionFactory == null) throw new ArgumentNullException(nameof(connectionFactory));
            if (logger == null) throw new ArgumentNullException(nameof(ILogger));
            _connectionFactory = connectionFactory;
            _retryCount = RetryCount;
            _logger = logger;
        }
        public IModel CreateModel()
        {
            if (!IsConnectned)
            {
                throw new InvalidOperationException("No RabbitMq Connection are available to perform this action");
            }
            return _connection.CreateModel();
        }

        public void Dispose()
        {
            if (_dispose) return;
            _dispose = true;
            try
            {
                _connection.Dispose();
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }

        public bool TryConnect()
        {
            _logger.LogInformation("RabbitMq Client is trying to connect");
            lock (lock_obj)
            {
                var policy = Policy.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(_retryCount, retryCount => TimeSpan.FromSeconds(Math.Pow(2, retryCount)), (ex, time) =>
                    {
                        _logger.LogWarning(ex, "RabbitMq Client cannot connect after {TimeOut}s ({Exceptiopn})");
                    });
                policy.Execute(() =>
                {
                    _connection = _connectionFactory.CreateConnection();
                });
                if (IsConnectned)
                {
                    _connection.ConnectionShutdown += _connection_ConnectionShutdown;
                    _connection.CallbackException += _connection_CallbackException;
                    _connection.ConnectionBlocked += _connection_ConnectionBlocked;

                    _logger.LogInformation("RabbitMq Client acquired a persistent  connection to ");
                    return true;
                }
                else
                {
                    _logger.LogCritical("RabbitMq Client connect error");
                    return false;
                }
            }
        }

        private void _connection_ConnectionBlocked(object sender, RabbitMQ.Client.Events.ConnectionBlockedEventArgs e)
        {
            if (_dispose) return;
            _logger.LogInformation("A rabbitMq Client Connection is ShutDown,trying to re-connect...");
            TryConnect();
        }

        private void _connection_CallbackException(object sender, RabbitMQ.Client.Events.CallbackExceptionEventArgs e)
        {
            if (_dispose) return;
            _logger.LogInformation("A rabbitMq Client Connection is throw exception");
            TryConnect();
        }

        private void _connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            if (_dispose) return;
            _logger.LogInformation("A rabbit Client Connection is ShutDown,trying to re-connect....");
            TryConnect();
        }

        public bool IsConnectned => _connection != null && _connection.IsOpen && !_dispose;
    }
}
