﻿namespace NServiceBus.Transport.Msmq.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Messaging;
    using NUnit.Framework;
    using Support;

    [TestFixture]
    public class MsmqUtilitiesTests
    {
        [Test]
        public void Should_convert_a_message_back_even_if_special_characters_are_contained_in_the_headers()
        {
            string expected = $"Can u see this '{(char)0x19}' character.";

            Message message =
                MsmqUtilities.Convert(
                    new OutgoingMessage("message id",
                        new Dictionary<string, string> { { "NServiceBus.ExceptionInfo.Message", expected } }, new byte[0]));
            Dictionary<string, string> headers = MsmqUtilities.ExtractHeaders(message);

            Assert.AreEqual(expected, headers["NServiceBus.ExceptionInfo.Message"]);
        }

        [Test]
        public void Should_convert_message_headers_that_contain_nulls_at_the_end()
        {
            string expected = "Hello World";

            Console.Out.WriteLine(sizeof(char));
            Message message =
                MsmqUtilities.Convert(
                    new OutgoingMessage("message id",
                        new Dictionary<string, string> { { "NServiceBus.ExceptionInfo.Message", expected } }, new byte[0]));
            byte[] bufferWithNulls = new byte[message.Extension.Length + (10 * sizeof(char))];

            Buffer.BlockCopy(message.Extension, 0, bufferWithNulls, 0, bufferWithNulls.Length - (10 * sizeof(char)));

            message.Extension = bufferWithNulls;

            Dictionary<string, string> headers = MsmqUtilities.ExtractHeaders(message);

            Assert.AreEqual(expected, headers["NServiceBus.ExceptionInfo.Message"]);
        }

        [Test]
        public void Should_fetch_the_replyToAddress_from_responsequeue_for_backwards_compatibility()
        {
            Message message = MsmqUtilities.Convert(
                new OutgoingMessage("message id", new Dictionary<string, string>(), new byte[0]));

            message.ResponseQueue = new MessageQueue(new MsmqAddress("local", RuntimeEnvironment.MachineName).FullPath);
            Dictionary<string, string> headers = MsmqUtilities.ExtractHeaders(message);

            Assert.AreEqual("local@" + RuntimeEnvironment.MachineName, headers[Headers.ReplyToAddress]);
        }

        [Test]
        public void Should_not_override_replyToAddress()
        {
            Message message = MsmqUtilities.Convert(
                new OutgoingMessage("message id",
                    new Dictionary<string, string> { { Headers.ReplyToAddress, "SomeAddress" } }, new byte[0]));

            message.ResponseQueue = new MessageQueue(new MsmqAddress("local", RuntimeEnvironment.MachineName).FullPath);
            Dictionary<string, string> headers = MsmqUtilities.ExtractHeaders(message);

            Assert.AreEqual("SomeAddress", headers[Headers.ReplyToAddress]);
        }

        [Test]
        public void Should_not_override_messageIntent()
        {
            Message message = MsmqUtilities.Convert(
                new OutgoingMessage("message id",
                    new Dictionary<string, string> { { Headers.MessageIntent, MessageIntent.Send.ToString() } },
                    new byte[0]));

            message.AppSpecific = 3; //Send = 1, Publish = 2, Subscribe = 3, Unsubscribe = 4 and Reply = 5 
            Dictionary<string, string> headers = MsmqUtilities.ExtractHeaders(message);

            Assert.AreEqual("Send", headers[Headers.MessageIntent]);
        }

        [Test]
        public void Should_set_messageIntent_if_header_not_present()
        {
            Message message = MsmqUtilities.Convert(
                new OutgoingMessage("message id", new Dictionary<string, string>
                    (), new byte[0]));

            message.AppSpecific = 3; //Send = 1, Publish = 2, Subscribe = 3, Unsubscribe = 4 and Reply = 5 
            Dictionary<string, string> headers = MsmqUtilities.ExtractHeaders(message);

            Assert.AreEqual("Subscribe", headers[Headers.MessageIntent]);
        }

        [Test]
        public void Should_deserialize_if_trailing_bogus_data()
        {
            string expected = "Hello World";

            Message message =
                MsmqUtilities.Convert(
                    new OutgoingMessage("message id",
                        new Dictionary<string, string> { { "NServiceBus.ExceptionInfo.Message", expected } }, new byte[0]));

            var r = new Random();

            byte[] bufferWithNulls = new byte[message.Extension.Length + (10 * sizeof(char))];
            r.NextBytes(bufferWithNulls);

            Buffer.BlockCopy(message.Extension, 0, bufferWithNulls, 0, bufferWithNulls.Length - (10 * sizeof(char)));

            message.Extension = bufferWithNulls;

            Dictionary<string, string> headers = MsmqUtilities.ExtractHeaders(message);

            Assert.AreEqual(expected, headers["NServiceBus.ExceptionInfo.Message"]);
        }
    }
}