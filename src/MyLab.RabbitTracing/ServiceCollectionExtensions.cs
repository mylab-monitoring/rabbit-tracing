using System;
using MyLab.RabbitTracing;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extensions for <see cref="IServiceCollection"/>
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds supporting for OpenTelemetry tracing
        /// </summary>
        public static IServiceCollection AddRabbitTracing(this IServiceCollection srv)
        {
            if (srv == null) throw new ArgumentNullException(nameof(srv));

            return srv
                .AddRabbitConsumingContext<ConsumingContext>()
                .AddRabbitPublishingContext<PublishingContext>();
        }
    }
}
