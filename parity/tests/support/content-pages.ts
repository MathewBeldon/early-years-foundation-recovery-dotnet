import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { openModuleFromCard, startModule } from './learning.js';
import {
  ALPHA_PASS_SKIP_FEEDBACK,
  walkModuleSteps,
  type ModuleStep,
} from './module-journey.js';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

export const ALPHA_VIDEO_PAGE_PATH = '/modules/alpha/content-pages/1-2-1-2';

export const ALPHA_VIDEO_HEADER = '1-2-1-2';
export const ALPHA_VIDEO_BODY_SNIPPET =
  'In this video an early years expert explains';
export const TRANSCRIPT_BODY_SNIPPET =
  'The children have gone outside and started a bug hunt.';

const ALPHA_VIDEO_PAGE_INDEX = ALPHA_PASS_SKIP_FEEDBACK.findIndex(
  (step) => step.path === ALPHA_VIDEO_PAGE_PATH,
);

/** Steps from module start up to (not including) the alpha video page. */
const ALPHA_BEFORE_VIDEO_PAGE: ModuleStep[] = ALPHA_PASS_SKIP_FEEDBACK.slice(
  0,
  ALPHA_VIDEO_PAGE_INDEX,
);

/** Arrive at 1-2-1-2 without advancing — caller asserts video page behaviour. */
export const ALPHA_VIDEO_PAGE_STEP: ModuleStep = {
  path: ALPHA_VIDEO_PAGE_PATH,
  text: ALPHA_VIDEO_HEADER,
  actions: [],
};

function transcriptSummary(page: Page) {
  return page.locator('.govuk-details__summary-text', { hasText: 'Transcript' });
}

function transcriptBody(page: Page) {
  return page.locator('.govuk-details__text', {
    hasText: TRANSCRIPT_BODY_SNIPPET,
  });
}

export async function expectAlphaVideoPage(page: Page): Promise<void> {
  await expectPath(page, ALPHA_VIDEO_PAGE_PATH);
  await expect(page.getByText(ALPHA_VIDEO_HEADER).first()).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(page.getByText(ALPHA_VIDEO_BODY_SNIPPET)).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(page.locator('iframe')).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(transcriptSummary(page)).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function openTranscript(page: Page): Promise<void> {
  await transcriptSummary(page).click();
  await expect(transcriptBody(page)).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function closeTranscript(page: Page): Promise<void> {
  await transcriptSummary(page).click();
  await expect(transcriptBody(page)).toBeHidden({ timeout: STEP_TIMEOUT_MS });
}

/**
 * Start alpha and walk content pages up to the video page (1-2-1-2).
 * Caller must already be registered and on my-modules (or equivalent).
 */
export async function walkToAlphaVideoPage(page: Page): Promise<void> {
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);
  await walkModuleSteps(page, [...ALPHA_BEFORE_VIDEO_PAGE, ALPHA_VIDEO_PAGE_STEP]);
  await expectAlphaVideoPage(page);
}
