using System;
using MyLab.RabbitClient.Consuming;
using RabbitMQ.Client.Events;

namespace MyLab.RabbitTracing
{
    /// <summary>
    /// Applies trace id from consumed message
    /// </summary>
    public class TraceIdRestorer : IConsumedMessageProcessor
    {
        /// <inheritdoc />
        public void Process(BasicDeliverEventArgs deliverEventArgs)
        {
            throw new NotImplementedException();
        }
    }
}