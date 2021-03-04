using RabbitMqEventBus.EventBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabbitMqEventBus.EventBusSubscriptions
{
    public class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
    {
        private readonly Dictionary<string, List<SubscriptionInfo>> _handlers;
        private readonly List<Type> _eventType;
        public InMemoryEventBusSubscriptionsManager()
        {
            _handlers = new Dictionary<string, List<SubscriptionInfo>>();
            _eventType = new List<Type>();
        }
        public bool IsEmpty => _handlers == null || !_handlers.Keys.Any();

        public event EventHandler<string> OnEventRemoveHandler;

        public void AddDynamicSubscription<TH>(string eventName) where TH : IDynamicIntegrationEventHandler
        {
            DoAddSubscription(typeof(TH), eventName, isDynamic: true);
        }

        public void AddSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventKey = GetEventKey<T>();
            DoAddSubscription(typeof(TH), eventKey, false);
            if (!_eventType.Contains(typeof(T)))
            {
                _eventType.Add(typeof(T));
            }
        }

        private void DoAddSubscription(Type handlerType, string eventName, bool isDynamic)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                _handlers.Add(eventName, new List<SubscriptionInfo>());
            }
            if (_handlers[eventName].Any(p => p.HandlerType == handlerType))
            {
                throw new ArgumentException($"Handler Type {handlerType.Name} already registered for {eventName}", nameof(handlerType));
            }
            if (isDynamic)
            {
                _handlers[eventName].Add(SubscriptionInfo.Dynamic(handlerType));
            }
            else
            {
                _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
            }
        }

        public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName);
        public string GetEventKey<T>()
        {
            return typeof(T).Name;
        }
        public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName) => _handlers[eventName];

        public Type GetEventTypeName(string eventName) => _eventType.SingleOrDefault(p => p.Name == eventName);
    }
}
