using Confluent.Kafka;
using RabbitMQ.Client;
using System.Text.Json;

namespace NotificationService.Consumers;

public class KafkaNotificationConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _kafkaConsumer;
    private readonly IConnection _rmqConnection;
    private readonly IModel _rabbitMqChannel;

    public KafkaNotificationConsumer(IConsumer<string, string> kafkaConsumer, IConnection rmqConnection)
    {
        _kafkaConsumer = kafkaConsumer;
        _rmqConnection = rmqConnection;
        _rabbitMqChannel = rmqConnection.CreateModel();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = _kafkaConsumer.Consume(stoppingToken);
                var transaction = JsonSerializer.Deserialize<Transaction>(consumeResult.Message.Value);

                // Process transaction (e.g., notification logic)
                var notificationMessage = $"Transaction {transaction.TransactionId} for {transaction.Amount} {transaction.Currency} completed successfully.";

                // Send notification to RabbitMQ
                var body = System.Text.Encoding.UTF8.GetBytes(notificationMessage);
                _rabbitMqChannel.BasicPublish(exchange: "", routingKey: "notification_queue", basicProperties: null, body: body);

                // Optionally log or perform other actions here
                Console.WriteLine("Notification sent to RabbitMQ: " + notificationMessage);
            }
        });
    }
}