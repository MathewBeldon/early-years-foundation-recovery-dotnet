import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import {
  newSimulatorUser,
  signInViaOneLogin,
  type SimulatorUser,
} from './auth.js';

/**
 * Contentful-backed registration views often take 6–10s in this stack.
 * Keep navigation/assertion waits above that floor.
 */
const STEP_TIMEOUT_MS = 30_000;

/** UI helpers for the registration wizard. Prefer accessible names over Rails form names. */

export async function expectPath(page: Page, path: string): Promise<void> {
  // Allow optional query strings (e.g. ?return_to=check_your_answers from CYA Change links).
  const escaped = path.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  await expect(page).toHaveURL(new RegExp(`${escaped}(?:\\?.*)?$`), {
    timeout: STEP_TIMEOUT_MS,
  });
}

async function submitButtonAndWaitForResponse(
  page: Page,
  name: string,
): Promise<void> {
  const button = page.getByRole('button', { name, exact: true });
  await expect(button).toBeEnabled({ timeout: STEP_TIMEOUT_MS });
  const actionPath = await button.evaluate((element) => {
    const form = element.closest('form');
    if (!form) throw new Error(`Submit button ${element.textContent} has no form.`);
    return new URL(form.action, document.baseURI).pathname;
  });
  const submitted = page.waitForResponse(
    (response) =>
      response.request().method() !== 'GET' &&
      new URL(response.url()).pathname === actionPath,
    { timeout: STEP_TIMEOUT_MS },
  );

  await button.click();
  const response = await submitted;
  if (response.status() >= 500) {
    throw new Error(`Form submission to ${actionPath} returned HTTP ${response.status()}.`);
  }
}

export async function continueRegistration(page: Page): Promise<void> {
  await submitButtonAndWaitForResponse(page, 'Continue');
}

export async function expectProblem(page: Page, message: string): Promise<void> {
  const summary = page.locator('.govuk-error-summary');
  await expect(summary).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(summary.getByText('There is a problem')).toBeVisible();
  // Prefer the summary link — the same copy also appears in the field error.
  await expect(summary.getByRole('link', { name: message })).toBeVisible();
}

/**
 * Sign in via One Login and complete terms + name so branch tests start at where-you-live.
 */
export async function startFreshRegistration(
  page: Page,
  baseUrl: string,
  options: {
    label: string;
    firstName: string;
    surname: string;
    identity?: SimulatorUser;
  },
): Promise<SimulatorUser> {
  const user = options.identity ?? newSimulatorUser(options.label);
  await signInViaOneLogin(page, user, baseUrl);
  await acceptTermsAndContinue(page);
  await fillNameAndContinue(page, options.firstName, options.surname);
  return user;
}

export async function acceptTermsAndContinue(page: Page): Promise<void> {
  await expectPath(page, '/registration/terms-and-conditions/edit');
  await page
    .getByLabel('I confirm that I accept the terms and conditions and privacy policy.')
    .check();
  await continueRegistration(page);
}

export async function fillNameAndContinue(
  page: Page,
  firstName: string,
  surname: string,
): Promise<void> {
  await expectPath(page, '/registration/name/edit');
  await expect(page.getByText('About you')).toBeVisible();
  await page.getByLabel('First name').fill(firstName);
  await page.getByLabel('Surname').fill(surname);
  await continueRegistration(page);
}

export async function chooseWhereYouLiveAndContinue(
  page: Page,
  location: string,
): Promise<void> {
  await expectPath(page, '/registration/where-you-live/edit');
  await page.getByLabel(location, { exact: true }).check();
  await continueRegistration(page);
}

export async function chooseCustomSettingAndContinue(
  page: Page,
  setting: string,
): Promise<void> {
  await expectPath(page, '/registration/setting-type/edit');
  await page.getByRole('link', { name: 'I cannot find my setting or organisation' }).click();
  await expectPath(page, '/registration/setting-type-other/edit');
  await page
    .getByLabel('Enter the type of setting or organisation where you work.')
    .fill(setting);
  await continueRegistration(page);
}

async function chooseAutocompleteOption(
  page: Page,
  fieldLabel: string,
  option: string,
): Promise<void> {
  const input = page.getByLabel(fieldLabel, { exact: true });
  await input.click();
  await input.fill(option);
  await page.getByRole('option', { name: option, exact: true }).click();
  await expect(input).toHaveValue(option, { timeout: STEP_TIMEOUT_MS });
  await expect(page.locator('main select option:checked').first()).toHaveText(option, {
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function chooseSettingByLabelAndContinue(
  page: Page,
  settingLabel: string,
): Promise<void> {
  await expectPath(page, '/registration/setting-type/edit');
  await chooseAutocompleteOption(page, 'Setting type', settingLabel);
  await continueRegistration(page);
}

export async function chooseLocalAuthorityAndContinue(
  page: Page,
  authority: string,
): Promise<void> {
  await expectPath(page, '/registration/local-authority/edit');
  await chooseAutocompleteOption(page, 'Local authority', authority);
  await continueRegistration(page);
}

export async function chooseRoleAndContinue(page: Page, role: string): Promise<void> {
  await expectPath(page, '/registration/role-type/edit');
  await page.getByLabel(role, { exact: true }).check();
  await continueRegistration(page);
}

export async function expectRoleRadios(
  page: Page,
  present: string[],
  absent: string[] = [],
): Promise<void> {
  await expectPath(page, '/registration/role-type/edit');
  for (const role of present) {
    await expect(page.getByRole('radio', { name: role, exact: true })).toBeVisible();
  }
  for (const role of absent) {
    await expect(page.getByRole('radio', { name: role, exact: true })).toHaveCount(0);
  }
}

export async function chooseCustomRoleAndContinue(
  page: Page,
  role: string,
): Promise<void> {
  await expectPath(page, '/registration/role-type/edit');
  await page.getByRole('link', { name: 'I would describe my role in another way.' }).click();
  await expectPath(page, '/registration/role-type-other/edit');
  await page.getByLabel('Enter your job title.').fill(role);
  await continueRegistration(page);
}

export async function chooseExperienceAndContinue(
  page: Page,
  experience: string,
): Promise<void> {
  await expectPath(page, '/registration/early-years-experience/edit');
  await page.getByLabel(experience, { exact: true }).check();
  await continueRegistration(page);
}

export async function chooseTrainingEmailsAndContinue(
  page: Page,
  choice: 'yes' | 'no',
): Promise<void> {
  await expectPath(page, '/registration/training-emails/edit');
  // Exact match required: "Do not send me…" contains "send me…" as a substring.
  const label =
    choice === 'yes'
      ? 'Send me email updates about this training course'
      : 'Do not send me email updates about this training course';
  await page.getByRole('radio', { name: label, exact: true }).check();
  await continueRegistration(page);
}

export async function chooseResearchParticipantAndContinue(
  page: Page,
  choice: 'Yes' | 'No',
): Promise<void> {
  await expectPath(page, '/registration/research-participant/edit');
  await page.getByLabel(choice, { exact: true }).check();
  await continueRegistration(page);
}

export async function completePreferences(
  page: Page,
  options: { emails: 'yes' | 'no'; research: 'Yes' | 'No' },
): Promise<void> {
  await chooseTrainingEmailsAndContinue(page, options.emails);
  await chooseResearchParticipantAndContinue(page, options.research);
}

export async function expectSummaryRow(
  page: Page,
  key: string,
  value: string,
): Promise<void> {
  await expectCheckYourAnswers(page);
  const row = page.locator('.govuk-summary-list__row').filter({
    has: page.locator('.govuk-summary-list__key', { hasText: key }),
  });
  await expect(row.locator('.govuk-summary-list__value')).toContainText(value, {
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function expectSummaryRowAbsent(page: Page, key: string): Promise<void> {
  await expectCheckYourAnswers(page);
  await expect(
    page.locator('.govuk-summary-list__key', { hasText: new RegExp(`^${key}$`) }),
  ).toHaveCount(0, { timeout: STEP_TIMEOUT_MS });
}

export async function expectCheckYourAnswers(page: Page): Promise<void> {
  await expectPath(page, '/registration/check-your-answers/edit');
  await expect(page.getByRole('heading', { name: 'Check your answers' })).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function confirmCheckYourAnswers(page: Page): Promise<void> {
  await expectCheckYourAnswers(page);
  await submitButtonAndWaitForResponse(page, 'Confirm and create account');
  await expectPath(page, '/my-modules');
  await expect(page.getByText('You can now start your first module.')).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

/** GOV.UK summary-list Change link (visually hidden context, e.g. "Change name"). */
export async function changeCheckYourAnswer(
  page: Page,
  field: string,
): Promise<void> {
  await expectCheckYourAnswers(page);
  await page.getByRole('link', { name: `Change ${field}`, exact: true }).click();
}

export async function clickRegistrationBack(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Back', exact: true }).click();
}

/**
 * Reach Check your answers via England + LA nursery + Student (full detail path).
 * Does not confirm the account.
 */
export async function reachCheckYourAnswersEnglandNursery(
  page: Page,
  baseUrl: string,
  options: { label: string; firstName?: string; surname?: string } = {
    label: 'cya-england',
  },
): Promise<void> {
  await startFreshRegistration(page, baseUrl, {
    label: options.label,
    firstName: options.firstName ?? 'Jamie',
    surname: options.surname ?? 'Review',
  });
  await chooseWhereYouLiveAndContinue(page, 'England');
  await chooseSettingByLabelAndContinue(
    page,
    'Local authority maintained nursery school',
  );
  await chooseLocalAuthorityAndContinue(page, 'Leeds');
  await chooseRoleAndContinue(page, 'Student');
  await chooseExperienceAndContinue(page, 'Less than 2 years');
  await completePreferences(page, { emails: 'yes', research: 'No' });
  await expectCheckYourAnswers(page);
}

/**
 * Reach Check your answers via Outside the UK + DfE (no LA / role / experience).
 */
export async function reachCheckYourAnswersOutsideUkDfE(
  page: Page,
  baseUrl: string,
  options: { label: string } = { label: 'cya-dfe' },
): Promise<void> {
  await startFreshRegistration(page, baseUrl, {
    label: options.label,
    firstName: 'Drew',
    surname: 'Skip',
  });
  await chooseWhereYouLiveAndContinue(page, 'Outside the UK');
  await chooseSettingByLabelAndContinue(page, 'Department for Education');
  await completePreferences(page, { emails: 'no', research: 'Yes' });
  await expectCheckYourAnswers(page);
}

/**
 * Reach Check your answers via Scotland + private nursery (role, no LA).
 */
export async function reachCheckYourAnswersScotlandNursery(
  page: Page,
  baseUrl: string,
  options: { label: string } = { label: 'cya-scotland' },
): Promise<void> {
  await startFreshRegistration(page, baseUrl, {
    label: options.label,
    firstName: 'Sam',
    surname: 'North',
  });
  await chooseWhereYouLiveAndContinue(page, 'Scotland');
  await chooseSettingByLabelAndContinue(page, 'Private nursery');
  await chooseRoleAndContinue(page, 'Student');
  await chooseExperienceAndContinue(page, 'Between 2 and 5 years');
  await completePreferences(page, { emails: 'yes', research: 'Yes' });
  await expectCheckYourAnswers(page);
}
