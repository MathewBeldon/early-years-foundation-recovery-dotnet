import { expect, test } from '../support/fixtures.js';
import {
  changeCheckYourAnswer,
  chooseExperienceAndContinue,
  chooseLocalAuthorityAndContinue,
  chooseRoleAndContinue,
  chooseSettingByLabelAndContinue,
  chooseTrainingEmailsAndContinue,
  chooseWhereYouLiveAndContinue,
  clickRegistrationBack,
  completePreferences,
  confirmCheckYourAnswers,
  expectCheckYourAnswers,
  expectPath,
  expectSummaryRow,
  expectSummaryRowAbsent,
  fillNameAndContinue,
  reachCheckYourAnswersEnglandNursery,
  reachCheckYourAnswersOutsideUkDfE,
  reachCheckYourAnswersScotlandNursery,
  startFreshRegistration,
} from '../support/registration.js';

/**
 * Registration back-links and Check your answers change flows.
 * Mirrors spec/requests/registration/check_your_answers_spec.rb plus
 * forward-journey Back navigation from link_helper.
 */
test.describe('Registration review and back navigation', () => {
  test.describe.configure({ timeout: 240_000 });

  test.describe('Back buttons on the forward journey', () => {
    test('steps back through the England nursery path in reverse order', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'back-england',
        firstName: 'Back',
        surname: 'Walker',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'England');
      await chooseSettingByLabelAndContinue(
        targetPage,
        'Local authority maintained nursery school',
      );
      await chooseLocalAuthorityAndContinue(targetPage, 'Leeds');
      await chooseRoleAndContinue(targetPage, 'Student');
      await chooseExperienceAndContinue(targetPage, 'Less than 2 years');
      await chooseTrainingEmailsAndContinue(targetPage, 'yes');
      await expectPath(targetPage, '/registration/research-participant/edit');

      await clickRegistrationBack(targetPage);
      await expectPath(targetPage, '/registration/training-emails/edit');

      await clickRegistrationBack(targetPage);
      await expectPath(targetPage, '/registration/early-years-experience/edit');

      await clickRegistrationBack(targetPage);
      await expectPath(targetPage, '/registration/role-type/edit');

      await clickRegistrationBack(targetPage);
      await expectPath(targetPage, '/registration/local-authority/edit');

      await clickRegistrationBack(targetPage);
      await expectPath(targetPage, '/registration/setting-type/edit');

      await clickRegistrationBack(targetPage);
      await expectPath(targetPage, '/registration/where-you-live/edit');

      await clickRegistrationBack(targetPage);
      await expectPath(targetPage, '/registration/name/edit');
      await expect(targetPage.getByLabel('First name')).toHaveValue('Back');
      await expect(targetPage.getByLabel('Surname')).toHaveValue('Walker');

      await clickRegistrationBack(targetPage);
      await expectPath(targetPage, '/registration/terms-and-conditions/edit');
    });

    test('skips LA and role when backing from emails after a no-role setting', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'back-dfe',
        firstName: 'Skip',
        surname: 'Back',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'England');
      await chooseSettingByLabelAndContinue(targetPage, 'Department for Education');
      await expectPath(targetPage, '/registration/training-emails/edit');

      await clickRegistrationBack(targetPage);
      await expectPath(targetPage, '/registration/setting-type/edit');
    });

    test('Previous on Check your answers returns to research participant', async ({
      targetPage,
      urls,
    }) => {
      await reachCheckYourAnswersEnglandNursery(targetPage, urls.target, {
        label: 'cya-previous',
      });
      await targetPage.getByRole('link', { name: 'Previous', exact: true }).click();
      await expectPath(targetPage, '/registration/research-participant/edit');
    });
  });

  test.describe('Check your answers change links', () => {
    test('changing name returns straight to Check your answers', async ({
      targetPage,
      urls,
    }) => {
      await reachCheckYourAnswersEnglandNursery(targetPage, urls.target, {
        label: 'cya-name',
        firstName: 'Jamie',
        surname: 'Review',
      });
      await expectSummaryRow(targetPage, 'Name', 'Jamie Review');

      await changeCheckYourAnswer(targetPage, 'name');
      await expectPath(targetPage, '/registration/name/edit');
      await fillNameAndContinue(targetPage, 'Casey', 'Jones');

      await expectCheckYourAnswers(targetPage);
      await expectSummaryRow(targetPage, 'Name', 'Casey Jones');
      await confirmCheckYourAnswers(targetPage);
    });

    test('Back while reviewing returns to Check your answers without saving', async ({
      targetPage,
      urls,
    }) => {
      await reachCheckYourAnswersEnglandNursery(targetPage, urls.target, {
        label: 'cya-back-review',
        firstName: 'Keep',
        surname: 'Name',
      });
      await changeCheckYourAnswer(targetPage, 'name');
      await expectPath(targetPage, '/registration/name/edit');
      await targetPage.getByLabel('First name').fill('Discarded');
      await clickRegistrationBack(targetPage);

      await expectCheckYourAnswers(targetPage);
      await expectSummaryRow(targetPage, 'Name', 'Keep Name');
    });

    test('England to Scotland clears local authority and returns to summary', async ({
      targetPage,
      urls,
    }) => {
      await reachCheckYourAnswersEnglandNursery(targetPage, urls.target, {
        label: 'cya-england-to-scotland',
      });
      await expectSummaryRow(targetPage, 'Local authority', 'Leeds');

      await changeCheckYourAnswer(targetPage, 'where you live');
      await chooseWhereYouLiveAndContinue(targetPage, 'Scotland');

      await expectCheckYourAnswers(targetPage);
      await expectSummaryRow(targetPage, 'Where you live', 'Scotland');
      await expectSummaryRowAbsent(targetPage, 'Local authority');
    });

    test('Scotland to England opens local authority then returns to summary', async ({
      targetPage,
      urls,
    }) => {
      await reachCheckYourAnswersScotlandNursery(targetPage, urls.target, {
        label: 'cya-scotland-to-england',
      });
      await expectSummaryRowAbsent(targetPage, 'Local authority');

      await changeCheckYourAnswer(targetPage, 'where you live');
      await chooseWhereYouLiveAndContinue(targetPage, 'England');
      await expectPath(targetPage, '/registration/local-authority/edit');
      await chooseLocalAuthorityAndContinue(targetPage, 'Leeds');

      await expectCheckYourAnswers(targetPage);
      await expectSummaryRow(targetPage, 'Where you live', 'England');
      await expectSummaryRow(targetPage, 'Local authority', 'Leeds');
    });

    test('no-role setting to early-years setting opens role and experience', async ({
      targetPage,
      urls,
    }) => {
      await reachCheckYourAnswersOutsideUkDfE(targetPage, urls.target, {
        label: 'cya-dfe-to-childminder',
      });
      await expectSummaryRowAbsent(targetPage, 'Role');
      await expectSummaryRowAbsent(targetPage, 'Time worked in early years');

      await changeCheckYourAnswer(targetPage, 'setting type');
      await chooseSettingByLabelAndContinue(targetPage, 'Independent childminder');
      await expectPath(targetPage, '/registration/role-type/edit');
      await chooseRoleAndContinue(targetPage, 'Childminder');
      await expectPath(targetPage, '/registration/early-years-experience/edit');
      await chooseExperienceAndContinue(targetPage, 'Between 2 and 5 years');

      await expectCheckYourAnswers(targetPage);
      await expectSummaryRow(targetPage, 'Setting type', 'Independent childminder');
      await expectSummaryRow(targetPage, 'Role', 'Childminder');
      await expectSummaryRow(
        targetPage,
        'Time worked in early years',
        'Between 2 and 5 years',
      );
      await expectSummaryRowAbsent(targetPage, 'Local authority');
    });

    test('England DfE to nursery opens LA, role and experience', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'cya-dfe-to-nursery',
        firstName: 'Pat',
        surname: 'Expand',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'England');
      await chooseSettingByLabelAndContinue(targetPage, 'Department for Education');
      await completePreferences(targetPage, { emails: 'yes', research: 'No' });
      await expectCheckYourAnswers(targetPage);
      await expectSummaryRowAbsent(targetPage, 'Local authority');
      await expectSummaryRowAbsent(targetPage, 'Role');

      await changeCheckYourAnswer(targetPage, 'setting type');
      await chooseSettingByLabelAndContinue(targetPage, 'Private nursery');
      await expectPath(targetPage, '/registration/local-authority/edit');
      await chooseLocalAuthorityAndContinue(targetPage, 'Manchester');
      await expectPath(targetPage, '/registration/role-type/edit');
      await chooseRoleAndContinue(targetPage, 'Student');
      await expectPath(targetPage, '/registration/early-years-experience/edit');
      await chooseExperienceAndContinue(targetPage, 'Less than 2 years');

      await expectCheckYourAnswers(targetPage);
      await expectSummaryRow(targetPage, 'Setting type', 'Private nursery');
      await expectSummaryRow(targetPage, 'Local authority', 'Manchester');
      await expectSummaryRow(targetPage, 'Role', 'Student');
      await expectSummaryRow(targetPage, 'Time worked in early years', 'Less than 2 years');
    });

    test('early-years setting to no-role setting clears role and experience', async ({
      targetPage,
      urls,
    }) => {
      await reachCheckYourAnswersEnglandNursery(targetPage, urls.target, {
        label: 'cya-nursery-to-dfe',
      });
      await expectSummaryRow(targetPage, 'Role', 'Student');

      await changeCheckYourAnswer(targetPage, 'setting type');
      await chooseSettingByLabelAndContinue(targetPage, 'Department for Education');

      await expectCheckYourAnswers(targetPage);
      await expectSummaryRow(targetPage, 'Setting type', 'Department for Education');
      await expectSummaryRowAbsent(targetPage, 'Local authority');
      await expectSummaryRowAbsent(targetPage, 'Role');
      await expectSummaryRowAbsent(targetPage, 'Time worked in early years');
    });

    test('changing email updates returns to summary with new preference', async ({
      targetPage,
      urls,
    }) => {
      await reachCheckYourAnswersEnglandNursery(targetPage, urls.target, {
        label: 'cya-emails',
      });
      await expectSummaryRow(targetPage, 'Email updates', 'Yes');

      await changeCheckYourAnswer(targetPage, 'email updates');
      await chooseTrainingEmailsAndContinue(targetPage, 'no');

      await expectCheckYourAnswers(targetPage);
      await expectSummaryRow(targetPage, 'Email updates', 'No');
      await confirmCheckYourAnswers(targetPage);
    });
  });
});
