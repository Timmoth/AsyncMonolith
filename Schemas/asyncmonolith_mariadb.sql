START TRANSACTION;

CREATE TABLE consumer_messages (
    id VARCHAR(255) NOT NULL,
    created_at BIGINT NOT NULL,
    available_after BIGINT NOT NULL,
    attempts INT NOT NULL,
    consumer_type VARCHAR(255) NOT NULL,
    payload_type VARCHAR(255) NOT NULL,
    payload TEXT NOT NULL,
    insert_id VARCHAR(255) NOT NULL,
    trace_id VARCHAR(255) NOT NULL,
    span_id VARCHAR(255) NOT NULL,
    PRIMARY KEY (id)
);

CREATE TABLE poisoned_messages (
    id VARCHAR(255) NOT NULL,
    created_at BIGINT NOT NULL,
    available_after BIGINT NOT NULL,
    attempts INT NOT NULL,
    consumer_type VARCHAR(255) NOT NULL,
    payload_type VARCHAR(255) NOT NULL,
    payload TEXT NOT NULL,
    insert_id VARCHAR(255) NOT NULL,
    trace_id VARCHAR(255) NOT NULL,
    span_id VARCHAR(255) NOT NULL,
    PRIMARY KEY (id)
);

CREATE TABLE scheduled_messages (
    id VARCHAR(255) NOT NULL,
    tag VARCHAR(255),
    available_after BIGINT NOT NULL,
    chron_expression VARCHAR(255) NOT NULL,
    chron_timezone VARCHAR(255) NOT NULL,
    payload_type VARCHAR(255) NOT NULL,
    payload TEXT NOT NULL,
    PRIMARY KEY (id)
);

CREATE TABLE submitted_values (
    Id INT AUTO_INCREMENT,
    value DOUBLE NOT NULL,
    PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_consumer_messages_insert_id_consumer_type ON consumer_messages (insert_id, consumer_type);

COMMIT;
