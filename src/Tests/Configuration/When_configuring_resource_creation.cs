namespace NServiceBus.AzureServiceBus.Tests
{
    using System;
    using Microsoft.ServiceBus.Messaging;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture]
    [Category("AzureServiceBus")]
    public class When_configuring_resource_creation
    {
        [Test]
        public void Should_be_able_to_set_queue_description_factory_method()
        {
            var settings = new SettingsHolder();
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            Func<string, ReadOnlySettings, QueueDescription> registeredFactory = (name, s) => new QueueDescription(name);

            var connectivitySettings = extensions.UseDefaultTopology().Resources().Queues().DescriptionFactory(registeredFactory);

            Assert.AreEqual(registeredFactory, connectivitySettings.GetSettings().Get<Func<string, ReadOnlySettings, QueueDescription>>(WellKnownConfigurationKeys.Topology.Resources.Queues.DescriptionFactory));
        }

        [Test]
        public void Should_be_able_to_set_topic_description_factory_method()
        {
            var settings = new SettingsHolder();
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            Func<string, ReadOnlySettings, TopicDescription> registeredFactory = (name, s) => new TopicDescription(name);

            var connectivitySettings = extensions.UseDefaultTopology().Resources().Topics().DescriptionFactory(registeredFactory);

            Assert.AreEqual(registeredFactory, connectivitySettings.GetSettings().Get<Func<string, ReadOnlySettings, TopicDescription>>(WellKnownConfigurationKeys.Topology.Resources.Topics.DescriptionFactory));
        }

        [Test]
        public void Should_be_able_to_set_subscription_description_factory_method()
        {
            var settings = new SettingsHolder();
            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);

            Func<string, string, ReadOnlySettings, SubscriptionDescription> registeredFactory = (topicname, subscriptionname, s) => new SubscriptionDescription(topicname, subscriptionname);

            var connectivitySettings = extensions.UseDefaultTopology().Resources().Subscriptions().DescriptionFactory(registeredFactory);

            Assert.AreEqual(registeredFactory, connectivitySettings.GetSettings().Get<Func<string, string, ReadOnlySettings, SubscriptionDescription>>(WellKnownConfigurationKeys.Topology.Resources.Subscriptions.DescriptionFactory));
        }

    }
}