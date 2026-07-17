import path from 'node:path';
import { resetDatabase } from './database.js';
import { test } from '../support/fixtures.js';

test('restore the canonical parity database', async () => {
  test.skip(
    process.env.PARITY_ALLOW_DATABASE_RESET !== 'true',
    'Database reset is an explicit opt-in command, not part of the ordinary parity suite.',
  );

  await resetDatabase({
    databaseUrl: process.env.PARITY_DATABASE_URL ?? '',
    templateDatabase: process.env.PARITY_TEMPLATE_DATABASE ?? '',
    fixturePath: path.resolve('fixtures/canonical.sql'),
    allowReset: process.env.PARITY_ALLOW_DATABASE_RESET === 'true',
  });
});
