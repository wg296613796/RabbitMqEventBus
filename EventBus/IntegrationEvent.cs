using System;

namespace RabbitMqEventBus.EventBus
{
    /// <summary>
    /// 事件模型
    /// </summary>
    public class IntegrationEvent
    {
        public IntegrationEvent()
        {
            Id = Guid.NewGuid();
            CreateTime = DateTime.Now;
        }
        public IntegrationEvent(Guid id, DateTime createTime)
        {
            Id = id;
            CreateTime = createTime;
        }
        public Guid Id { get; set; }
        public DateTime CreateTime { get; set; }

    }
}
