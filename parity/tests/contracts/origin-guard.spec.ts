import { test, expect } from '../support/fixtures.js';
import { assertAllowedMainFrameUrl } from '../support/origin-guard.js';

test('journey origin guard accepts the target and One Login origins', () => {
  const allowed = new Set([
    'http://gateway.recovery.internal:8080',
    'http://gov-one-simulator:4000',
  ]);

  expect(() =>
    assertAllowedMainFrameUrl('http://gateway.recovery.internal:8080/my-modules', allowed),
  ).not.toThrow();
  expect(() =>
    assertAllowedMainFrameUrl('http://gov-one-simulator:4000/authorize', allowed),
  ).not.toThrow();
});

test('journey origin guard rejects a gateway escape directly to Rails', () => {
  const allowed = new Set([
    'http://gateway.recovery.internal:8080',
    'http://gov-one-simulator:4000',
  ]);

  expect(() =>
    assertAllowedMainFrameUrl('http://rails.recovery.internal:3000/my-modules', allowed),
  ).toThrow('Journey escaped its approved origins');
});
