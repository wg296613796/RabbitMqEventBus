using RabbitMqEventBus.EventBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabbitMqEventBus.EventHandling
{
    public class BlogDeleteIntegrationModelEvent : IntegrationEvent
    {
        public string BlogId { get; private set; }
        public BlogDeleteIntegrationModelEvent(string blogid)
        {
            this.BlogId = blogid;
        }
    }
}
