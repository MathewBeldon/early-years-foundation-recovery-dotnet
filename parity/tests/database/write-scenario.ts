import assert from 'node:assert/strict';
import {
  captureDatabaseState,
  compareDatabaseState,
  resetDatabase,
  validateCapturePlan,
  withDatabaseComparisonLock,
} from './database.js';
import type { CapturePlan, DatabaseResetOptions } from './database.js';

interface WriteScenario<T> {
  reset: DatabaseResetOptions;
  capturePlan: CapturePlan;
  verifyTarget: (
    candidate: 'rails' | 'dotnet',
    expectedDatabaseUrl: string,
  ) => Promise<void>;
  runRails: (databaseUrl: string) => Promise<T>;
  runDotNet: (databaseUrl: string) => Promise<T>;
}

export async function runSequentialWriteScenario<T>(scenario: WriteScenario<T>): Promise<void> {
  validateCapturePlan(scenario.capturePlan, { requireApplicationTable: true });
  await withDatabaseComparisonLock(scenario.reset.databaseUrl, async () => {
    await resetDatabase(scenario.reset);
    await scenario.verifyTarget('rails', scenario.reset.databaseUrl);
    const railsResult = await scenario.runRails(scenario.reset.databaseUrl);
    const railsState = await captureDatabaseState(
      scenario.reset.databaseUrl,
      scenario.capturePlan,
    );

    await resetDatabase(scenario.reset);
    await scenario.verifyTarget('dotnet', scenario.reset.databaseUrl);
    const dotnetResult = await scenario.runDotNet(scenario.reset.databaseUrl);
    const dotnetState = await captureDatabaseState(
      scenario.reset.databaseUrl,
      scenario.capturePlan,
    );

    assert.deepStrictEqual(dotnetResult, railsResult);
    compareDatabaseState(railsState, dotnetState);
  });
}
