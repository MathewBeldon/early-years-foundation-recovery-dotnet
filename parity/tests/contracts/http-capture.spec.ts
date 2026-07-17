import { captureHttp } from '../http/capture.js';
import { test, expect } from '../support/fixtures.js';

test('HTTP recorder captures redirect, selected headers, cookies and HTML', async ({ request, urls }, testInfo) => {
  const redirect = await captureHttp(request, `${urls.sideEffectSink}/contract-fixture`);
  const target = await captureHttp(request, `${urls.sideEffectSink}/contract-target`);
  const post = await captureHttp(request, `${urls.sideEffectSink}/contract-echo`, {
    method: 'POST',
    data: { value: 'canonical request' },
  });

  expect(redirect.method).toBe('GET');
  expect(redirect.status).toBe(302);
  expect(redirect.redirect).toBe('/contract-target');
  expect(redirect.cookies).toContain(
    'parity_cookie=reference; Path=/; HttpOnly; SameSite=Lax',
  );
  expect(target.status).toBe(200);
  expect(target.headers['x-content-type-options']).toEqual(['nosniff']);
  expect(target.headers['content-security-policy']).toEqual(["default-src 'self'"]);
  expect(target.headers['permissions-policy']).toEqual(['camera=(), microphone=()']);
  expect(target.headers['referrer-policy']).toEqual(['no-referrer']);
  expect(target.html).toContain('<main><h1>Parity fixture</h1><p>Deterministic HTML</p></main>');
  expect(post.method).toBe('POST');
  expect(post.status).toBe(201);
  expect(JSON.parse(post.html)).toEqual({
    method: 'POST',
    body: JSON.stringify({ value: 'canonical request' }),
  });

  const capture = JSON.stringify({ redirect, target, post }, null, 2);
  await testInfo.attach('http-capture', {
    body: Buffer.from(capture),
    contentType: 'application/json',
  });
});
