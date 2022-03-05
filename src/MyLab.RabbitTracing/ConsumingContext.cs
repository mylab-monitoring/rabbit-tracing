using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using MyLab.RabbitClient.Consuming;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MyLab.RabbitTracing
{
	class ConsumingContext : IConsumingContext
	{
		public ConsumingContext(ILogger<ConsumingContext> logger)
		{
			_logger = logger;
		}

		private static readonly ActivitySource ActivitySource = new ActivitySource(Assembly.GetEntryAssembly().GetName().Name);
		private static readonly TextMapPropagator Propagator = new TraceContextPropagator();
		private readonly ILogger<ConsumingContext> _logger;


		public IConsumingContextInstance Set(BasicDeliverEventArgs deliverEventArgs)
		{
			var parentContext = Propagator.Extract(default, deliverEventArgs.BasicProperties, ExtractTraceContextFromBasicProperties);
			Baggage.Current = parentContext.Baggage;

			var destination = string.IsNullOrWhiteSpace(deliverEventArgs.Exchange) ? deliverEventArgs.RoutingKey : deliverEventArgs.Exchange;

			var activityName = $"{(string.IsNullOrWhiteSpace(destination) ? "queue" : destination)} receive";

			// Start an activity with a name following the semantic convention of the OpenTelemetry messaging specification.
			// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md#span-name
			var activity = ActivitySource.StartActivity(activityName, ActivityKind.Consumer, parentContext.ActivityContext);

			var message = Encoding.UTF8.GetString(deliverEventArgs.Body.Span.ToArray());

			activity?.SetTag("message", message);
			// The OpenTelemetry messaging specification defines a number of attributes. These attributes are added here.
			AddMessagingTags(activity, deliverEventArgs);

			return new ConsumingContextInstance(activity, CreateLogScope(_logger, activity));
		}

		private IDisposable CreateLogScope(ILogger logger, Activity activity)
		{
			var dic = new Dictionary<string, object>
			{
				{ "TraceId", activity?.TraceId.ToHexString() }
			};
			return logger.BeginScope(dic);
		}

		public static void AddMessagingTags(Activity activity, BasicDeliverEventArgs ea)
		{
			// These tags are added demonstrating the semantic conventions of the OpenTelemetry messaging specification
			// See:
			//   * https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md#messaging-attributes
			//   * https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md#rabbitmq
			activity?.SetTag("messaging.system", "rabbitmq");
			activity?.SetTag("messaging.destination_kind", string.IsNullOrWhiteSpace(ea.Exchange) ? "queue" : "topic");
			activity?.SetTag("messaging.destination", ea.Exchange);
			activity?.SetTag("messaging.rabbitmq.routing_key", ea.RoutingKey);
		}

		private IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
		{
			try
			{
				if (props.Headers != null && props.Headers.TryGetValue(key, out var value))
				{
					var bytes = value as byte[];
					return new[] { Encoding.UTF8.GetString(bytes) };
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to extract trace context: {ex}");
			}
			
			return Enumerable.Empty<string>();
		}


		public class ConsumingContextInstance : IConsumingContextInstance
		{
			private readonly Activity _activity;
			private readonly IDisposable _logScope;

			public ConsumingContextInstance(Activity activity, IDisposable logScope)
			{
				_activity = activity;
				_logScope = logScope;
			}

			public void Dispose()
			{
				_activity?.Dispose();
				_logScope?.Dispose();
			}

			public void NotifyUnhandledException(Exception exception)
			{
				_activity?.SetTag("error", true);
			}
		}
	}
}