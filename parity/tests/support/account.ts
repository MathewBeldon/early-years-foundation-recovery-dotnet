import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { expectPath, expectProblem } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

export async function visitMyAccount(page: Page, baseUrl: string): Promise<void> {
  await page.goto(`${baseUrl}/my-account`);
}

export async function expectMyAccountPage(page: Page): Promise<void> {
  await expectPath(page, '/my-account');
  await expect(page.getByRole('heading', { name: 'Manage your account' })).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

/**
 * Asserts account summary content for a user registered via registerMinimalUser
 * (England, Department for Education, no training emails, no research).
 */
export async function expectRegisteredAccountDetails(
  page: Page,
  options: { fullName: string },
): Promise<void> {
  await expectMyAccountPage(page);

  await expect(page.getByText(options.fullName)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Change name' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Change password' })).toHaveCount(0);

  await expect(
    page.getByText(
      'This is the name that will appear on your end of module certificate',
    ),
  ).toBeVisible();
  await expect(
    page.getByText(
      'Changing your name on this account will not affect your GOV.UK One Login',
    ),
  ).toBeVisible();

  await expect(page.getByRole('link', { name: 'Change setting details' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Change', exact: true })).toBeVisible();
  await expect(
    page.locator('.govuk-summary-list__value', { hasText: 'England' }),
  ).toBeVisible();
  await expect(
    page.locator('.govuk-summary-list__value', {
      hasText: 'Department for Education',
    }),
  ).toBeVisible();

  await expect(page.getByRole('link', { name: 'Change email preferences' })).toBeVisible();
  await expect(
    page.getByText('You have chosen not to receive emails about this training course.'),
  ).toBeVisible();

  await expect(page.getByRole('link', { name: 'Change research preferences' })).toBeVisible();
  await expect(
    page.getByText('You have chosen not to participate in research.'),
  ).toBeVisible();

  await expect(page.getByRole('heading', { name: 'Closing your account' })).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'ask us to close your account' }),
  ).toHaveAttribute('href', '/my-account/close/edit-reason');
}

export async function openCloseAccountReason(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'ask us to close your account' }).click();
  await expectPath(page, '/my-account/close/edit-reason');
  await expect(
    page.getByRole('group', {
      name: 'Tell us why you want to close your account',
    }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function chooseCloseReasonAndContinue(
  page: Page,
  reason: string,
  customReason?: string,
): Promise<void> {
  await page.getByLabel(reason, { exact: true }).check();
  if (customReason !== undefined) {
    await page
      .getByLabel('Tell us why you want to close your account.')
      .fill(customReason);
  }
  await page.getByRole('button', { name: 'Continue' }).click();
}

export async function submitCloseReasonWithoutSelection(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Continue' }).click();
}

export async function expectCloseReasonValidation(page: Page): Promise<void> {
  await expectPath(page, '/my-account/close/edit-reason');
  await expectProblem(page, 'Select a reason for closing your account');
}

export async function expectCloseAccountConfirmPage(page: Page): Promise<void> {
  await expectPath(page, '/my-account/close/confirm');
  await expect(
    page.getByRole('heading', { name: 'Confirm you want to close your account' }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(
    page.getByRole('link', { name: 'Cancel and go back to my account' }),
  ).toHaveAttribute('href', '/my-account');
}

export async function confirmCloseAccount(page: Page): Promise<void> {
  await expectCloseAccountConfirmPage(page);
  await page.getByRole('button', { name: 'Close my account' }).click();
}

export async function expectAccountClosed(page: Page): Promise<void> {
  await expectPath(page, '/my-account/close');
  await expect(page.getByRole('heading', { name: 'Account closed' })).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(page.getByText('You have been logged out.')).toBeVisible();
}

export async function expectSignedOutFromMyAccount(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await page.goto(`${baseUrl}/my-account`);
  await expect(page).toHaveURL(/\/users\/sign-in/, { timeout: STEP_TIMEOUT_MS });
  await expect(
    page.getByRole('heading', { name: 'How to access this training course' }),
  ).toBeVisible();
}
