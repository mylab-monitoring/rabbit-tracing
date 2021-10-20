using System;
using MyLab.RabbitClient.Publishing;
using RabbitMQ.Client;

namespace MyLab.RabbitTracing
{
    class TraceIdInjector : IPublishingMessageProcessor
    {
        public void Process(IBasicProperties basicProperties, ref byte[] content)
        {
            throw new NotImplementedException();
        }
    }
}