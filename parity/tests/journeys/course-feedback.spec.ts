import { expect, test } from '../support/fixtures.js';
import {
  advanceFromCheckboxOtherOrSkippingAnsweredSkippable,
  completeGuestCourseFeedback,
  completeUserCourseFeedback,
  expectGuestFeedbackRadioOnlyNavigation,
  expectUpdateFeedbackWithPriorAnswer,
  followFooterFeedbackLink,
  returnHomeFromCourseFeedbackThankYou,
  returnToMyModulesFromCourseFeedbackThankYou,
  visitCourseFeedback,
  visitFeedbackCheckboxOtherOr,
} from '../support/course-feedback.js';
import { registerMinimalUser } from '../support/learning.js';

/**
 * Course-level feedback at /feedback (not end-of-module feedback).
 * Target-agnostic: TARGET_BASE_URL + One Login simulator for registered flows.
 */
test.describe('Course feedback journey', () => {
  test.describe.configure({ timeout: 300_000 });

  test('footer Feedback link navigates to course feedback index', async ({
    targetPage,
    urls,
  }) => {
    await targetPage.goto(`${urls.target}/`);
    await expect(
      targetPage.getByRole('heading', {
        level: 1,
        name: 'Early years child development training',
      }),
    ).toBeVisible();

    await followFooterFeedbackLink(targetPage);
    await expect(targetPage.getByRole('heading', { name: 'Give feedback' })).toBeVisible();
  });

  test('guest completes course feedback and returns home from thank-you', async ({
    targetPage,
    urls,
  }) => {
    await visitCourseFeedback(targetPage, urls.target);
    await completeGuestCourseFeedback(targetPage);
    await returnHomeFromCourseFeedbackThankYou(targetPage);
    await expect(
      targetPage.getByRole('heading', {
        level: 1,
        name: 'Early years child development training',
      }),
    ).toBeVisible();
  });

  test('registered user completes course feedback and returns to my modules from thank-you', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'course-feedback-user' });
    await visitCourseFeedback(targetPage, urls.target);
    await completeUserCourseFeedback(targetPage);
    await returnToMyModulesFromCourseFeedbackThankYou(targetPage);
  });

  test('completed course feedback can be updated with prior answer visible', async ({
    targetPage,
    urls,
  }) => {
    await visitCourseFeedback(targetPage, urls.target);
    await completeGuestCourseFeedback(targetPage);
    await visitCourseFeedback(targetPage, urls.target);
    await expectUpdateFeedbackWithPriorAnswer(targetPage);
  });

  test('registered user can update completed feedback with prior answer checked', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'course-feedback-update-user',
    });
    await visitCourseFeedback(targetPage, urls.target);
    await completeUserCourseFeedback(targetPage);
    await visitCourseFeedback(targetPage, urls.target);
    await expectUpdateFeedbackWithPriorAnswer(targetPage);
  });

  test('skips already-answered skippable question to thank-you', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'course-feedback-skippable',
    });
    await visitCourseFeedback(targetPage, urls.target);
    await completeUserCourseFeedback(targetPage);
    await visitFeedbackCheckboxOtherOr(targetPage, urls.target);
    await advanceFromCheckboxOtherOrSkippingAnsweredSkippable(targetPage);
  });

  test('guest feedback-radio-only step has Previous and Next controls', async ({
    targetPage,
    urls,
  }) => {
    await expectGuestFeedbackRadioOnlyNavigation(targetPage, urls.target);
  });
});
