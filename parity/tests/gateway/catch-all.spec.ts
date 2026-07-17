import { captureHttp } from '../http/capture.js';
import { test, expect } from '../support/fixtures.js';

function redirectPath(value: string | null, baseUrl: string): string | null {
  return value === null ? null : new URL(value, baseUrl).pathname;
}

test('unmigrated gateway paths are served by Rails catch-all', async ({ request, urls }) => {
  const paths = [
    '/health',
    '/about-training',
    '/my-modules',
    '/migration-catch-all-unknown',
  ];

  for (const path of paths) {
    const rails = await captureHttp(request, `${urls.rails}${path}`);
    const gateway = await captureHttp(request, `${urls.gateway}${path}`);

    expect(gateway.status, path).toBe(rails.status);
    expect(gateway.html, path).toBe(rails.html);
    expect(redirectPath(gateway.redirect, urls.gateway), path).toBe(
      redirectPath(rails.redirect, urls.rails),
    );
    expect(gateway.headers['content-type'], path).toEqual(
      rails.headers['content-type'],
    );
  }
});
