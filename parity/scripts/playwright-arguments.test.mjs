import assert from 'node:assert/strict';
import test from 'node:test';
import {
  decodeArguments,
  defaultJourneyTargets,
} from './playwright-arguments.mjs';

function encode(...args) {
  return Buffer.from(`${args.join('\0')}\0`, 'utf8').toString('base64');
}

test('uses journey and concurrency targets when no arguments are supplied', () => {
  assert.deepEqual(decodeArguments(undefined), defaultJourneyTargets);
});

test('applies option-only arguments to the default journey targets', () => {
  assert.deepEqual(decodeArguments(encode('--workers=6')), [
    ...defaultJourneyTargets,
    '--workers=6',
  ]);
  assert.deepEqual(decodeArguments(encode('-g', 'clears local authority')), [
    ...defaultJourneyTargets,
    '-g',
    'clears local authority',
  ]);
});

test('allows an explicit test path to replace the default targets', () => {
  assert.deepEqual(
    decodeArguments(encode('tests/journeys/registration.spec.ts', '--workers=6')),
    ['tests/journeys/registration.spec.ts', '--workers=6'],
  );
});

test('rejects malformed encoded arguments', () => {
  assert.throws(() => decodeArguments('not base64'), /not valid base64/);
});
