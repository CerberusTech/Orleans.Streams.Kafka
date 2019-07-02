﻿using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Orleans.Streams.Kafka.E2E.Extensions;
using Orleans.Streams.Kafka.E2E.Grains;
using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Orleans.Streams.Kafka.E2E.Tests
{
	public class AvroDeserilizationTests : TestBase
	{
		private const int ReceiveDelay = 500;

		public AvroDeserilizationTests()
		{
			Initialize(1); // Initialize with three silos so that queues will be load-balanced on different silos.
		}

		[Fact]
		public async Task ProduceConsumeExternalMessage()
		{
			var config = GetKafkaServerConfig();

			var testMessage = TestModelAvro.Random();

			var completion = new TaskCompletionSource<bool>();

			var provider = Cluster.Client.GetStreamProvider(Consts.KafkaStreamProvider);
			var stream = provider.GetStream<TestModelAvro>(Consts.StreamId4, Consts.StreamNamespaceExternalAvro);

			await stream.QuickSubscribe((message, seq) =>
			{
				Assert.Equal(testMessage, message);
				completion.SetResult(true);
				return Task.CompletedTask;
			});

			await Task.Delay(5000);

			using (var schema = new CachedSchemaRegistryClient(new SchemaRegistryConfig
			{
				SchemaRegistryUrl = "https://dev-data.rivertech.dev/schema-registry"
			}))
			using (var producer = new ProducerBuilder<byte[], TestModelAvro>(config)
				.SetValueSerializer(new AvroSerializer<TestModelAvro>(schema).AsSyncOverAsync())
				.Build()
			)
			{
				await producer.ProduceAsync(Consts.StreamNamespaceExternalAvro, new Message<byte[], TestModelAvro>
				{
					Key = Encoding.UTF8.GetBytes(Consts.StreamId4),
					Value = testMessage,
					Timestamp = new Timestamp(DateTimeOffset.UtcNow)
				});
			}

			await Task.WhenAny(completion.Task, Task.Delay(ReceiveDelay * 4));

			if (!completion.Task.IsCompleted)
				throw new XunitException("Message not received.");
		}

		private static ClientConfig GetKafkaServerConfig()
			=> new ClientConfig
			{
				BootstrapServers = string.Join(',', Brokers)
			};
	}
}