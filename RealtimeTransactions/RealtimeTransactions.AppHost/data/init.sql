-- Se connecter à la base de données
\c "transactions-db";

-- Création de la table transactions
CREATE TABLE IF NOT EXISTS transactions (
    TransactionId UUID PRIMARY KEY,
    Amount DECIMAL(10, 2),
    Currency VARCHAR(3),
    Timestamp TIMESTAMP
);
