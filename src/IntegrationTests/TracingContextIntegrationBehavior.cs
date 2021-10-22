using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyLab.RabbitClient.Consuming;
using MyLab.RabbitClient.Model;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests
{
    public class TracingContextIntegrationBehavior
    {
        private readonly ITestOutputHelper _output;

        public TracingContextIntegrationBehavior(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ShouldRestoreTraceContextInConsumer()
        {
            //Arrange
            var activitySource = new ActivitySource(string.Empty);
            var builder = Sdk.CreateTracerProviderBuilder();
            builder.AddLegacySource("MyLegacySource");
            using var provider = builder.Build();
            using var activity = activitySource.StartActivity("MyTestActivity");
            var traceId = Tracer.CurrentSpan.Context.TraceId.ToHexString();
            var spanId = Tracer.CurrentSpan.Context.SpanId.ToHexString();

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

            queue.Publish("foo");

            await Task.Delay(500);

            cancellationSource.Cancel();

            await host.StopAsync();

            host.Dispose();

            //Assert
            //Check current trace id here
            Assert.NotEqual("0000000000000000", spanId);
            Assert.NotEqual("0000000000000000", consumer.SpanId);
            Assert.NotEqual("00000000000000000000000000000000", traceId);
            Assert.NotEqual("00000000000000000000000000000000", consumer.TraceId);
            Assert.NotEqual(consumer.SpanId, spanId);
            Assert.Equal(consumer.TraceId, traceId);
        }

        [Fact]
        public async Task ShouldNotRestoreTraceContextInConsumer()
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

            queue.Publish("foo");

            await Task.Delay(500);

            cancellationSource.Cancel();

            await host.StopAsync();

            host.Dispose();

            //Assert
            //Check current trace id here
            Assert.Equal("0000000000000000", consumer.SpanId);
            Assert.Equal("00000000000000000000000000000000", consumer.TraceId);
        }

        class TestConsumer : RabbitConsumer<string>
        {
            protected override Task ConsumeMessageAsync(ConsumedMessage<string> consumedMessage)
            {
                SpanId = Tracer.CurrentSpan.Context.SpanId.ToHexString();
                TraceId = Tracer.CurrentSpan.Context.TraceId.ToHexString();
                return Task.CompletedTask;
            }

            public string TraceId { get; private set; }
            public string SpanId { get; private set; }
        }
    }
}
