BEGIN TRANSACTION;

CREATE TABLE consumer_messages (
    id NVARCHAR(255) NOT NULL,
    created_at BIGINT NOT NULL,
    available_after BIGINT NOT NULL,
    attempts INT NOT NULL,
    consumer_type NVARCHAR(255) NOT NULL,
    payload_type NVARCHAR(255) NOT NULL,
    payload NVARCHAR(MAX) NOT NULL,
    insert_id NVARCHAR(255) NOT NULL,
    CONSTRAINT PK_consumer_messages PRIMARY KEY (id)
);

CREATE TABLE poisoned_messages (
    id NVARCHAR(255) NOT NULL,
    created_at BIGINT NOT NULL,
    available_after BIGINT NOT NULL,
    attempts INT NOT NULL,
    consumer_type NVARCHAR(255) NOT NULL,
    payload_type NVARCHAR(255) NOT NULL,
    payload NVARCHAR(MAX) NOT NULL,
    insert_id NVARCHAR(255) NOT NULL,
    CONSTRAINT PK_poisoned_messages PRIMARY KEY (id)
);

CREATE TABLE scheduled_messages (
    id NVARCHAR(255) NOT NULL,
    tag NVARCHAR(255),
    available_after BIGINT NOT NULL,
    chron_expression NVARCHAR(255) NOT NULL,
    chron_timezone NVARCHAR(255) NOT NULL,
    payload_type NVARCHAR(255) NOT NULL,
    payload NVARCHAR(MAX) NOT NULL,
    CONSTRAINT PK_scheduled_messages PRIMARY KEY (id)
);

CREATE TABLE submitted_values (
    Id INT IDENTITY(1,1),
    value FLOAT NOT NULL,
    CONSTRAINT PK_submitted_values PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_consumer_messages_insert_id_consumer_type ON consumer_messages (insert_id, consumer_type);

COMMIT;
