using System;
using MyLab.RabbitClient.Publishing;
using RabbitMQ.Client;

namespace MyLab.RabbitTracing
{
    /// <summary>
    /// Adds trace id into publishing message
    /// </summary>
    public class TraceIdInjector : IPublishingMessageProcessor
    {
        /// <inheritdoc />
        public void Process(IBasicProperties basicProperties, ref byte[] content)
        {
            throw new NotImplementedException();
        }
    }
}