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
using MyLab.RabbitClient.Publishing;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
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
			Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
				{
					new TraceContextPropagator(),
					new BaggagePropagator(),
				}));
			var activityListener = new ActivityListener
			{
				ShouldListenTo = s => true,
				SampleUsingParentId = (ref ActivityCreationOptions<string> activityOptions) => ActivitySamplingResult.AllData,
				Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) => ActivitySamplingResult.AllData
			};
			ActivitySource.AddActivityListener(activityListener);
			
			ActivitySource activitySource = new ActivitySource("MyTestActivitySource");

			using var activity = activitySource.StartActivity("MyTestActivity");

			var traceId = Tracer.CurrentSpan.Context.TraceId.ToHexString();
			var spanId = Tracer.CurrentSpan.Context.SpanId.ToHexString();
			var activityId = Activity.Current.Id;

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
					.AddRabbitTracing()
					.ConfigureRabbit(TestTools.OptionsConfigureAct)
					.AddRabbitConsumer(queue.Name, consumer)
					.AddLogging(l => l.AddXUnit(_output).AddFilter(l => true))
				   )
				.Build();
			var publisher = host.Services.GetService<IRabbitPublisher>();

			await host.StartAsync(cancellationToken);

			//Act

			publisher
				.IntoQueue(queue.Name)
				.SendJson(new MessageDto { Value = "test value" })
				.Publish();

			await Task.Delay(500);

			cancellationSource.Cancel();

			await host.StopAsync();

			host.Dispose();

			//Assert
			//Check current trace id here
			Assert.NotEqual("0000000000000000", spanId);
			Assert.NotEqual("0000000000000000", consumer.SpanId);
			Assert.NotNull(consumer.SpanId);
			Assert.NotEqual("00000000000000000000000000000000", traceId);
			Assert.NotEqual("00000000000000000000000000000000", consumer.TraceId);
			Assert.NotNull(consumer.TraceId);

			Assert.NotEqual(activityId, consumer.ActivityId);
			Assert.NotEqual(spanId, consumer.SpanId);
			Assert.Equal(traceId, consumer.TraceId);

			Assert.Equal("test value", consumer.MessageValue);

		}

		public class MessageDto
		{
			public string Value { get; set; }
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
					.AddRabbitTracing()
					.ConfigureRabbit(TestTools.OptionsConfigureAct)
					.AddRabbitConsumer(queue.Name, consumer)
					.AddLogging(l => l.AddXUnit(_output).AddFilter(l => true))
				   )
				.Build();
			var publisher = host.Services.GetService<IRabbitPublisher>();

			await host.StartAsync(cancellationToken);

			//Act
			publisher
				.IntoQueue(queue.Name)
				.SendJson(new MessageDto { Value = "test value" })
				.Publish();

			await Task.Delay(500);

			cancellationSource.Cancel();

			await host.StopAsync();

			host.Dispose();

			//Assert
			//Check current trace id here
			Assert.Equal("test value", consumer.MessageValue);
		}

		class TestConsumer : RabbitConsumer<MessageDto>
		{
			protected override Task ConsumeMessageAsync(ConsumedMessage<MessageDto> consumedMessage)
			{
				SpanId = Activity.Current?.SpanId.ToHexString();
				TraceId = Activity.Current?.TraceId.ToHexString();
				ActivityId = Activity.Current?.Id;

				MessageValue = consumedMessage.Content.Value;
				return Task.CompletedTask;
			}

			public string TraceId { get; private set; }
			public string SpanId { get; private set; }
			public string ActivityId { get; private set; }
			public string MessageValue { get; private set; }
		}
	}
}
