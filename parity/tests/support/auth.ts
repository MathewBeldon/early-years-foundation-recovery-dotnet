import { randomUUID } from 'node:crypto';
import type { Page } from '@playwright/test';
import { targetUrls } from './environment.js';

export interface SimulatorUser {
  email: string;
  sub: string;
}

export function newSimulatorUser(label = 'registration'): SimulatorUser {
  const id = randomUUID();
  return {
    email: `${label}.${id}@example.com`,
    sub: `urn:fdc:gov.uk:one-login:simulator:${id}`,
  };
}

/**
 * Drive browser sign-in through the application under test and the simulator.
 * Interactive mode binds the submitted identity to this authorize flow and token.
 * Lands on the first incomplete-registration step for a new user.
 */
export async function signInViaOneLogin(
  page: Page,
  user: SimulatorUser,
  baseUrl = targetUrls().target,
): Promise<void> {
  const host = new URL(baseUrl).hostname;
  await page.context().addCookies([
    {
      name: 'track_analytics_v2',
      value: 'true',
      domain: host,
      path: '/',
      httpOnly: true,
    },
  ]);

  await page.goto(`${baseUrl}/users/sign-in`);
  await page.getByRole('heading', { name: 'How to access this training course' }).waitFor();
  await page.getByRole('link', { name: 'Continue to GOV.UK One Login' }).click();

  const subject = page.getByTestId('sub');
  await subject.waitFor({ state: 'visible' });
  await subject.fill(user.sub);
  await page.getByTestId('email').fill(user.email);
  await page.getByTestId('email-verified').check();
  await page.getByRole('button', { name: 'Continue', exact: true }).click();

  await page.waitForURL(
    (url) =>
      url.pathname.includes('/registration/terms-and-conditions') ||
      url.pathname.includes('/my-modules') ||
      url.pathname.includes('/whats-new') ||
      url.pathname.includes('/email-preferences'),
    { timeout: 120_000 },
  );
}
