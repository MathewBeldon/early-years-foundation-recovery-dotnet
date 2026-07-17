import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { expectMyModulesPage } from './learning.js';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

type ClickAction = { op: 'click'; name: string };
type ChooseAction = { op: 'choose'; fieldId: string };
type CheckAction = { op: 'check'; fieldIds: string[] };
type FillAction = { op: 'fill'; fieldId: string; value: string };
type CourseFeedbackAction = ClickAction | ChooseAction | CheckAction | FillAction;

export type CourseFeedbackStep = {
  path: string;
  text: string;
  actions: CourseFeedbackAction[];
};

/**
 * Guest course feedback walk. Mirrors spec/support/ast/course-feedback-guest.yml
 * (skippable question omitted for guests).
 */
export const COURSE_FEEDBACK_GUEST_STEPS: CourseFeedbackStep[] = [
  {
    path: '/feedback',
    text: 'Give feedback',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/feedback/feedback-radio-only',
    text: 'Feedback radio buttons only',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/feedback/feedback-checkbox-only',
    text: 'Feedback checkboxes only',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field'] },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/feedback/feedback-textarea-only',
    text: 'Feedback textarea only',
    actions: [
      { op: 'fill', fieldId: 'response-text-input-field', value: 'free text' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/feedback/feedback-radio-other-more',
    text: 'Feedback radio buttons with large other',
    actions: [
      { op: 'choose', fieldId: 'response-answers-5-field' },
      { op: 'fill', fieldId: 'response-text-input-field', value: 'other text' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/feedback/feedback-checkbox-other-more',
    text: 'Feedback checkbox with large other',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field'] },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/feedback/feedback-radio-more',
    text: 'Feedback radio buttons with additional reasons',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/feedback/feedback-checkbox-other-or',
    text: 'Feedback checkboxes with Other and Or',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field'] },
      { op: 'click', name: 'Next' },
    ],
  },
];

/**
 * Registered user course feedback walk. Mirrors spec/support/ast/course-feedback-user.yml
 * (includes skippable one-off question).
 */
export const COURSE_FEEDBACK_USER_STEPS: CourseFeedbackStep[] = [
  ...COURSE_FEEDBACK_GUEST_STEPS,
  {
    path: '/feedback/feedback-skippable',
    text: 'Skippable',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Next' },
    ],
  },
];

async function clickCourseFeedbackAction(page: Page, name: string): Promise<void> {
  // Course feedback uses govuk button/link submits — not #next-action training pagination.
  const control = page
    .getByRole('button', { name, exact: true })
    .or(page.getByRole('link', { name, exact: true }));
  await expect(control.first()).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await control.first().click();
}

async function applyCourseFeedbackAction(
  page: Page,
  action: CourseFeedbackAction,
): Promise<void> {
  switch (action.op) {
    case 'click':
      await clickCourseFeedbackAction(page, action.name);
      return;
    case 'choose': {
      const input = page.locator(`#${action.fieldId}`);
      await expect(input).toBeVisible({ timeout: STEP_TIMEOUT_MS });
      await input.check();
      return;
    }
    case 'check':
      for (const fieldId of action.fieldIds) {
        const input = page.locator(`#${fieldId}`);
        await expect(input).toBeVisible({ timeout: STEP_TIMEOUT_MS });
        await input.check();
      }
      return;
    case 'fill': {
      const input = page.locator(`#${action.fieldId}`);
      await expect(input).toBeVisible({ timeout: STEP_TIMEOUT_MS });
      await input.fill(action.value);
      return;
    }
  }
}

export async function walkCourseFeedbackSteps(
  page: Page,
  steps: CourseFeedbackStep[],
): Promise<void> {
  for (const step of steps) {
    await expectPath(page, step.path);
    await expect(page.getByText(step.text).first()).toBeVisible({
      timeout: STEP_TIMEOUT_MS,
    });

    for (const action of step.actions) {
      await applyCourseFeedbackAction(page, action);
    }
  }
}

export async function followFooterFeedbackLink(page: Page): Promise<void> {
  await page
    .locator('.govuk-footer')
    .getByRole('link', { name: 'Feedback', exact: true })
    .click();
  await expectPath(page, '/feedback');
}

export async function visitCourseFeedback(page: Page, baseUrl: string): Promise<void> {
  await page.goto(`${baseUrl}/feedback`);
  await expectPath(page, '/feedback');
}

export async function expectCourseFeedbackThankYou(page: Page): Promise<void> {
  await expectPath(page, '/feedback/thank-you');
  await expect(page.getByRole('heading', { name: 'Thank you' })).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(page.getByRole('heading', { name: 'Thank you' })).toBeVisible();
}

export async function completeGuestCourseFeedback(page: Page): Promise<void> {
  await walkCourseFeedbackSteps(page, COURSE_FEEDBACK_GUEST_STEPS);
  await expectCourseFeedbackThankYou(page);
}

export async function completeUserCourseFeedback(page: Page): Promise<void> {
  await walkCourseFeedbackSteps(page, COURSE_FEEDBACK_USER_STEPS);
  await expectCourseFeedbackThankYou(page);
}

export async function expectUpdateFeedbackWithPriorAnswer(page: Page): Promise<void> {
  await expectPath(page, '/feedback');
  await page.getByRole('link', { name: 'Update my feedback' }).click();
  await expectPath(page, '/feedback/feedback-radio-only');
  await expect(page.getByText('Feedback radio buttons only').first()).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(page.getByLabel('Option 1', { exact: true })).toBeChecked();
}

export async function expectGuestFeedbackRadioOnlyNavigation(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await page.goto(`${baseUrl}/feedback/feedback-radio-only`);
  await expectPath(page, '/feedback/feedback-radio-only');
  await expect(page.getByText('Feedback radio buttons only').first()).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(page.getByRole('link', { name: 'Previous' })).toHaveAttribute(
    'href',
    '/feedback',
  );
  await expect(page.getByRole('button', { name: 'Next' })).toBeVisible();
}

export async function visitFeedbackCheckboxOtherOr(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await page.goto(`${baseUrl}/feedback/feedback-checkbox-other-or`);
  await expectPath(page, '/feedback/feedback-checkbox-other-or');
  await expect(
    page.getByText('Feedback checkboxes with Other and Or').first(),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

/**
 * When feedback-skippable is already answered, Next from checkbox-other-or
 * lands on thank-you (mirrors spec/system/course_feedback_spec.rb one-off skip).
 */
export async function advanceFromCheckboxOtherOrSkippingAnsweredSkippable(
  page: Page,
): Promise<void> {
  await page.locator('#response-answers-1-field').check();
  await clickCourseFeedbackAction(page, 'Next');
  await expectCourseFeedbackThankYou(page);
}

export async function returnHomeFromCourseFeedbackThankYou(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Go to home' }).click();
  await expectPath(page, '/');
}

export async function returnToMyModulesFromCourseFeedbackThankYou(
  page: Page,
): Promise<void> {
  await page.getByRole('link', { name: 'Go to my modules' }).click();
  await expectMyModulesPage(page);
}
