import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

export async function visitSignIn(page: Page, baseUrl: string): Promise<void> {
  await page.goto(`${baseUrl}/users/sign-in`);
}

export async function expectSignInPage(page: Page): Promise<void> {
  await expect(
    page.getByRole('heading', { name: 'How to access this training course' }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

/** Mirrors spec/system/gov_one_spec.rb unauthenticated content assertions. */
export async function expectSignInExplainerContent(page: Page): Promise<void> {
  await expectSignInPage(page);

  await expect(
    page.getByText(
      'This service uses GOV.UK One Login which is managed by the Government Digital Service.',
    ),
  ).toBeVisible();

  await expect(
    page.getByText(
      'You will be asked to sign in to your account, or create a One Login account, in this service',
    ),
  ).toBeVisible();

  await expectContinueToOneLoginLink(page);

  await page.getByText('How to access an existing training account').click();
  await expect(
    page.getByText(
      'If you have an existing early years child development training account but you do not yet have a GOV.UK One Login, you must use the same email address for both accounts. This will ensure that any progress you have made through the training is retained.',
    ),
  ).toBeVisible();
}

export async function expectContinueToOneLoginLink(page: Page): Promise<void> {
  await expect(
    page.getByRole('link', { name: 'Continue to GOV.UK One Login' }),
  ).toBeVisible();
}

/** Mirrors spec/system/gov_one_spec.rb authenticated redirect. */
export async function expectAlreadySignedInRedirect(page: Page): Promise<void> {
  await expectPath(page, '/');
  await expect(page.getByText('You are already signed in.')).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function signOutFromNavigation(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Sign out' }).click();
  await page.waitForURL(
    (url) =>
      url.pathname === '/' ||
      url.pathname === '/users/sign_out' ||
      url.pathname.includes('/users/sign-in'),
    { timeout: 120_000 },
  );
}

export async function expectSignedOutState(page: Page, baseUrl: string): Promise<void> {
  await visitSignIn(page, baseUrl);
  await expectSignInPage(page);
  await expectContinueToOneLoginLink(page);

  await page.goto(`${baseUrl}/my-modules`);
  await expect(page).toHaveURL(/\/users\/sign-in/, { timeout: STEP_TIMEOUT_MS });
  await expectSignInPage(page);
}
