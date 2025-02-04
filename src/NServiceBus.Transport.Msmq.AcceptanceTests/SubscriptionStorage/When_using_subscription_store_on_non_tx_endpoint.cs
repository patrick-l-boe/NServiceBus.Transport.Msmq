﻿namespace NServiceBus.Transport.Msmq.AcceptanceTests.SubscriptionStorage
{
    using System.Messaging;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_subscription_store_on_non_tx_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_persist_subscriptions()
        {
            var queuePath = $".\\private$\\{StorageQueueName}";

            if (MessageQueue.Exists(queuePath))
            {
                MessageQueue.Delete(queuePath);
            }

            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                            b.When(c => c.Subscribed, (session, c) => session.Publish(new MyEvent()))
                )
                .WithEndpoint<Subscriber>(b => b.When(session => session.Subscribe<MyEvent>()))
                .Done(c => c.GotTheEvent)
                .Run();

            Assert.IsTrue(ctx.GotTheEvent);

            using (var queue = new MessageQueue(queuePath))
            {
                CollectionAssert.IsNotEmpty(queue.GetAllMessages());
            }
        }

        static string StorageQueueName = "msmq.acpt.nontxsubscriptions";

        public class Context : ScenarioContext
        {
            public bool GotTheEvent { get; set; }
            public bool Subscribed { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(b =>
                {
                    b.OnEndpointSubscribed<Context>((s, context) => { context.Subscribed = true; });
                    b.DisableFeature<AutoSubscribe>();
                    b.UsePersistence<MsmqPersistence>().SubscriptionQueue(StorageQueueName);
                    var transportSettings = (MsmqTransport)b.ConfigureTransport();
                    transportSettings.TransportTransactionMode = TransportTransactionMode.None;
                    transportSettings.UseTransactionalQueues = false;
                });
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                    {
                        c.DisableFeature<AutoSubscribe>();
                        var transportSettings = (MsmqTransport)c.ConfigureTransport();
                        transportSettings.TransportTransactionMode = TransportTransactionMode.None;
                        transportSettings.UseTransactionalQueues = false;
                    }, metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));
            }

            public class MyHandler : IHandleMessages<MyEvent>
            {
                readonly Context scenarioContext;
                public MyHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.GotTheEvent = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}