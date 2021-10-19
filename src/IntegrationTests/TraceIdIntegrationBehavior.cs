using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyLab.RabbitClient.Consuming;
using MyLab.RabbitClient.Model;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests
{
    public class TraceIdIntegrationBehavior
    {
        private readonly ITestOutputHelper _output;

        public TraceIdIntegrationBehavior(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ShouldRestoreTraceIdInConsumer()
        {
            //Arrange
            var queue = new RabbitQueueFactory(TestTools.ChannelProvider)
            {
                AutoDelete = true,
                Prefix = "test"
            }.CreateWithRandomId();

            var consumer = new TestConsumer();

            var cancellationSource = new CancellationTokenSource();
            var cancellationToken = cancellationSource.Token;

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(srv => srv
                    .AddRabbit()
                    .ConfigureRabbit(TestTools.OptionsConfigureAct)
                    .AddRabbitConsumer(queue.Name, consumer)
                    .AddLogging(l => l.AddXUnit(_output).AddFilter(l => true)))
                .Build();

            await host.StartAsync(cancellationToken);

            //Act
            
            //Set trace id here
            
            queue.Publish("foo");

            //Reset trace id here

            await Task.Delay(500);

            cancellationSource.Cancel();

            await host.StopAsync();

            host.Dispose();

            //Get current trace id here

            //Assert
            //Check current trace id here
            Assert.Null("Implement it!");
        }

        class TestConsumer : RabbitConsumer<string>
        {
            protected override Task ConsumeMessageAsync(ConsumedMessage<string> consumedMessage)
            {
                return Task.CompletedTask;
            }
        }
    }
}
