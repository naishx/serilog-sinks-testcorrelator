﻿using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Xunit;

namespace Serilog.Utilities.ConcurrentCorrelator.Tests
{
    public class CorrelationLogContextTests
    {
        public CorrelationLogContextTests()
        {
            TestSerilogLogEvents.ConfigureGlobalLoggerForTesting();
        }

        [Fact]
        [Test]
        public void A_CorrelationLogContext_does_enrich_LogEvents_inside_its_scope()
        {
            using (var correlationLogContext = new CorrelationLogContext())
            {
                Log.Logger.Information("Message template.");

                TestSerilogLogEvents.WithCorrelationLogContextGuid(correlationLogContext.Guid)
                    .Should()
                    .OnlyContain(logEvent => logEvent.MessageTemplate.Text == "Message template.");
            }
        }

        [Fact]
        [Test]
        public void A_CorrelationLogContext_does_not_enrich_LogEvents_outside_its_scope()
        {
            Guid correlationLogContextGuid;

            using (var correlationLogContext = new CorrelationLogContext())
            {
                correlationLogContextGuid = correlationLogContext.Guid;
            }

            Log.Logger.Information("Message template.");

            TestSerilogLogEvents.WithCorrelationLogContextGuid(correlationLogContextGuid)
                .Should()
                .NotContain(logEvent => logEvent.MessageTemplate.Text == "Message template.");
        }

        [Fact]
        [Test]
        public void A_CorrelationLogContext_does_enrich_LogEvents_inside_the_same_logical_call_context()
        {
            using (var context = new CorrelationLogContext())
            {
                var logTask = Task.Run(() =>
                {
                    Log.Logger.Information("Message template.");
                });

                Task.WaitAll(logTask);

                TestSerilogLogEvents.WithCorrelationLogContextGuid(context.Guid)
                    .Should()
                    .Contain(logEvent => logEvent.MessageTemplate.Text == "Message template.");
            }
        }

        [Fact]
        [Test]
        public void A_CorrelationLogContext_does_not_enrich_LogEvents_outside_the_same_logical_call_context()
        {
            var usingEnteredSignal = new ManualResetEvent(false);

            var loggingFinishedSignal = new ManualResetEvent(false);

            var logTask = Task.Run(() =>
            {
                usingEnteredSignal.WaitOne();

                Log.Logger.Information("Message template.");

                loggingFinishedSignal.Set();
            });

            var logContextTask = Task.Run(() =>
            {
                using (var context = new CorrelationLogContext())
                {
                    usingEnteredSignal.Set();
                    loggingFinishedSignal.WaitOne();
                    return context.Guid;
                }
            });

            Task.WaitAll(logTask, logContextTask);

            TestSerilogLogEvents.WithCorrelationLogContextGuid(logContextTask.Result)
                .Should()
                .NotContain(logEvent => logEvent.MessageTemplate.Text == "Message template.");
        }

        [Fact]
        [Test]
        public void A_CorrelationLogContext_within_a_CorrelationLogContext_adds_an_additional_CorrelationLogContext_to_LogEvents()
        {
            using (var outerCorrelationLogContext = new CorrelationLogContext())
            {
                using (var innerCorrelationLogContext = new CorrelationLogContext())
                {
                    Log.Logger.Information("Message template.");

                    TestSerilogLogEvents.WithCorrelationLogContextGuid(innerCorrelationLogContext.Guid)
                        .Should()
                        .OnlyContain(logEvent => logEvent.MessageTemplate.Text == "Message template.");

                    TestSerilogLogEvents.WithCorrelationLogContextGuid(outerCorrelationLogContext.Guid)
                        .Should()
                        .OnlyContain(logEvent => logEvent.MessageTemplate.Text == "Message template.");
                }
            }
        }
    }
}
