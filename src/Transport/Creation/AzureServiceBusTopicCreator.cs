﻿namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.ServiceBus.Messaging;
    using Logging;
    using Settings;

    class AzureServiceBusTopicCreator : ICreateAzureServiceBusTopics
    {
        ReadOnlySettings settings;
        Func<string, ReadOnlySettings, TopicDescription> topicDescriptionFactory;
        ConcurrentDictionary<string, Task<bool>> rememberExistence = new ConcurrentDictionary<string, Task<bool>>();
        ILog logger = LogManager.GetLogger<AzureServiceBusTopicCreator>();

        public AzureServiceBusTopicCreator(ReadOnlySettings settings)
        {
            this.settings = settings;

            if (!settings.TryGet(WellKnownConfigurationKeys.Topology.Resources.Topics.DescriptionFactory, out topicDescriptionFactory))
            {
                topicDescriptionFactory = (topicPath, setting) => new TopicDescription(topicPath)
                {
                    AutoDeleteOnIdle = setting.GetOrDefault<TimeSpan>(WellKnownConfigurationKeys.Topology.Resources.Topics.AutoDeleteOnIdle),
                    DefaultMessageTimeToLive = setting.GetOrDefault<TimeSpan>(WellKnownConfigurationKeys.Topology.Resources.Topics.DefaultMessageTimeToLive),
                    DuplicateDetectionHistoryTimeWindow = setting.GetOrDefault<TimeSpan>(WellKnownConfigurationKeys.Topology.Resources.Topics.DuplicateDetectionHistoryTimeWindow),
                    EnableBatchedOperations = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Topics.EnableBatchedOperations),
                    EnableFilteringMessagesBeforePublishing = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Topics.EnableFilteringMessagesBeforePublishing),
                    EnablePartitioning = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Topics.EnablePartitioning),
                    MaxSizeInMegabytes = setting.GetOrDefault<long>(WellKnownConfigurationKeys.Topology.Resources.Topics.MaxSizeInMegabytes),
                    RequiresDuplicateDetection = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Topics.RequiresDuplicateDetection),
                    SupportOrdering = setting.GetOrDefault<bool>(WellKnownConfigurationKeys.Topology.Resources.Topics.SupportOrdering),

                    EnableExpress = setting.GetConditional<bool>(topicPath, WellKnownConfigurationKeys.Topology.Resources.Topics.EnableExpress),
                };
            }
        }

        public async Task<TopicDescription> Create(string topicPath, INamespaceManager namespaceManager)
        {
            var topicDescription = topicDescriptionFactory(topicPath, settings);

            try
            {
                if (!await ExistsAsync(topicPath, namespaceManager).ConfigureAwait(false))
                {
                    await namespaceManager.CreateTopic(topicDescription).ConfigureAwait(false);
                    logger.InfoFormat("Topic '{0}' in namespace '{1}' created.", topicDescription.Path, namespaceManager.Address);

                    var key = GenerateTopicKey(topicPath, namespaceManager);

                    await rememberExistence.AddOrUpdate(key, notFoundTopicPath => Task.FromResult(true), (updateTopicPath, previousValue) => Task.FromResult(true)).ConfigureAwait(false);
                }
                else
                {
                    logger.InfoFormat("Topic '{0}' in namespace '{1}' already exists, skipping creation.", topicDescription.Path, namespaceManager.Address);
                    logger.InfoFormat("Checking if topic '{0}' in namespace '{1}' needs to be updated.", topicDescription.Path, namespaceManager.Address);
                    var existingTopicDescription = await namespaceManager.GetTopic(topicDescription.Path).ConfigureAwait(false);
                    if (MembersAreNotEqual(existingTopicDescription, topicDescription))
                    {
                        logger.InfoFormat("Updating topic '{0}' in namespace '{1}' with new description.", topicDescription.Path, namespaceManager.Address);
                        await namespaceManager.UpdateTopic(topicDescription).ConfigureAwait(false);
                    }
                }
            }
            catch (MessagingEntityAlreadyExistsException)
            {
                // the topic already exists or another node beat us to it, which is ok
                logger.InfoFormat("Topic '{0}' in namespace '{1}' already exists, another node probably beat us to it.", topicDescription.Path, namespaceManager.Address);
            }
            catch (TimeoutException)
            {
                logger.InfoFormat("Timeout occurred on topic creation for '{0}' in namespace '{1}' going to validate if it doesn't exist.", topicDescription.Path, namespaceManager.Address);

                // there is a chance that the timeout occurred, but the topic was still created, check again
                if (!await ExistsAsync(topicDescription.Path, namespaceManager, removeCacheEntry: true).ConfigureAwait(false))
                {
                    throw;
                }

                logger.InfoFormat("Looks like topic '{0}' in namespace '{1}' exists anyway.", topicDescription.Path, namespaceManager.Address);
            }
            catch (MessagingException ex)
            {
                var loggedMessage = string.Format("{1} {2} occurred on topic creation '{0}' in namespace {3}.", topicDescription.Path, (ex.IsTransient ? "Transient" : "Non transient"), ex.GetType().Name, namespaceManager.Address);

                if (!ex.IsTransient)
                {
                    logger.Fatal(loggedMessage, ex);
                    throw;
                }

                logger.Info(loggedMessage, ex);
            }

            return topicDescription;
        }

        async Task<bool> ExistsAsync(string topicPath, INamespaceManager namespaceClient, bool removeCacheEntry = false)
        {
            var key = GenerateTopicKey(topicPath, namespaceClient);
            logger.InfoFormat("Checking existence cache for '{0}' in namespace '{1}'.", topicPath, namespaceClient.Address);

            if (removeCacheEntry)
            {
                Task<bool> dummy;
                rememberExistence.TryRemove(key, out dummy);
            }

            var exists = await rememberExistence.GetOrAdd(key, notFoundTopicPath =>
            {
                logger.InfoFormat("Checking namespace for existence of the topic '{0}' in namespace '{1}'.", topicPath, namespaceClient.Address);
                return namespaceClient.TopicExists(topicPath);
            }).ConfigureAwait(false);

            logger.InfoFormat("Determined, from cache, that the topic '{0}' in namespace '{2}' {1}.", topicPath, exists ? "exists" : "does not exist", namespaceClient.Address);

            return exists;
        }

        bool MembersAreNotEqual(TopicDescription existingDescription, TopicDescription newDescription)
        {
            if (existingDescription.RequiresDuplicateDetection != newDescription.RequiresDuplicateDetection)
            {
                logger.Warn("RequiresDuplicateDetection cannot be update on the existing queue!");
            }
            if (existingDescription.EnablePartitioning != newDescription.EnablePartitioning)
            {
                logger.Warn("EnablePartitioning cannot be update on the existing queue!");
            }

            return existingDescription.AutoDeleteOnIdle != newDescription.AutoDeleteOnIdle
                   || existingDescription.MaxSizeInMegabytes != newDescription.MaxSizeInMegabytes
                   || existingDescription.DefaultMessageTimeToLive != newDescription.DefaultMessageTimeToLive
                   || existingDescription.DuplicateDetectionHistoryTimeWindow != newDescription.DuplicateDetectionHistoryTimeWindow
                   || existingDescription.EnableBatchedOperations != newDescription.EnableBatchedOperations
                   || existingDescription.SupportOrdering != newDescription.SupportOrdering
                   || existingDescription.EnableExpress != newDescription.EnableExpress
                   || existingDescription.EnableFilteringMessagesBeforePublishing != newDescription.EnableFilteringMessagesBeforePublishing;
        }

        static string GenerateTopicKey(string topicPath, INamespaceManager namespaceClient)
        {
            return topicPath + namespaceClient.Address;
        }
    }
}