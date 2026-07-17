import { captureSideEffects, resetSideEffects } from './client.js';
import { test, expect } from '../support/fixtures.js';

test('external side effects can be reset and captured canonically', async ({ request, urls }) => {
  await resetSideEffects(request, urls.sideEffectSink);
  const accepted = await request.post(`${urls.sideEffectSink}/capture/notify`, {
    data: { template: 'synthetic-template', recipient: 'test@example.invalid' },
    headers: { 'X-Correlation-Id': 'parity-1' },
  });

  expect(accepted.status()).toBe(202);
  const captures = await captureSideEffects(request, urls.sideEffectSink);
  expect(captures).toHaveLength(1);
  expect(captures[0]).toMatchObject({
    sequence: 1,
    method: 'POST',
    path: '/capture/notify',
    headers: {
      'content-type': 'application/json',
      'x-correlation-id': 'parity-1',
    },
    body: JSON.stringify({
      template: 'synthetic-template',
      recipient: 'test@example.invalid',
    }),
  });
});
