import fs from 'node:fs';
import { validateCapturePlan } from './database.js';
import type { CapturePlan } from './database.js';
import { test, expect } from '../support/fixtures.js';

test('source-controlled write capture plan includes application state', () => {
  const plan = JSON.parse(
    fs.readFileSync('fixtures/database-capture-plan.json', 'utf8'),
  ) as CapturePlan;

  expect(() =>
    validateCapturePlan(plan, { requireApplicationTable: true }),
  ).not.toThrow();

  const tables = plan.tables.map((entry) => entry.table);
  expect(tables).toEqual(
    expect.arrayContaining([
      'users',
      'events',
      'mail_events',
      'assessments',
      'confidence_check_progress',
      'notes',
      'responses',
      'user_module_progress',
    ]),
  );
});

test('write capture plans require application state', () => {
  expect(() =>
    validateCapturePlan(
      {
        tables: [
          {
            table: 'schema_migrations',
            columns: ['version'],
            orderBy: ['version'],
            volatileFields: [],
          },
        ],
      },
      { requireApplicationTable: true },
    ),
  ).toThrow('must include at least one application table');
});

test('capture plans require deterministic ordering', () => {
  expect(() =>
    validateCapturePlan({
      tables: [
        {
          table: 'users',
          columns: ['id'],
          orderBy: [],
          volatileFields: [],
        },
      ],
    }),
  ).toThrow('has no deterministic orderBy');
});

test('capture plans reject volatile fields that are not captured', () => {
  expect(() =>
    validateCapturePlan({
      tables: [
        {
          table: 'users',
          columns: ['id'],
          orderBy: ['id'],
          volatileFields: ['updated_at'],
        },
      ],
    }),
  ).toThrow('is not present in captured columns');
});
