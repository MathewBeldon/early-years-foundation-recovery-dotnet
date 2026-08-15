-- This fixture satisfies the .NET schema-preflight contract only.
-- It is NOT a Rails schema replica and must not be used as evidence of schema fidelity.

CREATE TABLE users (
    id bigint PRIMARY KEY,
    country text NOT NULL
);

INSERT INTO users (id, country) VALUES (1, 'Rails-owned country sentinel');

CREATE TABLE user_module_progress (id bigint PRIMARY KEY);
CREATE TABLE assessments (id bigint PRIMARY KEY);
CREATE TABLE responses (id bigint PRIMARY KEY);
CREATE TABLE notes (id bigint PRIMARY KEY);
CREATE TABLE visits (id bigint PRIMARY KEY);
CREATE TABLE events (id bigint PRIMARY KEY);
CREATE TABLE mail_events (id bigint PRIMARY KEY);
CREATE TABLE confidence_check_progress (id bigint PRIMARY KEY);
CREATE TABLE releases (id bigint PRIMARY KEY);
CREATE TABLE module_releases (id bigint PRIMARY KEY);
CREATE TABLE que_jobs (id bigint PRIMARY KEY);

CREATE TABLE schema_migrations (
    version character varying NOT NULL PRIMARY KEY
);
