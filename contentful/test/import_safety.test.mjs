import assert from 'node:assert/strict';
import { test } from 'node:test';
import { assertSafeDestination } from '../scripts/import_safety.mjs';

const safeDestination = {
  allowTestSeed: 'true',
  environmentId: 'master',
  sourceSpace: 'source-space',
  spaceId: 'fresh-destination-space',
};

test('allows master in a separate destination space', () => {
  assert.doesNotThrow(() => assertSafeDestination(safeDestination));
});

test('requires explicit destructive-operation confirmation', () => {
  assert.throws(
    () => assertSafeDestination({ ...safeDestination, allowTestSeed: 'false' }),
    /CONTENTFUL_ALLOW_TEST_SEED=true/,
  );
});

for (const environmentId of ['production', 'PRODUCTION', 'staging', 'STAGING']) {
  test(`refuses protected environment ${environmentId}`, () => {
    assert.throws(
      () => assertSafeDestination({ ...safeDestination, environmentId }),
      /Refusing to seed protected environment/,
    );
  });
}

test('refuses the exported HFEYP source space even when targeting master', () => {
  assert.throws(
    () => assertSafeDestination({ ...safeDestination, spaceId: safeDestination.sourceSpace }),
    /Refusing to write to the source HFEYP Contentful space/,
  );
});
