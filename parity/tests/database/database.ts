import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { Client } from 'pg';

const identifier = /^[a-z_][a-z0-9_]*$/;
const resetAdvisoryLock = 1_905_202_026;
const comparisonAdvisoryLock = 1_905_202_027;

export interface CaptureTable {
  table: string;
  columns: string[];
  orderBy: string[];
  volatileFields: string[];
}

export interface CapturePlan {
  tables: CaptureTable[];
}

export function validateCapturePlan(
  plan: CapturePlan,
  options: { requireApplicationTable?: boolean } = {},
): void {
  if (plan.tables.length === 0) {
    throw new Error('Database capture plan must contain at least one table.');
  }

  for (const table of plan.tables) {
    quoteIdentifier(table.table);
    if (table.columns.length === 0) {
      throw new Error(`Database capture table ${table.table} has no columns.`);
    }
    if (table.orderBy.length === 0) {
      throw new Error(`Database capture table ${table.table} has no deterministic orderBy.`);
    }

    for (const column of [...table.columns, ...table.orderBy, ...table.volatileFields]) {
      quoteIdentifier(column);
    }
    for (const field of table.volatileFields) {
      if (!table.columns.includes(field)) {
        throw new Error(
          `Volatile field ${table.table}.${field} is not present in captured columns.`,
        );
      }
    }
  }

  if (
    options.requireApplicationTable === true &&
    !plan.tables.some((table) => table.table !== 'schema_migrations')
  ) {
    throw new Error('Write parity capture plan must include at least one application table.');
  }
}

export interface DatabaseResetOptions {
  databaseUrl: string;
  templateDatabase: string;
  fixturePath: string;
  allowReset: boolean;
  allowedHosts?: readonly string[];
}

const defaultAllowedResetHosts = ['db', 'localhost', '127.0.0.1', '[::1]'] as const;

export async function withDatabaseComparisonLock<T>(
  databaseUrl: string,
  operation: () => Promise<T>,
): Promise<T> {
  targetDatabase(databaseUrl);
  const maintenanceUrl = new URL(databaseUrl);
  maintenanceUrl.pathname = '/postgres';
  const client = new Client({ connectionString: maintenanceUrl.toString() });
  await client.connect();

  try {
    await client.query('SELECT pg_advisory_lock($1)', [comparisonAdvisoryLock]);
    return await operation();
  } finally {
    await client.query('SELECT pg_advisory_unlock($1)', [comparisonAdvisoryLock]);
    await client.end();
  }
}

function quoteIdentifier(value: string): string {
  if (!identifier.test(value)) {
    throw new Error(`Unsafe PostgreSQL identifier: ${value}`);
  }

  return `"${value}"`;
}

function targetDatabase(databaseUrl: string): string {
  const url = new URL(databaseUrl);
  if (!['postgres:', 'postgresql:'].includes(url.protocol)) {
    throw new Error(`Unsupported parity database protocol: ${url.protocol}`);
  }

  const name = decodeURIComponent(url.pathname.slice(1));
  if (!name.endsWith('_test') && !name.endsWith('_parity')) {
    throw new Error(`Refusing to reset non-test database: ${name}`);
  }

  quoteIdentifier(name);

  return name;
}

function validateResetTarget(options: DatabaseResetOptions): string {
  const url = new URL(options.databaseUrl);
  const database = targetDatabase(options.databaseUrl);
  const allowedHosts = options.allowedHosts ?? defaultAllowedResetHosts;
  if (!allowedHosts.includes(url.hostname)) {
    throw new Error(
      `Refusing database reset on host ${url.hostname}; allowed hosts: ${allowedHosts.join(', ')}.`,
    );
  }

  const template = options.templateDatabase;
  if (!template.endsWith('_test') && !template.endsWith('_parity')) {
    throw new Error(`Refusing non-test template database: ${template}`);
  }
  quoteIdentifier(template);

  if (database === template) {
    throw new Error('Parity target and template databases must be different.');
  }

  return database;
}

export async function resetDatabase(options: DatabaseResetOptions): Promise<void> {
  if (!options.allowReset) {
    throw new Error('Database reset requires PARITY_ALLOW_DATABASE_RESET=true.');
  }

  const database = validateResetTarget(options);
  const maintenanceUrl = new URL(options.databaseUrl);
  maintenanceUrl.pathname = '/postgres';
  const client = new Client({ connectionString: maintenanceUrl.toString() });
  await client.connect();

  try {
    await client.query('SELECT pg_advisory_lock($1)', [resetAdvisoryLock]);
    await client.query(
      'SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = $1 AND pid <> pg_backend_pid()',
      [database],
    );
    await client.query(`DROP DATABASE IF EXISTS ${quoteIdentifier(database)}`);
    await client.query(
      `CREATE DATABASE ${quoteIdentifier(database)} TEMPLATE ${quoteIdentifier(options.templateDatabase)}`,
    );
  } finally {
    await client.query('SELECT pg_advisory_unlock($1)', [resetAdvisoryLock]);
    await client.end();
  }

  const fixture = await fs.readFile(options.fixturePath, 'utf8');
  if (fixture.trim()) {
    const fixtureClient = new Client({ connectionString: options.databaseUrl });
    await fixtureClient.connect();
    try {
      await fixtureClient.query(fixture);
    } finally {
      await fixtureClient.end();
    }
  }
}

export async function captureDatabaseState(
  databaseUrl: string,
  plan: CapturePlan,
): Promise<Record<string, unknown[]>> {
  targetDatabase(databaseUrl);
  validateCapturePlan(plan);
  const client = new Client({ connectionString: databaseUrl });
  await client.connect();
  const capture: Record<string, unknown[]> = {};

  try {
    for (const table of plan.tables) {
      const columns = table.columns.map(quoteIdentifier).join(', ');
      const orderBy = table.orderBy.map(quoteIdentifier).join(', ');
      const result = await client.query(
        `SELECT ${columns} FROM ${quoteIdentifier(table.table)} ORDER BY ${orderBy}`,
      );
      capture[table.table] = result.rows.map((row: Record<string, unknown>) =>
        Object.fromEntries(
          Object.entries(row)
            .filter(([field]) => !table.volatileFields.includes(field))
            .map(([field, value]) => [field, value instanceof Date ? value.toISOString() : value]),
        ),
      );
    }
  } finally {
    await client.end();
  }

  return capture;
}

export function compareDatabaseState(
  rails: Record<string, unknown[]>,
  dotnet: Record<string, unknown[]>,
): void {
  assert.deepStrictEqual(dotnet, rails);
}
