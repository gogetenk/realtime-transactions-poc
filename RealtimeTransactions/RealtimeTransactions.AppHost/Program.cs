var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddKafka("kafka")
    .WithKafkaUI();

var rmq = builder.AddRabbitMQ("rabbitmq");
var postgresql = builder.AddPostgres("postgres");
var elastic = builder.AddElasticsearch("elastic");
var seq = builder.AddSeq("seq");

builder.AddProject<Projects.TransactionService>("transactions-api")
    .WithReference(kafka)
    .WithReference(postgresql)
    .WithReference(elastic)
    .WithReference(seq);

builder.AddProject<Projects.NotificationService>("notifications-api")
    .WithReference(kafka)
    .WithReference(rmq)
    .WithReference(seq);

builder.Build().Run();
