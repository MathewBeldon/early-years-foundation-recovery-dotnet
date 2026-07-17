import { captureHttp } from '../http/capture.js';
import { test, expect } from '../support/fixtures.js';

test('HTTP recorder captures a real Rails health contract', async ({ request, urls }, testInfo) => {
  const health = await captureHttp(request, `${urls.rails}/health`);

  expect(health.status).toBe(200);
  expect(health.redirect).toBeNull();
  expect(health.headers['content-type']).toEqual(['application/json; charset=utf-8']);
  expect(health.headers['x-content-type-options']).toEqual(['nosniff']);
  expect(health.headers['x-frame-options']).toEqual(['SAMEORIGIN']);
  expect(health.headers['referrer-policy']).toEqual(['strict-origin-when-cross-origin']);
  expect(JSON.parse(health.html)).toEqual({ status: 'HEALTHY' });

  await testInfo.attach('rails-health-http-contract', {
    body: Buffer.from(JSON.stringify(health, null, 2)),
    contentType: 'application/json',
  });
});
