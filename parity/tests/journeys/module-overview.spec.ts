import { test } from '../support/fixtures.js';
import {
  continueFromInterruption,
  openModuleFromCard,
  registerMinimalUser,
  startModule,
} from '../support/learning.js';
import {
  ALPHA_FAIL,
  ALPHA_PASS_SKIP_FEEDBACK,
  walkModuleSteps,
} from '../support/module-journey.js';
import {
  expectFirstTopicCompleteIndicator,
  expectFreshOverviewNotStartedIndicators,
  expectNoRetakeTestCta,
  expectPartialThirdTopicInProgress,
  expectResumeModuleCta,
  expectRetakeTestCta,
  expectSoftGatedFirstTopic,
  expectStartModuleCta,
  visitAlphaOverview,
  walkAlphaThroughFirstTopicComplete,
  walkAlphaThroughPartialThirdTopic,
} from '../support/module-overview.js';

/**
 * Module overview progress indicators and soft gating.
 * Target-agnostic: TARGET_BASE_URL + One Login simulator only.
 */
test.describe('Module overview progress journey', () => {
  test.describe.configure({ timeout: 240_000 });

  test('shows Start module before progress and Resume after first section intro', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'overview-progress' });
    await openModuleFromCard(targetPage, /First Training Module/);
    await expectStartModuleCta(targetPage);

    await startModule(targetPage);
    await continueFromInterruption(targetPage);

    await targetPage.getByRole('link', { name: 'Back to Module 1 overview' }).click();
    await expectResumeModuleCta(targetPage, '/modules/alpha/content-pages/1-1');
    await expectSoftGatedFirstTopic(targetPage);
  });

  test('shows not-started indicators and soft-gated topics on fresh overview', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'overview-fresh' });
    await openModuleFromCard(targetPage, /First Training Module/);
    await expectStartModuleCta(targetPage);
    await expectFreshOverviewNotStartedIndicators(targetPage);
  });

  test.describe('long module walks', () => {
    test.describe.configure({ timeout: 600_000 });

    test('shows first topic as linkable with complete indicator after formative pass', async ({
      targetPage,
      urls,
    }) => {
      await registerMinimalUser(targetPage, urls.target, {
        label: 'overview-first-topic',
      });
      await openModuleFromCard(targetPage, /First Training Module/);
      await startModule(targetPage);
      await walkAlphaThroughFirstTopicComplete(targetPage);

      await visitAlphaOverview(targetPage, urls.target);
      await expectFirstTopicCompleteIndicator(targetPage);
      await expectResumeModuleCta(targetPage, '/modules/alpha/content-pages/1-2');
    });

    test('shows in progress indicator and no link for partially viewed third topic', async ({
      targetPage,
      urls,
    }) => {
      await registerMinimalUser(targetPage, urls.target, {
        label: 'overview-partial-topic',
      });
      await openModuleFromCard(targetPage, /First Training Module/);
      await startModule(targetPage);
      await walkAlphaThroughPartialThirdTopic(targetPage);

      await visitAlphaOverview(targetPage, urls.target);
      await expectPartialThirdTopicInProgress(targetPage);
      await expectResumeModuleCta(targetPage, '/modules/alpha/content-pages/1-1-3');
    });

    test('shows Retake test on overview after failed summative assessment', async ({
      targetPage,
      urls,
    }) => {
      await registerMinimalUser(targetPage, urls.target, { label: 'overview-fail' });
      await openModuleFromCard(targetPage, /First Training Module/);
      await startModule(targetPage);
      await walkModuleSteps(targetPage, ALPHA_FAIL);

      await visitAlphaOverview(targetPage, urls.target);
      await expectRetakeTestCta(targetPage);
    });

    test('does not show Retake test on overview after full module pass', async ({
      targetPage,
      urls,
    }) => {
      await registerMinimalUser(targetPage, urls.target, { label: 'overview-pass' });
      await openModuleFromCard(targetPage, /First Training Module/);
      await startModule(targetPage);
      await walkModuleSteps(targetPage, ALPHA_PASS_SKIP_FEEDBACK);

      await visitAlphaOverview(targetPage, urls.target);
      await expectNoRetakeTestCta(targetPage);
    });
  });
});
