using RabbitMQ.Client;
using System;

namespace RabbitMqEventBus.RabbitMqPersistent
{
    public interface IRabbitMQPersistentConnection : IDisposable
    {
        bool IsConnectned { get; }
        bool TryConnect();
        IModel CreateModel();
    }
}
