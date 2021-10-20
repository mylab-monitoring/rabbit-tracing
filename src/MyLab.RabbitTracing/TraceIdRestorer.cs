using System;
using MyLab.RabbitClient.Consuming;
using RabbitMQ.Client.Events;

namespace MyLab.RabbitTracing
{
    class TraceIdRestorer : IConsumedMessageProcessor
    {
        public void Process(BasicDeliverEventArgs deliverEventArgs)
        {
            throw new NotImplementedException();
        }
    }
}