using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RabbitMqEventBus.EventBus;
using RabbitMqEventBus.EventHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabbitMqEventBus.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        public Task Consume([FromServices] IEventBus eventBus)
        {
            eventBus.Publish(new BlogDeleteIntegrationModelEvent(Guid.NewGuid().ToString("N")));
            return Task.CompletedTask;
        }
    }
}
