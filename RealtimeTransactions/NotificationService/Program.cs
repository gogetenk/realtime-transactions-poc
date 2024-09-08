using Confluent.Kafka;
using Elastic.Clients.Elasticsearch;
using NotificationService.Consumers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddKafkaConsumer<string, string>("transactions-kafka", builder =>
{
    builder.Config.GroupId = "my-consumer-group";
    builder.Config.AutoOffsetReset = AutoOffsetReset.Earliest;
    builder.Config.EnableAutoCommit = false;
    builder.Config.ApiVersionRequest = false;
});
builder.AddElasticsearchClient("elastic");
builder.AddRabbitMQClient("rabbitmq");

builder.Services.AddHostedService<KafkaNotificationConsumer>(); // Add Background Service

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


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