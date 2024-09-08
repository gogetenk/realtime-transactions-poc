using Confluent.Kafka;
using Elastic.Clients.Elasticsearch;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddKafkaConsumer<string, Transaction>("transactions-kafka", builder =>
{
    builder.SetValueDeserializer(new JsonDeserializer<Transaction>());
});
builder.AddElasticsearchClient("elastic");
builder.AddRabbitMQClient("rabbitmq");

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Consume Kafka transactions and process notifications
app.MapPost("/api/notifications", async (IConsumer<string, Transaction> kafka, IModel rabbitMqChannel, ElasticsearchClient elastic) =>
{
    // Kafka Consumer Configuration
    var consumeResult = kafka.Consume();
    var transaction = consumeResult.Message.Value;

    // Process transaction (example: notification logic)
    var notificationMessage = $"Transaction {transaction.TransactionId} for {transaction.Amount} {transaction.Currency} completed successfully.";

    // Send notification to RabbitMQ
    var body = System.Text.Encoding.UTF8.GetBytes(notificationMessage);
    rabbitMqChannel.BasicPublish(exchange: "", routingKey: "notification_queue", basicProperties: null, body: body);

    // Optionally index the notification in Elasticsearch
    await elastic.IndexAsync(new
    {
        TransactionId = transaction.TransactionId,
        Message = notificationMessage,
        Timestamp = DateTime.UtcNow
    });

    return Results.Ok(notificationMessage);
});

// Retrieve sent notifications from RabbitMQ
app.MapGet("/api/notifications", () =>
{
    var factory = new ConnectionFactory() { HostName = "localhost" };
    using var connection = factory.CreateConnection();
    using var channel = connection.CreateModel();

    channel.QueueDeclare(queue: "notification_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);

    var consumer = new EventingBasicConsumer(channel);
    var messages = new List<string>();

    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = System.Text.Encoding.UTF8.GetString(body);
        messages.Add(message);
    };

    channel.BasicConsume(queue: "notification_queue", autoAck: true, consumer: consumer);

    return Results.Ok(messages);
});

app.Run();

public class Transaction
{
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public DateTime Timestamp { get; set; }
}

public class JsonDeserializer<T> : IDeserializer<T>
{
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        return JsonSerializer.Deserialize<T>(data);
    }
}