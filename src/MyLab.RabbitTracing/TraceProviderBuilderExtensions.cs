using System;
using System.Reflection;
using OpenTelemetry.Trace;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extensions for <see cref="TracerProviderBuilder"/>
    /// </summary>
    public static class TraceProviderBuilderExstensions
    {
        /// <summary>
        /// Adds RabbitMq source for tracing
        /// </summary>
        public static TracerProviderBuilder AddRabbitSource(this TracerProviderBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var assemblyName = Assembly.GetEntryAssembly().GetName().Name;
            return builder.AddSource(assemblyName);
        }
    }
}
