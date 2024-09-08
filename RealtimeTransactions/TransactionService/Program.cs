using Confluent.Kafka;
using Elastic.Clients.Elasticsearch;
using Npgsql;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddKafkaProducer<string, Transaction>("transactions-kafka", builder =>
{
    builder.SetValueSerializer(new JsonSerializer<Transaction>());
});
builder.AddElasticsearchClient("elastic", null, settings =>
{
    settings.DefaultMappingFor<Transaction>(m => m.IndexName("transactions"));
    settings.DefaultIndex("transactions");
});
builder.AddNpgsqlDataSource("transactions-db");

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/transactions", async (Transaction transaction, NpgsqlDataSource dataSource, IProducer<string, Transaction> kafka, ElasticsearchClient elastic) =>
{
    // Open a connection and insert into PostgreSQL
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var cmd = connection.CreateCommand();
    cmd.CommandText = "INSERT INTO transactions (TransactionId, Amount, Currency, Timestamp) VALUES (@id, @amount, @currency, @timestamp)";
    cmd.Parameters.AddWithValue("id", transaction.TransactionId);
    cmd.Parameters.AddWithValue("amount", transaction.Amount);
    cmd.Parameters.AddWithValue("currency", transaction.Currency);
    cmd.Parameters.AddWithValue("timestamp", transaction.Timestamp);
    await cmd.ExecuteNonQueryAsync();

    // Send to Kafka
    await kafka.ProduceAsync("transaction-topic", new Message<string, Transaction>() { Value = transaction });

    // Index in Elasticsearch
    await elastic.IndexAsync(transaction);

    return Results.Ok(transaction);
});

app.MapGet("/api/transactions/{id}", async (Guid id, NpgsqlDataSource dataSource) =>
{
    // Open a connection to PostgreSQL
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT TransactionId, Amount, Currency, Timestamp FROM transactions WHERE TransactionId = @id";
    cmd.Parameters.AddWithValue("id", id);

    await using var reader = await cmd.ExecuteReaderAsync();

    if (await reader.ReadAsync())
    {
        var transaction = new Transaction
        {
            TransactionId = reader.GetGuid(0),
            Amount = reader.GetDecimal(1),
            Currency = reader.GetString(2),
            Timestamp = reader.GetDateTime(3)
        };
        return Results.Ok(transaction);
    }

    return Results.NotFound();
});


app.MapGet("/api/search", async (string? currency, decimal? amount, ElasticsearchClient elastic) =>
{
    var searchResponse = await elastic.SearchAsync<Transaction>(s => s
        .Query(q => q
            .Bool(b => b
                .Must(
                    m => m.Term(t => t.Field(f => f.Currency).Value(currency)),
                    m => m.Term(r => r.Field(f => f.Amount).Value(amount.Value.ToString()))
                )
            )
        )
    );

    return Results.Ok(searchResponse.Documents);
});


app.Run();

public class Transaction
{
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public DateTime Timestamp { get; set; }
}

public class JsonSerializer<T> : ISerializer<T>
{
    public byte[] Serialize(T data, SerializationContext context)
    {
        return JsonSerializer.SerializeToUtf8Bytes(data);
    }
}