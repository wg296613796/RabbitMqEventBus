using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMqEventBus.EventBus;
using RabbitMqEventBus.EventBusSubscriptions;
using RabbitMqEventBus.EventHandling;
using RabbitMqEventBus.RabbitMqPersistent;
using System;

namespace RabbitMqEventBus.ServiceExtention
{
    public static class EventBusSetup
    {
        public static IServiceCollection AddEventBusSetup(this IServiceCollection service)
        {
            if (service == null) throw new ArgumentNullException(nameof(IServiceCollection));
            service.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();
            //service.AddTransient<BlogDeleteIntegrationEventHander>();
            service.AddSingleton<IRabbitMQPersistentConnection>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<RabbitMQPersistentConnection>>();
                var factory = new ConnectionFactory()
                {
                    HostName = "127.0.0.1",
                    DispatchConsumersAsync = true,
                    Password = "guest",
                    UserName = "guest"
                };
                return new RabbitMQPersistentConnection(factory, logger, 5);
            });

            service.AddSingleton<IEventBus, EventBusRabbitMq>(sp =>
            {
                var rabbitMqConnection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
                var lifeTimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusRabbitMq>>();
                var eventBusSubscriotion = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                var retryCount = 5;

                return new EventBusRabbitMq(rabbitMqConnection, logger, lifeTimeScope, eventBusSubscriotion, "Blog_Core_Api", retryCount);
            });
            return service;
        }

        public static IApplicationBuilder ConfigureEventBus(this IApplicationBuilder app)
        {
            var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
            eventBus.Subscribe<BlogDeleteIntegrationModelEvent, BlogDeleteIntegrationEventHander>();
            return app;
        }
    }
}
