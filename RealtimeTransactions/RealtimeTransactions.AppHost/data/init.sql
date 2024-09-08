-- Se connecter � la base de donn�es
\c "transactions-db";

-- Cr�ation de la table transactions
CREATE TABLE IF NOT EXISTS transactions (
    TransactionId UUID PRIMARY KEY,
    Amount DECIMAL(10, 2),
    Currency VARCHAR(3),
    Timestamp TIMESTAMP
);
