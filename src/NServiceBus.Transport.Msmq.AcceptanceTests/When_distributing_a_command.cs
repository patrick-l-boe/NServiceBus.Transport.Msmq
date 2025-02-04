﻿namespace NServiceBus.Transport.Msmq.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Configuration.AdvancedExtensibility;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Routing;
    using Settings;

    public class When_distributing_a_command : NServiceBusAcceptanceTest
    {
        const int numberOfMessagesToSendPerEndpoint = 20;
        static string ReceiverAEndpoint => Conventions.EndpointNamingConvention(typeof(ReceiverA));
        static string ReceiverBEndpoint => Conventions.EndpointNamingConvention(typeof(ReceiverB));

        [Test]
        public async Task Should_round_robin()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When((session, c) => session.Send(new RequestA())))
                .WithEndpoint<ReceiverA>(b => b.CustomConfig(c =>
                {
                    c.MakeInstanceUniquelyAddressable("1");
                }))
                .WithEndpoint<ReceiverA>(b => b.CustomConfig(c =>
                {
                    c.MakeInstanceUniquelyAddressable("2");
                }))
                .WithEndpoint<ReceiverB>(b => b.CustomConfig(c =>
                {
                    c.MakeInstanceUniquelyAddressable("1");
                }))
                .WithEndpoint<ReceiverB>(b => b.CustomConfig(c =>
                {
                    c.MakeInstanceUniquelyAddressable("2");
                }))
                .Done(c => c.MessagesReceivedPerEndpoint == numberOfMessagesToSendPerEndpoint)
                .Run();

            Assert.That(context.ReceiverA1TimesCalled, Is.EqualTo(10));
            Assert.That(context.ReceiverA2TimesCalled, Is.EqualTo(10));
            Assert.That(context.ReceiverB1TimesCalled, Is.EqualTo(10));
            Assert.That(context.ReceiverB2TimesCalled, Is.EqualTo(10));
        }

        public class Context : ScenarioContext
        {
            public int MessagesReceivedPerEndpoint { get; set; }
            public int ReceiverA1TimesCalled { get; set; }
            public int ReceiverA2TimesCalled { get; set; }
            public int ReceiverB1TimesCalled { get; set; }
            public int ReceiverB2TimesCalled { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(RequestA), ReceiverAEndpoint);
                    routing.RouteToEndpoint(typeof(RequestB), ReceiverBEndpoint);

                    c.GetSettings().GetOrCreate<EndpointInstances>().AddOrReplaceInstances("test", new List<EndpointInstance>
                    {
                        new EndpointInstance(ReceiverAEndpoint, "1"),
                        new EndpointInstance(ReceiverAEndpoint, "2"),
                        new EndpointInstance(ReceiverBEndpoint, "1"),
                        new EndpointInstance(ReceiverBEndpoint, "2")
                    });
                });
            }

            public class ResponseHandler :
                IHandleMessages<ResponseA>,
                IHandleMessages<ResponseB>
            {
                Context testContext;

                public ResponseHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(ResponseA message, IMessageHandlerContext context)
                {
                    switch (message.EndpointInstance)
                    {
                        case "1":
                            testContext.ReceiverA1TimesCalled++;
                            break;
                        case "2":
                            testContext.ReceiverA2TimesCalled++;
                            break;
                        default:
                            break;
                    }

                    return context.Send(new RequestB());
                }

                public Task Handle(ResponseB message, IMessageHandlerContext context)
                {
                    switch (message.EndpointInstance)
                    {
                        case "1":
                            testContext.ReceiverB1TimesCalled++;
                            break;
                        case "2":
                            testContext.ReceiverB2TimesCalled++;
                            break;
                        default:
                            break;
                    }

                    testContext.MessagesReceivedPerEndpoint++;
                    if (testContext.MessagesReceivedPerEndpoint < numberOfMessagesToSendPerEndpoint)
                    {
                        return context.Send(new RequestA());
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class ReceiverA : EndpointConfigurationBuilder
        {
            public ReceiverA()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<RequestA>
            {
                readonly IReadOnlySettings settings;
                public MyMessageHandler(IReadOnlySettings settings)
                {
                    this.settings = settings;
                }

                public Task Handle(RequestA message, IMessageHandlerContext context)
                {
                    return context.Reply(new ResponseA
                    {
                        EndpointInstance = settings.Get<string>("EndpointInstanceDiscriminator")
                    });
                }
            }
        }

        public class ReceiverB : EndpointConfigurationBuilder
        {
            public ReceiverB()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<RequestB>
            {
                readonly IReadOnlySettings settings;
                public MyMessageHandler(IReadOnlySettings settings)
                {
                    this.settings = settings;
                }

                public Task Handle(RequestB message, IMessageHandlerContext context)
                {
                    return context.Reply(new ResponseB
                    {
                        EndpointInstance = settings.Get<string>("EndpointInstanceDiscriminator")
                    });
                }
            }
        }

        public class RequestA : ICommand
        {
        }

        public class RequestB : ICommand
        {
        }

        public class ResponseA : IMessage
        {
            public string EndpointInstance { get; set; }
        }

        public class ResponseB : IMessage
        {
            public string EndpointInstance { get; set; }
        }
    }
}