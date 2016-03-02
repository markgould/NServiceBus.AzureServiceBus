﻿namespace NServiceBus
{
    using Microsoft.ServiceBus;
    using NServiceBus.AzureServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    public class AzureServiceBusTransport : TransportDefinition
    {
        protected override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            settings.SetDefault("Transactions.DoNotWrapHandlersExecutionInATransactionScope", true);
            settings.SetDefault("Transactions.SuppressDistributedTransactions", true);
            settings.SetDefault<ITopology>(new StandardTopology());

            var topology = settings.Get<ITopology>();
            topology.Initialize(settings);

            RegisterConnectionStringAsNamespace(connectionString, settings);

            SetConnectivityMode(settings);

            return new AzureServiceBusTransportInfrastructure(topology, settings);
        }

        private static void SetConnectivityMode(SettingsHolder settings)
        {
            ServiceBusEnvironment.SystemConnectivity.Mode = settings.Get<ConnectivityMode>(WellKnownConfigurationKeys.Connectivity.ConnectivityMode);
        }

        private static void RegisterConnectionStringAsNamespace(string connectionString, ReadOnlySettings settings)
        {
            var namespaces = settings.Get<NamespaceConfigurations>(WellKnownConfigurationKeys.Topology.Addressing.Partitioning.Namespaces);
            namespaces.AddDefault(connectionString);
        }

        public override bool RequiresConnectionString { get; } = true;

        public override string ExampleConnectionStringForErrorMessage { get; } = "Endpoint=sb://[namespace].servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=[secret_key]";
    }

}