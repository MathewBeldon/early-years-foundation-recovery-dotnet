import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

type ClickAction = { op: 'click'; name: string };
type ChooseAction = { op: 'choose'; fieldId: string };
type CheckAction = { op: 'check'; fieldIds: string[] };
type FillAction = { op: 'fill'; fieldId: string; value: string };
type Action = ClickAction | ChooseAction | CheckAction | FillAction;

export type ModuleStep = {
  path: string;
  text: string | RegExp;
  actions: Action[];
};

/** Canonical alpha happy path (pass + skip feedback). Mirrors spec/support/ast/alpha-pass-response-skip-feedback.yml */
export const ALPHA_PASS_SKIP_FEEDBACK: ModuleStep[] = [
  {
    path: '/modules/alpha/content-pages/what-to-expect',
    text: 'What to expect during the training',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/content-pages/1-1',
    text: 'The first submodule',
    actions: [{ op: 'click', name: 'Start section' }],
  },
  {
    path: '/modules/alpha/content-pages/1-1-1',
    text: '1-1-1',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/content-pages/1-1-2',
    text: '1-1-2',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/content-pages/1-1-3',
    text: '1-1-3',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/content-pages/1-1-3-1',
    text: '1-1-3-1',
    actions: [
      { op: 'fill', fieldId: 'note-body-field', value: 'hello world' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/content-pages/1-1-4',
    text: '1-1-4',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/questionnaires/1-1-4-1',
    text: 'Question One - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Next' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/content-pages/1-2',
    text: 'The second submodule',
    actions: [{ op: 'click', name: 'Start section' }],
  },
  {
    path: '/modules/alpha/content-pages/1-2-1',
    text: '1-2-1',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/questionnaires/1-2-1-1',
    text: 'Question Two - Select from following',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field', 'response-answers-3-field'] },
      { op: 'click', name: 'Next' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/content-pages/1-2-1-2',
    text: '1-2-1-2',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/questionnaires/1-2-1-3',
    text: 'Question Three - Select from following',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field', 'response-answers-3-field'] },
      { op: 'click', name: 'Next' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/content-pages/1-3',
    text: 'Summary and next steps',
    actions: [{ op: 'click', name: 'Start section' }],
  },
  {
    path: '/modules/alpha/content-pages/1-3-1',
    text: 'Recap',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/content-pages/1-3-2',
    text: 'End of module test',
    actions: [{ op: 'click', name: 'Start test' }],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-1',
    text: 'Question One - Select from following',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field', 'response-answers-3-field'] },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-2',
    text: 'Question Two - Select from following',
    actions: [
      { op: 'check', fieldIds: ['response-answers-2-field', 'response-answers-3-field'] },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-3',
    text: 'Question Three - Select from following',
    actions: [
      { op: 'check', fieldIds: ['response-answers-3-field', 'response-answers-4-field'] },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-4',
    text: 'Question Four - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-3-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-5',
    text: 'Question Five - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-3-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-6',
    text: 'Question Six - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-3-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-7',
    text: 'Question Seven - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-3-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-8',
    text: 'Question Eight - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-3-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-9',
    text: 'Question Nine - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-3-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-10',
    text: 'Question Ten - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-3-field' },
      { op: 'click', name: 'Finish test' },
    ],
  },
  {
    path: '/modules/alpha/assessment-result/1-3-2-11',
    text: 'Assessment results',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/content-pages/1-3-3',
    text: 'Reflect on your learning',
    actions: [{ op: 'click', name: 'Next' }],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-3-1',
    text: 'Question One - Select from 1 to 5',
    actions: [
      { op: 'choose', fieldId: 'response-answers-5-field' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-3-2',
    text: 'Question Two - Select from 1 to 5',
    actions: [
      { op: 'choose', fieldId: 'response-answers-5-field' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-3-3',
    text: 'Question Three - Select from 1 to 5',
    actions: [
      { op: 'choose', fieldId: 'response-answers-5-field' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-3-4',
    text: 'Question Four - Select from 1 to 5',
    actions: [
      { op: 'choose', fieldId: 'response-answers-5-field' },
      { op: 'click', name: 'Next' },
    ],
  },
  {
    path: '/modules/alpha/content-pages/feedback-intro',
    text: 'Additional feedback',
    actions: [{ op: 'click', name: 'Skip feedback' }],
  },
  {
    path: '/modules/alpha/content-pages/1-3-3-5',
    text: 'Thank you',
    actions: [{ op: 'click', name: 'View certificate' }],
  },
  {
    path: '/modules/alpha/content-pages/1-3-4',
    text: 'Congratulations!',
    actions: [],
  },
];

/** Alpha fail path through assessment results. Mirrors alpha-fail-response.yml */
export const ALPHA_FAIL: ModuleStep[] = [
  ...ALPHA_PASS_SKIP_FEEDBACK.slice(0, 16), // through End of module test intro
  {
    path: '/modules/alpha/questionnaires/1-3-2-1',
    text: 'Question One - Select from following',
    actions: [
      { op: 'check', fieldIds: ['response-answers-2-field', 'response-answers-4-field'] },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-2',
    text: 'Question Two - Select from following',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field', 'response-answers-4-field'] },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-3',
    text: 'Question Three - Select from following',
    actions: [
      { op: 'check', fieldIds: ['response-answers-1-field', 'response-answers-2-field'] },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-4',
    text: 'Question Four - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-5',
    text: 'Question Five - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-6',
    text: 'Question Six - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-7',
    text: 'Question Seven - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-8',
    text: 'Question Eight - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-9',
    text: 'Question Nine - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Save and continue' },
    ],
  },
  {
    path: '/modules/alpha/questionnaires/1-3-2-10',
    text: 'Question Ten - Select from following',
    actions: [
      { op: 'choose', fieldId: 'response-answers-1-field' },
      { op: 'click', name: 'Finish test' },
    ],
  },
  {
    path: '/modules/alpha/assessment-result/1-3-2-11',
    text: 'Assessment results',
    actions: [],
  },
];

/**
 * Click training CTAs. Pagination uses #next-action / #previous-action with
 * aria-labels "Next Page" / "Previous Page" while visible text varies.
 */
export async function clickTrainingAction(page: Page, name: string): Promise<void> {
  const nextAction = page.locator('#next-action');
  if (await nextAction.isVisible().catch(() => false)) {
    const accessible = ((await nextAction.getAttribute('aria-label')) ?? '').trim();
    const visible = (await nextAction.innerText()).trim();
    if (
      accessible === name ||
      visible === name ||
      (name === 'Next' && (accessible === 'Next Page' || visible === 'Next'))
    ) {
      await nextAction.click();
      return;
    }
  }

  const previousAction = page.locator('#previous-action');
  if (await previousAction.isVisible().catch(() => false)) {
    const accessible = ((await previousAction.getAttribute('aria-label')) ?? '').trim();
    const visible = (await previousAction.innerText()).trim();
    if (
      accessible === name ||
      visible === name ||
      (name === 'Previous' &&
        (accessible === 'Previous Page' || visible === 'Previous'))
    ) {
      await previousAction.click();
      return;
    }
  }

  // Form submits (formative/summative/confidence) and Skip feedback use visible name.
  let control = page
    .getByRole('button', { name, exact: true })
    .or(page.getByRole('link', { name, exact: true }));
  if (name === 'Next') {
    control = control.or(
      page.getByRole('link', { name: 'Next Page', exact: true }),
    );
  }
  if (name === 'Previous') {
    control = control.or(
      page.getByRole('link', { name: 'Previous Page', exact: true }),
    );
  }
  await expect(control.first()).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await control.first().click();
}

async function applyAction(page: Page, action: Action): Promise<void> {
  switch (action.op) {
    case 'click':
      await clickTrainingAction(page, action.name);
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

/**
 * Drive a Contentful seed module walk from the current page (usually interruption).
 * Asserts each step path + heading/text before applying actions.
 */
export async function walkModuleSteps(page: Page, steps: ModuleStep[]): Promise<void> {
  for (const step of steps) {
    await expectPath(page, step.path);
    await expect(page.getByText(step.text).first()).toBeVisible({
      timeout: STEP_TIMEOUT_MS,
    });

    for (const [i, action] of step.actions.entries()) {
      await applyAction(page, action);

      // Formative: first Next submits and stays; wait for result banner before second Next.
      const nextAction = step.actions[i + 1];
      const nextIsAlsoNext =
        nextAction?.op === 'click' && nextAction.name === 'Next';
      if (
        step.path.includes('/questionnaires/') &&
        action.op === 'click' &&
        action.name === 'Next' &&
        nextIsAlsoNext
      ) {
        await expect(page.locator('#formative-results')).toBeVisible({
          timeout: STEP_TIMEOUT_MS,
        });
      }
    }
  }
}

export async function expectCertificatePage(page: Page): Promise<void> {
  await expectPath(page, '/modules/alpha/content-pages/1-3-4');
  await expect(page.getByText('Congratulations!')).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(
    page.getByText('You have now completed this module.'),
  ).toBeVisible();
  await expect(
    page.getByRole('link', { name: /Download or print your certificate/i }),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Go to my modules' })).toBeVisible();
}

export async function expectPassedAssessment(page: Page): Promise<void> {
  await expectPath(page, '/modules/alpha/assessment-result/1-3-2-11');
  const results = page.locator('#assessment-results');
  await expect(results.getByText('Congratulations')).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(results.getByText(/You scored \d+%/)).toBeVisible();
}

export async function expectFailedAssessment(page: Page): Promise<void> {
  await expectPath(page, '/modules/alpha/assessment-result/1-3-2-11');
  const results = page.locator('#assessment-results');
  await expect(results.getByText('Revisit the module')).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(results.getByText(/You scored \d+%/)).toBeVisible();
  await expect(
    results.getByText(
      /Unfortunately you have not scored highly enough to receive a certificate/,
    ),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Retake test' })).toBeVisible();
}

export async function expectCompletedModuleCard(
  page: Page,
  title: string,
): Promise<void> {
  await expectPath(page, '/my-modules');
  const completed = page.locator('#completed');
  await expect(completed).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(completed.getByText(title)).toBeVisible();
  await expect(page.locator('#started').getByText(title)).toHaveCount(0);
}
