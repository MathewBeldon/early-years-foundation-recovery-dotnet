import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { continueFromInterruption, startModule } from './learning.js';
import {
  ALPHA_PASS_SKIP_FEEDBACK,
  type ModuleStep,
  walkModuleSteps,
} from './module-journey.js';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

const THIRD_TOPIC_INTRO = ALPHA_PASS_SKIP_FEEDBACK[4];
if (!THIRD_TOPIC_INTRO) {
  throw new Error('ALPHA_PASS_SKIP_FEEDBACK missing third topic intro step');
}

/** Through 1-1-3 intro only (no Next) — mirrors view_pages_upto(alpha, 'topic_intro', 3). */
export const ALPHA_THROUGH_THIRD_TOPIC_INTRO: ModuleStep[] = [
  ...ALPHA_PASS_SKIP_FEEDBACK.slice(0, 4),
  {
    ...THIRD_TOPIC_INTRO,
    actions: [],
  },
];

/** Through formative 1-1-4-1 with correct answer — mirrors start_first_topic(alpha). */
export const ALPHA_FIRST_TOPIC_COMPLETE: ModuleStep[] =
  ALPHA_PASS_SKIP_FEEDBACK.slice(0, 8);

export async function visitAlphaOverview(page: Page, baseUrl: string): Promise<void> {
  await page.goto(`${baseUrl}/modules/alpha`, { waitUntil: 'domcontentloaded' });
  await expectPath(page, '/modules/alpha');
}

export async function expectStartModuleCta(page: Page): Promise<void> {
  const start = page.getByRole('link', { name: 'Start module' });
  await expect(start.first()).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(start.first()).toHaveAttribute(
    'href',
    '/modules/alpha/content-pages/what-to-expect',
  );
  await expect(page.getByRole('link', { name: 'Resume module' })).toHaveCount(0);
}

export async function expectResumeModuleCta(
  page: Page,
  resumeHref: string,
): Promise<void> {
  const resume = page.getByRole('link', { name: 'Resume module' });
  await expect(resume.first()).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(resume.first()).toHaveAttribute('href', resumeHref);
  await expect(page.getByRole('link', { name: 'Start module' })).toHaveCount(0);
}

/** Mirrors module_overview_progress_spec: submodule intro reached, first topic not linked. */
export async function expectSoftGatedFirstTopic(page: Page): Promise<void> {
  const section = page.locator('#section-content-1');
  await expect(section).toBeVisible({ timeout: STEP_TIMEOUT_MS });

  const firstItem = section
    .locator('.module-section--container .module-section--item')
    .first();
  await expect(firstItem.getByText('1-1-1')).toBeVisible();
  await expect(
    firstItem.getByRole('link', { name: '1-1-1', exact: true }),
  ).toHaveCount(0);
  await expect(firstItem.locator('.progress-indicator')).toContainText('not started');
}

/** Mirrors module_overview_progress_spec: fresh module, all indicators not started. */
export async function expectFreshOverviewNotStartedIndicators(
  page: Page,
): Promise<void> {
  const section1 = page.locator('#section-content-1');
  await expect(section1.getByText('not started')).toHaveCount(4);
  await expect(section1.getByText('1-1-1')).toBeVisible();
  await expect(
    section1.getByRole('link', { name: '1-1-1', exact: true }),
  ).toHaveCount(0);

  const section2 = page.locator('#section-content-2');
  await expect(section2.getByText('not started')).toHaveCount(1);

  const section3 = page.locator('#section-content-3');
  await expect(section3.getByText('not started')).toHaveCount(3);
  await expect(section3.getByRole('link', { name: 'Recap' })).toHaveCount(0);
  await expect(section3.getByRole('link', { name: 'End of module test' })).toHaveCount(0);
  await expect(
    section3.getByRole('link', { name: 'Reflect on your learning' }),
  ).toHaveCount(0);

  const section4 = page.locator('#section-content-4');
  await expect(section4.getByText('not started')).toHaveCount(1);
  await expect(
    section4.getByRole('link', { name: 'Download your certificate' }),
  ).toHaveCount(0);
}

/** Mirrors module_overview_progress_spec: first topic complete after formative pass. */
export async function expectFirstTopicCompleteIndicator(page: Page): Promise<void> {
  const firstItem = page
    .locator('#section-content-1 .module-section--container .module-section--item')
    .first();
  await expect(
    firstItem.getByRole('link', { name: '1-1-1', exact: true }),
  ).toHaveAttribute('href', '/modules/alpha/content-pages/1-1-1');
  await expect(firstItem.locator('.progress-indicator')).toContainText('complete');
}

/** Mirrors module_overview_progress_spec: partial third topic in progress, not linkable. */
export async function expectPartialThirdTopicInProgress(page: Page): Promise<void> {
  const thirdItem = page
    .locator('#section-content-1 .module-section--container .module-section--item')
    .nth(2);
  await expect(thirdItem.locator('.progress-indicator')).toContainText('in progress');
  await expect(
    thirdItem.getByRole('link', { name: '1-1-3', exact: true }),
  ).toHaveCount(0);
}

export async function expectRetakeTestCta(page: Page): Promise<void> {
  await expect(page.getByRole('link', { name: 'Retake test' }).first()).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function expectNoRetakeTestCta(page: Page): Promise<void> {
  await expect(page.getByRole('link', { name: 'Retake test' })).toHaveCount(0);
}

export async function walkAlphaFromStart(page: Page): Promise<void> {
  await startModule(page);
  await continueFromInterruption(page);
}

export async function walkAlphaThroughFirstTopicComplete(page: Page): Promise<void> {
  await walkModuleSteps(page, ALPHA_FIRST_TOPIC_COMPLETE);
  await expectPath(page, '/modules/alpha/content-pages/1-2');
}

export async function walkAlphaThroughPartialThirdTopic(page: Page): Promise<void> {
  await walkModuleSteps(page, ALPHA_THROUGH_THIRD_TOPIC_INTRO);
}
