using System;

namespace RabbitMqEventBus.EventBusSubscriptions
{
    public class SubscriptionInfo
    {
        public bool IsDynamic { get; set; }
        public Type HandlerType { get; set; }
        public SubscriptionInfo(bool isDynamic, Type handlerType)
        {
            IsDynamic = isDynamic;
            HandlerType = handlerType;
        }
        public static SubscriptionInfo Dynamic(Type handlerType)
        {
            return new SubscriptionInfo(true, handlerType);
        }

        public static SubscriptionInfo Typed(Type handlerType)
        {
            return new SubscriptionInfo(false, handlerType);
        }
    }
}
