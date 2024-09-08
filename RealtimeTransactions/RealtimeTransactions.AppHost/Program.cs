var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddKafka("transactions-kafka")
    .WithKafkaUI();

var postgresql = builder.AddPostgres("postgres")
    .WithPgWeb()
    .WithEnvironment("POSTGRES_DB", "transactions-db")
    .WithBindMount(
        "data",
        "/docker-entrypoint-initdb.d");
var postgresdb = postgresql.AddDatabase("transactions-db");

var rmq = builder.AddRabbitMQ("rabbitmq");
var elastic = builder.AddElasticsearch("elastic");
var seq = builder.AddSeq("seq");

builder.AddProject<Projects.TransactionService>("transactions-api")
    .WithReference(kafka)
    .WithReference(postgresdb)
    .WithReference(elastic)
    .WithReference(seq);

builder.AddProject<Projects.NotificationService>("notifications-api")
    .WithReference(kafka)
    .WithReference(rmq)
    .WithReference(seq);

builder.Build().Run();
