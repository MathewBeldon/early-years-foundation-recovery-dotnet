import { test } from '../support/fixtures.js';
import {
  advanceFormativeAfterResults,
  chooseFormativeAnswer,
  expectCorrectFormativeResults,
  expectFirstFormativePage,
  expectFormativeAnswerRequiredError,
  expectFormativeInputsLocked,
  expectWrongFormativeResults,
  FIRST_FORMATIVE_PATH,
  FIRST_FORMATIVE_WRONG_ANSWER_FIELD,
  resumeFormativeFromOverview,
  submitCorrectFormativeAnswer,
  submitFormativeAnswer,
  walkToFirstFormative,
} from '../support/formative.js';
import { registerMinimalUser } from '../support/learning.js';
import { expectPath } from '../support/registration.js';

/**
 * Formative answer lock, resume, and validation.
 * Mirrors spec/system/formative_question_spec.rb.
 */
test.describe('Formative journey', () => {
  test.describe.configure({ timeout: 300_000 });

  test('shows that is right feedback and locks inputs after correct answer', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'formative-correct' });
    await walkToFirstFormative(targetPage);
    await expectFirstFormativePage(targetPage);

    await submitCorrectFormativeAnswer(targetPage);
    await expectCorrectFormativeResults(targetPage);

    await targetPage.goto(`${urls.target}${FIRST_FORMATIVE_PATH}`);
    await expectFirstFormativePage(targetPage);
    await expectFormativeInputsLocked(targetPage);
  });

  test('resume from overview after answering formative lands on questionnaire', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'formative-resume' });
    await walkToFirstFormative(targetPage);
    await expectFirstFormativePage(targetPage);

    await submitCorrectFormativeAnswer(targetPage);
    await expectCorrectFormativeResults(targetPage);

    await resumeFormativeFromOverview(targetPage, urls.target);
    await expectPath(targetPage, FIRST_FORMATIVE_PATH);
    await expectCorrectFormativeResults(targetPage);
  });

  test('shows not-quite-right feedback, locks inputs, and advances after wrong answer', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'formative-wrong' });
    await walkToFirstFormative(targetPage);
    await expectFirstFormativePage(targetPage);

    await chooseFormativeAnswer(targetPage, FIRST_FORMATIVE_WRONG_ANSWER_FIELD);
    await submitFormativeAnswer(targetPage);
    await expectWrongFormativeResults(targetPage);

    await advanceFormativeAfterResults(targetPage);
  });

  test('requires an answer before submitting a formative question', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'formative-empty' });
    await walkToFirstFormative(targetPage);
    await expectFirstFormativePage(targetPage);

    await submitFormativeAnswer(targetPage);
    await expectFormativeAnswerRequiredError(targetPage);
  });
});
