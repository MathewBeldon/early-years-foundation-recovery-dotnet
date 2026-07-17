import { test } from '../support/fixtures.js';
import {
  openModuleFromCard,
  registerMinimalUser,
  startModule,
} from '../support/learning.js';
import {
  advanceSkippedSkippableFromCheckboxOtherOr,
  expectFeedbackAnswerRequiredError,
  expectFeedbackQuestionPagination,
  expectSkippedSkippablePaginationOnRadioOnly,
  FEEDBACK_RADIO_ONLY_PATH,
  openFeedbackQuestion,
  submitFeedbackAnswer,
  walkAlphaPassAnswerFeedback,
} from '../support/module-feedback.js';
import { seedAnsweredSkippableFeedback } from '../support/database-preconditions.js';
import {
  expectCertificatePage,
  expectCompletedModuleCard,
} from '../support/module-journey.js';

/**
 * Module feedback validation, skippable one-off pagination, and full pass.
 * Mirrors spec/system/module_feedback_spec.rb.
 */
test.describe('Module feedback journey', () => {
  test.describe.configure({ timeout: 600_000 });

  test('requires an answer before submitting feedback-radio-only', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'module-feedback-empty',
    });
    await openFeedbackQuestion(targetPage, urls.target, FEEDBACK_RADIO_ONLY_PATH);
    await submitFeedbackAnswer(targetPage);
    await expectFeedbackAnswerRequiredError(targetPage);
  });

  test('pagination counts skippable when not already answered', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'module-feedback-page-of-9',
    });
    await openFeedbackQuestion(targetPage, urls.target, FEEDBACK_RADIO_ONLY_PATH);
    await expectFeedbackQuestionPagination(targetPage, 'Page 1 of 9');
  });

  test('shows Page 1 of 8 when skippable one-off is already answered on another module', async ({
    targetPage,
    urls,
  }) => {
    const identity = await registerMinimalUser(targetPage, urls.target, {
      label: 'module-feedback-skippable-pagination',
    });
    await seedAnsweredSkippableFeedback(identity);
    await expectSkippedSkippablePaginationOnRadioOnly(targetPage, urls.target);
  });

  test('skips answered skippable when advancing through feedback-checkbox-other-or', async ({
    targetPage,
    urls,
  }) => {
    const identity = await registerMinimalUser(targetPage, urls.target, {
      label: 'module-feedback-skippable-skip',
    });
    await seedAnsweredSkippableFeedback(identity);
    await advanceSkippedSkippableFromCheckboxOtherOr(targetPage, urls.target);
  });

  test('passes assessment, answers feedback questionnaires, and completes with certificate', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'module-feedback',
    });
    await openModuleFromCard(targetPage, /First Training Module/);
    await startModule(targetPage);

    await walkAlphaPassAnswerFeedback(targetPage);
    await expectCertificatePage(targetPage);

    await targetPage.getByRole('link', { name: 'Go to my modules' }).click();
    await expectCompletedModuleCard(targetPage, 'First Training Module');
  });
});
