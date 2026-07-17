import path from 'node:path';
import { resetDatabase } from './database.js';
import { test, expect } from '../support/fixtures.js';

test('database reset is opt-in and rejects accidental execution', async () => {
  await expect(resetDatabase({
    databaseUrl: 'postgres://postgres:password@db:5432/early_years_foundation_recovery_parity',
    templateDatabase: 'early_years_foundation_recovery_test',
    fixturePath: path.resolve('fixtures/canonical.sql'),
    allowReset: false,
  })).rejects.toThrow('PARITY_ALLOW_DATABASE_RESET=true');
});

test('database reset rejects a non-test target before connecting', async () => {
  await expect(resetDatabase({
    databaseUrl: 'postgres://postgres:password@db:5432/production',
    templateDatabase: 'early_years_foundation_recovery_test',
    fixturePath: path.resolve('fixtures/canonical.sql'),
    allowReset: true,
  })).rejects.toThrow('Refusing to reset non-test database');
});

test('database reset rejects an unapproved host before connecting', async () => {
  await expect(resetDatabase({
    databaseUrl: 'postgres://postgres:password@database.example/early_years_parity',
    templateDatabase: 'early_years_foundation_recovery_test',
    fixturePath: path.resolve('fixtures/canonical.sql'),
    allowReset: true,
  })).rejects.toThrow('Refusing database reset on host database.example');
});

test('database reset rejects a non-test template before connecting', async () => {
  await expect(resetDatabase({
    databaseUrl: 'postgres://postgres:password@db:5432/early_years_parity',
    templateDatabase: 'production',
    fixturePath: path.resolve('fixtures/canonical.sql'),
    allowReset: true,
  })).rejects.toThrow('Refusing non-test template database');
});

test('database reset rejects identical target and template names', async () => {
  await expect(resetDatabase({
    databaseUrl: 'postgres://postgres:password@db:5432/early_years_parity',
    templateDatabase: 'early_years_parity',
    fixturePath: path.resolve('fixtures/canonical.sql'),
    allowReset: true,
  })).rejects.toThrow('must be different');
});
