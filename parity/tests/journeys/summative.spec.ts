import { expect, test } from '../support/fixtures.js';
import { registerMinimalUser } from '../support/learning.js';
import { clickTrainingAction } from '../support/module-journey.js';
import {
  expectFailedSummativeResultsWithWrongQuestions,
  expectPerfectSummativeResults,
  expectReflectLinkOnOverview,
  expectReflectTextWithoutLink,
  expectSummativeAnswerRequiredError,
  expectSummativeInputsDisabled,
  expectSummativeInputsEnabled,
  expectSummativeIntro,
  expectSummativePassmark,
  FIRST_SUMMATIVE_QUESTION_PATH,
  submitSummativeWithoutAnswer,
  visitAlphaOverview,
  walkToFailedAssessment,
  walkToFirstSummativeQuestion,
  walkToPassedAssessment,
  walkToSummativeIntro,
} from '../support/summative.js';

/**
 * Summative assessment intro, validation, pass/fail results, and overview links.
 * Mirrors spec/system/summative_assessment_spec.rb, common_page_spec.rb (passmark),
 * and assessment_results_spec.rb (overview back link where applicable).
 */
test.describe('Summative assessment journey', () => {
  test.describe.configure({ timeout: 600_000 });

  test('intro page shows End of module test and 70% passmark messaging', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'summative-intro' });
    await walkToSummativeIntro(targetPage);
    await expectSummativeIntro(targetPage);
    await expectSummativePassmark(targetPage);
  });

  test('empty submit on first question shows Please select an answer', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'summative-empty',
    });
    await walkToFirstSummativeQuestion(targetPage);
    await submitSummativeWithoutAnswer(targetPage);
    await expectSummativeAnswerRequiredError(targetPage);
  });

  test('pass shows 100% results, disables revisit, and links Reflect on overview', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'summative-pass' });
    await walkToPassedAssessment(targetPage);
    await expectPerfectSummativeResults(targetPage);

    await targetPage.goto(`${urls.target}${FIRST_SUMMATIVE_QUESTION_PATH}`);
    await expectSummativeInputsDisabled(targetPage);

    await visitAlphaOverview(targetPage, urls.target);
    await expectReflectLinkOnOverview(targetPage);
  });

  test('fail shows wrong questions, Reflect text without link, retake, and revisit topic', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'summative-fail' });
    await walkToFailedAssessment(targetPage);
    await expectFailedSummativeResultsWithWrongQuestions(targetPage);
    // RSpec asserts revisit links on the failed results page (before retake).
    await expect(
      targetPage.getByRole('link', { name: /revisit topic/i }).first(),
    ).toBeVisible();

    await visitAlphaOverview(targetPage, urls.target);
    await expectReflectTextWithoutLink(targetPage);

    await targetPage.goto(`${urls.target}/modules/alpha/assessment-result/1-3-2-11`);
    await targetPage.getByRole('link', { name: 'Retake test' }).first().click();
    await expect(targetPage).toHaveURL(/\/modules\/alpha\/content-pages\/1-3-2/);
    await clickTrainingAction(targetPage, 'Start test');
    await expect(targetPage).toHaveURL(
      /\/modules\/alpha\/questionnaires\/1-3-2-1/,
    );
    await expectSummativeInputsEnabled(targetPage);
  });
});
