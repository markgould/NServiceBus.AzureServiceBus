﻿namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;
    using Settings;
    using Transport.AzureServiceBus;

    /// <summary>
    /// Individualization API settings.
    /// </summary>
    public class AzureServiceBusIndividualizationSettings : ExposeSettings
    {
        internal AzureServiceBusIndividualizationSettings(SettingsHolder settings) : base(settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// Provide individualization strategy to use.
        /// <remarks>Default is <see cref="DiscriminatorBasedIndividualization" /></remarks>
        /// <seealso cref="CoreIndividualization" />
        /// <seealso cref="DiscriminatorBasedIndividualization" />
        /// </summary>
        public AzureServiceBusIndividualizationExtensionPoint<T> UseStrategy<T>() where T : IIndividualizationStrategy
        {
            settings.Set(WellKnownConfigurationKeys.Topology.Addressing.Individualization.Strategy, typeof(T));

            return new AzureServiceBusIndividualizationExtensionPoint<T>(settings);
        }

        SettingsHolder settings;
    }
}