using RabbitMqEventBus.EventBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabbitMqEventBus.EventHandling
{
    public class BlogDeleteIntegrationEventHander : IIntegrationEventHandler<BlogDeleteIntegrationModelEvent>
    {
        public Task Handle(BlogDeleteIntegrationModelEvent @event)
        {
            Console.WriteLine("1111111111:" + @event.BlogId);
            return Task.CompletedTask;
        }
    }
}
