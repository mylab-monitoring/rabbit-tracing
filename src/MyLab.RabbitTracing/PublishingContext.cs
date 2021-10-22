using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using MyLab.Log.Dsl;
using MyLab.RabbitClient.Publishing;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace MyLab.RabbitTracing
{
	class PublishingContext : IPublishingContext
	{
		public PublishingContext(ILogger<PublishingContext> logger)
		{
			_logger = logger;
		}

		private static readonly ActivitySource ActivitySource = new ActivitySource("MessagePublisher");
		private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
		private readonly ILogger<PublishingContext> _logger;

		private void InjectTraceContextIntoBasicProperties(IBasicProperties props, string key, string value)
		{
			try
			{
				if (props.Headers == null)
				{
					props.Headers = new Dictionary<string, object>();
				}

				props.Headers[key] = value;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to inject trace context.");
			}
		}

		public IDisposable Set(RabbitPublishingMessage publishingMessage)
		{
			// Start an activity with a name following the semantic convention of the OpenTelemetry messaging specification.
			// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md#span-name
			var destination = string.IsNullOrWhiteSpace(publishingMessage.Exchange) ? publishingMessage.RoutingKey : publishingMessage.Exchange;
			var activityName = $"{(string.IsNullOrWhiteSpace(destination) ? "queue": destination)} send";
			var activity = ActivitySource.StartActivity(activityName, ActivityKind.Producer);
			ActivityContext contextToInject = default;
			if (activity != null)
			{
				contextToInject = activity.Context;
			}
			else if (Activity.Current != null)
			{
				contextToInject = Activity.Current.Context;
			}

			// Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
			Propagator.Inject(new PropagationContext(contextToInject, Baggage.Current), publishingMessage.BasicProperties, this.InjectTraceContextIntoBasicProperties);

			// The OpenTelemetry messaging specification defines a number of attributes. These attributes are added here.
			AddMessagingTags(activity, publishingMessage);

			return activity;
		}

		public static void AddMessagingTags(Activity activity, RabbitPublishingMessage ea)
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
	}
}