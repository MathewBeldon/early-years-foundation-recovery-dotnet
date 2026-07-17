import { test, expect } from '../support/fixtures.js';

test('Rails reference is reachable', async ({ request, urls }) => {
  const response = await request.get(`${urls.rails}/health`);
  expect(response.status()).toBe(200);
});

test('.NET candidate is reachable', async ({ request, urls }) => {
  const response = await request.get(`${urls.dotnet}/health`);
  expect(response.status()).toBe(200);
});

test('YARP gateway is reachable', async ({ request, urls }) => {
  const response = await request.get(`${urls.gateway}/gateway-health`);
  expect(response.status()).toBe(200);
});
