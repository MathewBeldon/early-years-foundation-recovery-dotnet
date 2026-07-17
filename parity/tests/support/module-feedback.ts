import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import {
  ALPHA_PASS_SKIP_FEEDBACK,
  clickTrainingAction,
  type ModuleStep,
  walkModuleSteps,
} from './module-journey.js';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

export const FEEDBACK_RADIO_ONLY_PATH =
  '/modules/alpha/questionnaires/feedback-radio-only';
export const FEEDBACK_CHECKBOX_OTHER_OR_PATH =
  '/modules/alpha/questionnaires/feedback-checkbox-other-or';
export const FEEDBACK_THANK_YOU_PATH = '/modules/alpha/content-pages/1-3-3-5';

/** Index of feedback-intro in the skip-feedback AST; feedback branch starts here. */
export const FEEDBACK_INTRO_INDEX = ALPHA_PASS_SKIP_FEEDBACK.findIndex((step) =>
  step.path.endsWith('/feedback-intro'),
);

/** Shared prefix: content → assessment → confidence through 1-3-3-4. */
export const ALPHA_PASS_THROUGH_CONFIDENCE = ALPHA_PASS_SKIP_FEEDBACK.slice(
  0,
  FEEDBACK_INTRO_INDEX,
);

/**
 * Feedback branch after confidence check. Mirrors alpha-pass-response-with-feedback.yml
 * content pages plus course-feedback-user.yml answer inputs (YAML note: answering not
 * implemented in ContentTestSchema). Questionnaire URLs follow pages_controller redirect.
 */
export const ALPHA_FEEDBACK_ANSWER_STEPS: ModuleStep[] = [
  {
    path: '/modules/alpha/content-pages/feedback-intro',
    text: 'Additional feedback',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/questionnaires/feedback-radio-only',
    text: 'Feedback radio buttons only',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/feedback-checkbox-only',
    text: 'Feedback checkboxes only',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field'] },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/feedback-textarea-only',
    text: 'Feedback textarea only',
    actions: [
      { op: 'fill', fieldId: 'response-text-input-field', value: 'free text' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/feedback-radio-other-more',
    text: 'Feedback radio buttons with large other',
    actions: [
      { op: 'choose', fieldId: 'response-answers-5-field' },
      { op: 'fill', fieldId: 'response-text-input-field', value: 'other text' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/feedback-checkbox-other-more',
    text: 'Feedback checkbox with large other',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field'] },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/feedback-radio-more',
    text: 'Feedback radio buttons with additional reasons',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/feedback-checkbox-other-or',
    text: 'Feedback checkboxes with Other and Or',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field'] },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/feedback-skippable',
    text: 'Skippable',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Next' },
    ],
  },
  ...ALPHA_PASS_SKIP_FEEDBACK.slice(FEEDBACK_INTRO_INDEX + 1),
];

/** Full alpha pass path with feedback answers (not skip). */
export const ALPHA_PASS_ANSWER_FEEDBACK: ModuleStep[] = [
  ...ALPHA_PASS_THROUGH_CONFIDENCE,
  ...ALPHA_FEEDBACK_ANSWER_STEPS,
];

export async function expectFeedbackQuestionPagination(
  page: Page,
  label: string,
): Promise<void> {
  await expect(page.getByText(label)).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

/**
 * Walk alpha module with feedback answers. Asserts feedback pagination on the first
 * questionnaire page (module_feedback_spec.rb: Page 1 of 9).
 */
export async function walkAlphaPassAnswerFeedback(page: Page): Promise<void> {
  await walkModuleSteps(page, ALPHA_PASS_THROUGH_CONFIDENCE);

  const introStep = ALPHA_FEEDBACK_ANSWER_STEPS[0];
  const firstQuestion = ALPHA_FEEDBACK_ANSWER_STEPS[1];
  if (!introStep || !firstQuestion) {
    throw new Error('ALPHA_FEEDBACK_ANSWER_STEPS missing intro or first question');
  }

  await walkModuleSteps(page, [introStep, firstQuestion]);
  await expectFeedbackQuestionPagination(page, 'Page 1 of 9');

  await walkModuleSteps(page, ALPHA_FEEDBACK_ANSWER_STEPS.slice(2));
}

export async function openFeedbackQuestion(
  page: Page,
  baseUrl: string,
  path: string,
): Promise<void> {
  await page.goto(`${baseUrl}${path}`, { waitUntil: 'domcontentloaded' });
  await expectPath(page, path);
}

export async function submitFeedbackAnswer(page: Page): Promise<void> {
  await clickTrainingAction(page, 'Next');
}

export async function expectFeedbackAnswerRequiredError(page: Page): Promise<void> {
  await expect(
    page.getByRole('link', { name: 'Please select an answer.' }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

/**
 * Answer the one-off skippable question on bravo so alpha feedback pagination
 * excludes it (FeedbackPaginationDecorator#other_forms). Mirrors RSpec factory
 * seeding training_module: 'bravo'.
 */
export async function answerBravoSkippableFeedback(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await openFeedbackQuestion(
    page,
    baseUrl,
    '/modules/bravo/questionnaires/feedback-skippable',
  );
  await expect(page.getByText('Skippable').first()).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await page.locator('#response-answers-1-field').check();
  await submitFeedbackAnswer(page);
}

export async function expectSkippedSkippablePaginationOnRadioOnly(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await openFeedbackQuestion(page, baseUrl, FEEDBACK_RADIO_ONLY_PATH);
  await expectFeedbackQuestionPagination(page, 'Page 1 of 8');
}

export async function advanceSkippedSkippableFromCheckboxOtherOr(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await openFeedbackQuestion(page, baseUrl, FEEDBACK_CHECKBOX_OTHER_OR_PATH);
  await expectFeedbackQuestionPagination(page, 'Page 7 of 8');

  const checkbox = page.locator('#response-answers-1-field');
  await expect(checkbox).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await checkbox.check();
  await submitFeedbackAnswer(page);

  await expectFeedbackQuestionPagination(page, 'Page 8 of 8');
  await expectPath(page, FEEDBACK_THANK_YOU_PATH);
  await expect(page.getByRole('heading', { name: 'Thank you' })).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}
