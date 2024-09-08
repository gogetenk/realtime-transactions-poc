var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddKafka("kafka")
    .WithKafkaUI();

var rmq = builder.AddRabbitMQ("rabbitmq")
    .WithRabbitMQUI();

builder.AddProject<Projects.TransactionService>("transactionservice")
    .WithReference(kafka);

builder.AddProject<Projects.NotificationService>("notificationservice")
    .WithReference(kafka);

builder.Build().Run();
