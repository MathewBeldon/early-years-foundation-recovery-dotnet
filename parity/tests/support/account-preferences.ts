import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import {
  chooseCustomSettingAndContinue,
  chooseWhereYouLiveAndContinue,
  continueRegistration,
  expectPath,
  expectProblem,
} from './registration.js';
import {
  expectMyAccountPage,
  visitMyAccount,
} from './account.js';

const STEP_TIMEOUT_MS = 30_000;

function pathPattern(path: string): RegExp {
  const escaped = path.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return new RegExp(`${escaped}(?:\\?.*)?$`);
}

async function openAccountEditPath(
  page: Page,
  selector: string,
  path: string,
): Promise<void> {
  const link = page.locator(selector);
  const expected = pathPattern(path);
  await expect(link).toHaveAttribute('href', expected, { timeout: STEP_TIMEOUT_MS });
  await Promise.all([
    page.waitForURL(expected, { timeout: STEP_TIMEOUT_MS }),
    link.click(),
  ]);
  await expectPath(page, path);
}

export async function openChangeNameFromAccount(page: Page): Promise<void> {
  await openAccountEditPath(
    page,
    '#edit_name_registration',
    '/registration/name/edit',
  );
}

export async function fillNameFields(
  page: Page,
  firstName: string,
  surname: string,
): Promise<void> {
  await page.getByLabel('First name').fill(firstName);
  await page.getByLabel('Surname').fill(surname);
}

export async function saveNameFromAccount(page: Page): Promise<void> {
  const updateCompleted = page.waitForResponse(
    (response) =>
      response.request().method() !== 'GET' &&
      new URL(response.url()).pathname === '/registration/name',
    { timeout: STEP_TIMEOUT_MS },
  );
  await page.getByRole('button', { name: 'Save' }).click();
  await updateCompleted;
}

export async function expectNameValidationErrors(page: Page): Promise<void> {
  await expectPath(page, '/registration/name/edit');
  await expectProblem(page, 'Enter a first name.');
  await expectProblem(page, 'Enter a surname.');
}

export async function openChangeWhereYouLiveFromAccount(page: Page): Promise<void> {
  await openAccountEditPath(
    page,
    '#edit_where_you_live_registration',
    '/registration/where-you-live/edit',
  );
}

export async function openChangeSettingDetailsFromAccount(page: Page): Promise<void> {
  await openAccountEditPath(
    page,
    '#edit_setting_type_registration',
    '/registration/setting-type/edit',
  );
}

export async function changeToCustomSettingFromAccount(
  page: Page,
  customSetting: string,
): Promise<void> {
  await openChangeSettingDetailsFromAccount(page);
  await chooseCustomSettingAndContinue(page, customSetting);
  await expectPath(page, '/my-account');
}

export async function expectScotlandAccountLocation(page: Page): Promise<void> {
  await expectMyAccountPage(page);
  await expect(
    page.locator('.govuk-summary-list__value', { hasText: 'Scotland' }),
  ).toBeVisible();
  await expect(
    page.locator('.govuk-summary-list__value', { hasText: 'Not applicable' }).first(),
  ).toBeVisible();
  await expect(page.getByText('Multiple')).toHaveCount(0);
}

export async function openChangeEmailPreferencesFromAccount(page: Page): Promise<void> {
  await openAccountEditPath(
    page,
    '#edit_training_emails_user',
    '/registration/training-emails/edit',
  );
}

export async function chooseTrainingEmailsAndSave(
  page: Page,
  choice: 'yes' | 'no',
): Promise<void> {
  const label =
    choice === 'yes'
      ? 'Send me email updates about this training course'
      : 'Do not send me email updates about this training course';
  await page.getByRole('radio', { name: label, exact: true }).check();
  await Promise.all([
    page.waitForURL(pathPattern('/my-account'), { timeout: STEP_TIMEOUT_MS }),
    page.getByRole('button', { name: 'Save' }).click(),
  ]);
  await expectPath(page, '/my-account');
}

export async function expectEmailPreferenceOnAccount(
  page: Page,
  choice: 'yes' | 'no',
): Promise<void> {
  const copy =
    choice === 'yes'
      ? 'You have chosen to receive emails about this training course.'
      : 'You have chosen not to receive emails about this training course.';
  await expectMyAccountPage(page);
  await expect(page.getByText(copy)).toBeVisible();
}

export async function openChangeResearchPreferencesFromAccount(
  page: Page,
): Promise<void> {
  await openAccountEditPath(
    page,
    '#edit_research_participant_user',
    '/registration/research-participant/edit',
  );
}

export async function chooseResearchParticipantAndSave(
  page: Page,
  choice: 'Yes' | 'No',
): Promise<void> {
  await page.getByLabel(choice, { exact: true }).check();
  await Promise.all([
    page.waitForURL(pathPattern('/my-account'), { timeout: STEP_TIMEOUT_MS }),
    page.getByRole('button', { name: 'Save' }).click(),
  ]);
  await expectPath(page, '/my-account');
}

export async function expectResearchPreferenceOnAccount(
  page: Page,
  choice: 'Yes' | 'No',
): Promise<void> {
  const copy =
    choice === 'Yes'
      ? 'You have chosen to participate in research.'
      : 'You have chosen not to participate in research.';
  await expectMyAccountPage(page);
  await expect(page.getByText(copy)).toBeVisible();
}

export async function expectDetailsUpdatedFlash(page: Page): Promise<void> {
  await expect(page.getByText('You have updated your details')).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function visitAccountAfterWhereYouLiveChange(
  page: Page,
  baseUrl: string,
  location: string,
): Promise<void> {
  await openChangeWhereYouLiveFromAccount(page);
  await chooseWhereYouLiveAndContinue(page, location);
  await expectPath(page, '/registration/setting-type/edit');
  await visitMyAccount(page, baseUrl);
}

export {
  visitMyAccount,
  expectMyAccountPage,
  chooseWhereYouLiveAndContinue,
  continueRegistration,
};
