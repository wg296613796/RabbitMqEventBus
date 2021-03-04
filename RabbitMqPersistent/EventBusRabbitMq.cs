using Autofac;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMqEventBus.EventBus;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMqEventBus.RabbitMqPersistent
{
    public class EventBusRabbitMq : IEventBus
    {
        const string BROKER_NAME = "blogcore_event_bus";
        private readonly IRabbitMQPersistentConnection _rabbitMqPersistentConnection;
        private readonly ILogger<EventBusRabbitMq> _logger;
        private readonly ILifetimeScope _scope;
        private readonly IEventBusSubscriptionsManager _subsManager;
        private readonly string AUTOFAC_SCOPE_NAME = "blogcore_event_bus";
        private readonly int _retryCount;
        private string _queueName;
        private IModel _consumerChannel;
        public EventBusRabbitMq(IRabbitMQPersistentConnection rabbitMQPersistentConnection,
            ILogger<EventBusRabbitMq> logger,
            ILifetimeScope scope,
            IEventBusSubscriptionsManager subsManager,
            string queueName = null,
            int tryCount = 5
            )
        {
            _rabbitMqPersistentConnection = rabbitMQPersistentConnection;
            _logger = logger;
            _scope = scope;
            _queueName = queueName;
            _retryCount = tryCount;
            _subsManager = subsManager;
            _consumerChannel = CreateConsumerChannel();
            _subsManager.OnEventRemoveHandler += _subsManager_OnEventRemoveHandler;
        }

        private void _subsManager_OnEventRemoveHandler(object sender, string eventName)
        {
            if (!_rabbitMqPersistentConnection.IsConnectned)
            {
                _rabbitMqPersistentConnection.TryConnect();
            }
            using (var channel = _rabbitMqPersistentConnection.CreateModel())
            {
                channel.QueueUnbind(queue: _queueName, exchange: BROKER_NAME, routingKey: eventName);
                if (_subsManager.IsEmpty)
                {
                    _queueName = string.Empty;
                    _consumerChannel.Close();
                }
            }
        }

        public void Publish(IntegrationEvent @event)
        {
            if (!_rabbitMqPersistentConnection.IsConnectned)
            {
                _rabbitMqPersistentConnection.TryConnect();
            }
            var polly = Policy.Handle<RabbitMQ.Client.Exceptions.BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(_retryCount, _retryCount => TimeSpan.FromSeconds(Math.Pow(2, _retryCount)), (ex, time) =>
                {
                    _logger.LogInformation(ex, "");
                });
            var eventName = @event.GetType().Name;
            using var channel = _rabbitMqPersistentConnection.CreateModel();
            channel.ExchangeDeclare(exchange: BROKER_NAME, ExchangeType.Direct);
            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);
            polly.Execute(() =>
            {
                var properties = channel.CreateBasicProperties();
                properties.DeliveryMode = 2;
                channel.BasicPublish(exchange: BROKER_NAME,
                                    routingKey: eventName,
                                    mandatory: true,
                                    basicProperties: properties,
                                    body: body);
            });
        }

        public void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            DoInternalSubscription(eventName);

            _subsManager.AddSubscription<T, TH>();
            StartBasicConsume();
        }
        private void DoInternalSubscription(string eventName)
        {
            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);
            if (!containsKey)
            {
                if (!_rabbitMqPersistentConnection.IsConnectned)
                {
                    _rabbitMqPersistentConnection.TryConnect();
                }
                using (var channel = _rabbitMqPersistentConnection.CreateModel())
                {
                    channel.QueueBind(queue: _queueName, exchange: BROKER_NAME, routingKey: eventName);
                }
            }
        }
        private IModel CreateConsumerChannel()
        {
            if (!_rabbitMqPersistentConnection.IsConnectned)
            {
                _rabbitMqPersistentConnection.TryConnect();
            }
            _logger.LogTrace("Createing RabbitMq Consumer channel");

            var channel = _rabbitMqPersistentConnection.CreateModel();
            channel.ExchangeDeclare(exchange: BROKER_NAME, type: "direct");
            channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, false);//prefechCount=1表示不要在同一时间给一个工作者发送多于1个的消息
            //channel.QueueBind(_queueName,exchange:BROKER_NAME);
            channel.CallbackException += (sender, e) =>
            {
                _logger.LogWarning(e.Exception, "Recreating RabbitMQ Consumer channel");
                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
                StartBasicConsume();
            };
            return channel;
        }

        private void StartBasicConsume()
        {
            _logger.LogTrace("Starting RabbitMQ basic Consume...");
            if (_consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
                consumer.Received += Consumer_Received;
                _consumerChannel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
            }
            else
            {
                _logger.LogError("StartBasicConsume cannot call on_consumeChannel == null");
            }
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs @event)
        {
            var eventName = @event.RoutingKey;
            var message = Encoding.UTF8.GetString(@event.Body.Span);
            try
            {
                if (message.ToLowerInvariant().Contains("throw-fake-exception"))
                {
                    throw new InvalidOperationException($"Fake exception requested:\"{message}\"");
                }
                await ProcessEvent(eventName, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "--- Error Processing message ", message);
            }
            _consumerChannel.BasicAck(@event.DeliveryTag, multiple: false);
        }

        private async Task ProcessEvent(string eventName, string message)
        {
            _logger.LogTrace("Processing RabbitMq event:{EventName}", eventName);
            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                using var scope = _scope.BeginLifetimeScope(AUTOFAC_SCOPE_NAME);
                var subscriptions = _subsManager.GetHandlersForEvent(eventName);
                foreach (var subscription in subscriptions)
                {
                    if (subscription.IsDynamic)
                    {
                        var handler = scope.ResolveOptional(subscription.HandlerType) as IDynamicIntegrationEventHandler;
                        if (handler == null) continue;
                        dynamic obj = JObject.Parse(message);
                        await handler.Handle(obj);
                    }
                    else
                    {
                        var handler = scope.ResolveOptional(subscription.HandlerType);
                        if (handler == null) continue;
                        var eventType = _subsManager.GetEventTypeName(eventName);
                        var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                        var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
                    }
                }
            }
            else
            {
                _logger.LogWarning("No Subscription for RabbitMQ event:", eventName);
            }
        }
    }
}
