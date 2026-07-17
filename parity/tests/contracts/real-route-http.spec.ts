import type { Browser, Page } from '@playwright/test';
import { captureBrowserResponse, captureHttp } from '../http/capture.js';
import type { HttpCapture } from '../http/capture.js';
import { newSimulatorUser, signInViaOneLogin } from '../support/auth.js';
import { expect, test } from '../support/fixtures.js';
import {
  acceptTermsAndContinue,
  fillNameAndContinue,
} from '../support/registration.js';

function redirectPath(capture: HttpCapture): string | null {
  return capture.redirect === null ? null : new URL(capture.redirect, capture.url).pathname;
}

async function captureSettingsPost(page: Page, baseUrl: string): Promise<HttpCapture> {
  await page.goto(`${baseUrl}/settings/cookie-policy`);
  await page.getByRole('radio', { name: 'Yes', exact: true }).check();
  const responsePromise = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname === '/settings',
  );
  await page.getByRole('button', { name: 'Save cookie settings' }).click();
  return captureBrowserResponse(await responsePromise);
}

async function captureRegistrationPatch(
  page: Page,
  baseUrl: string,
  label: string,
): Promise<HttpCapture> {
  await signInViaOneLogin(page, newSimulatorUser(label), baseUrl);
  await acceptTermsAndContinue(page);
  expect(await page.locator('form input[name="_method"]').inputValue()).toBe('patch');
  const responsePromise = page.waitForResponse(
    (response) =>
      ['POST', 'PATCH'].includes(response.request().method()) &&
      new URL(response.url()).pathname === '/registration/name',
  );
  await fillNameAndContinue(page, 'Contract', 'Capture');
  return captureBrowserResponse(await responsePromise, 'PATCH');
}

async function withPage<T>(
  browser: Browser,
  operation: (page: Page) => Promise<T>,
): Promise<T> {
  const context = await browser.newContext({ serviceWorkers: 'block' });
  try {
    return await operation(await context.newPage());
  } finally {
    await context.close();
  }
}

test('captures critical real-route contracts directly and through the gateway', async ({
  browser,
  request,
  urls,
}, testInfo) => {
  test.setTimeout(240_000);
  const origins = [
    { name: 'rails-direct', baseUrl: urls.rails },
    { name: 'gateway', baseUrl: urls.gateway },
  ] as const;
  const captures: Record<string, Record<string, HttpCapture>> = {};

  for (const origin of origins) {
    const myModules = await captureHttp(request, `${origin.baseUrl}/my-modules`);
    const unknown = await captureHttp(
      request,
      `${origin.baseUrl}/migration-contract-unknown-path`,
    );
    const settings = await withPage(browser, (page) =>
      captureSettingsPost(page, origin.baseUrl),
    );
    const registration = await withPage(browser, (page) =>
      captureRegistrationPatch(page, origin.baseUrl, `contract-${origin.name}`),
    );

    expect(myModules.status).toBe(302);
    expect(redirectPath(myModules)).toBe('/users/sign-in');

    expect(unknown.status).toBe(404);
    expect(unknown.redirect).toBeNull();

    expect(registration.status).toBe(302);
    expect(redirectPath(registration)).toBe('/registration/where-you-live/edit');

    expect(settings.status).toBe(302);
    expect(redirectPath(settings)).toBe('/settings/cookie-policy');
    const analyticsCookie = settings.cookies.find((cookie) =>
      cookie.startsWith('track_analytics_v2=true;'),
    );
    expect(analyticsCookie).toBeDefined();
    expect(analyticsCookie).toMatch(/path=\//i);
    expect(analyticsCookie).toMatch(/expires=/i);
    expect(analyticsCookie).toMatch(/httponly/i);
    expect(analyticsCookie).toMatch(/samesite=lax/i);
    expect(analyticsCookie).not.toMatch(/;\s*secure/i);

    captures[origin.name] = { myModules, registration, unknown, settings };
  }

  for (const route of ['myModules', 'registration', 'unknown', 'settings']) {
    expect(captures.gateway?.[route]?.status).toBe(
      captures['rails-direct']?.[route]?.status,
    );
    expect(redirectPath(captures.gateway?.[route] as HttpCapture)).toBe(
      redirectPath(captures['rails-direct']?.[route] as HttpCapture),
    );
  }

  await testInfo.attach('real-route-http-contracts', {
    body: Buffer.from(JSON.stringify(captures, null, 2)),
    contentType: 'application/json',
  });
});
