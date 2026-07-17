import { maskDynamicValues } from '../normalisation/dynamic-values.js';
import { test, expect } from '../support/fixtures.js';

test('normalisation retains potentially meaningful UUIDs and timestamps', () => {
  const value = 'user 123e4567-e89b-42d3-a456-426614174000 at 2026-07-15T12:34:56Z';

  expect(maskDynamicValues(value)).toBe(value);
});

test('normalisation masks only justified dynamic values', () => {
  const value = [
    '<script nonce="unique-value"></script>',
    '<meta name="csp-nonce" content="meta-nonce">',
    '<meta name="csrf-token" content="session-token">',
    '<input name="authenticity_token" value="form-token">',
  ].join('');

  expect(maskDynamicValues(value)).toBe(
    '<script nonce="<nonce>"></script>' +
      '<meta name="csp-nonce" content="<nonce>">' +
      '<meta name="csrf-token" content="<csrf-token>">' +
      '<input name="authenticity_token" value="<csrf-token>">',
  );
  expect(() =>
    maskDynamicValues(value, [
      {
        pattern: /unique-value/g,
        replacement: '<value>',
        justification: '   ',
      },
    ]),
  ).toThrow('has no justification');
});
