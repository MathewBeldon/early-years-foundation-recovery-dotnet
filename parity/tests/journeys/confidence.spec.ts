import { expect, test } from '../support/fixtures.js';
import { registerMinimalUser } from '../support/learning.js';
import { clickTrainingAction } from '../support/module-journey.js';
import {
  advanceConfidenceQuestion,
  chooseConfidenceAnswer,
  confidenceChoiceLabels,
  expectConfidenceUsesRadios,
  expectFirstConfidenceQuestion,
  FIRST_CONFIDENCE_QUESTION_PATH,
  resumeModuleFromOverview,
  visitAlphaOverview,
  walkToFirstConfidenceQuestion,
} from '../support/confidence.js';

/**
 * Confidence check radios, resume, and editable answers.
 * Mirrors spec/system/confidence_check_spec.rb.
 */
test.describe('Confidence check journey', () => {
  test.describe.configure({ timeout: 600_000 });

  test('confidence questions use radios not checkboxes', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'confidence-radios',
    });
    await walkToFirstConfidenceQuestion(targetPage);
    await expectFirstConfidenceQuestion(targetPage);
    await expectConfidenceUsesRadios(targetPage);
  });

  test('resume from overview lands on first confidence question', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'confidence-resume',
    });
    await walkToFirstConfidenceQuestion(targetPage);
    await expectFirstConfidenceQuestion(targetPage);

    await visitAlphaOverview(targetPage, urls.target);
    await resumeModuleFromOverview(targetPage);
    await expect(targetPage).toHaveURL(
      new RegExp(`${FIRST_CONFIDENCE_QUESTION_PATH}(?:\\?.*)?$`),
    );
  });

  test('answered confidence selections remain editable', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'confidence-edit',
    });
    await walkToFirstConfidenceQuestion(targetPage);
    await expectFirstConfidenceQuestion(targetPage);

    const { first, second } = await confidenceChoiceLabels(targetPage);

    for (let i = 0; i < 3; i += 1) {
      await chooseConfidenceAnswer(targetPage, first);
      await advanceConfidenceQuestion(targetPage);
    }

    await targetPage.goto(`${urls.target}${FIRST_CONFIDENCE_QUESTION_PATH}`);
    await expectFirstConfidenceQuestion(targetPage);
    await expect(targetPage.getByLabel(first, { exact: true })).toBeChecked();

    await chooseConfidenceAnswer(targetPage, second);
    await advanceConfidenceQuestion(targetPage);
    await clickTrainingAction(targetPage, 'Previous');
    await expect(targetPage.getByLabel(second, { exact: true })).toBeChecked();
  });
});
