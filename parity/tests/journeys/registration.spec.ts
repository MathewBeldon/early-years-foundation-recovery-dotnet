import { newSimulatorUser, signInViaOneLogin } from '../support/auth.js';
import { expect, test } from '../support/fixtures.js';
import {
  acceptTermsAndContinue,
  chooseCustomRoleAndContinue,
  chooseCustomSettingAndContinue,
  chooseExperienceAndContinue,
  chooseLocalAuthorityAndContinue,
  chooseResearchParticipantAndContinue,
  chooseRoleAndContinue,
  chooseSettingByLabelAndContinue,
  chooseTrainingEmailsAndContinue,
  chooseWhereYouLiveAndContinue,
  completePreferences,
  confirmCheckYourAnswers,
  continueRegistration,
  expectPath,
  expectProblem,
  expectRoleRadios,
  expectSummaryRow,
  expectSummaryRowAbsent,
  fillNameAndContinue,
  startFreshRegistration,
} from '../support/registration.js';

/**
 * Standalone registration journeys.
 * Drive only TARGET_BASE_URL + the One Login simulator — no Rails test helpers.
 * Run via parity-journey (Rails) or parity-journey-gateway (YARP); same specs.
 *
 * Branch matrix mirrors spec/requests/registration/registration_flow_spec.rb:
 *   - England + LA + early-years role
 *   - England + LA + no role
 *   - England + no LA + no role
 *   - England + custom setting (skips LA and role)
 *   - Outside England + early-years / childminder role (no LA)
 *   - Outside England + no role
 *   - Outside England + custom setting (asks for role)
 */
test.describe('Registration journey', () => {
  // Contentful-backed steps regularly take 6–10s each under migration Compose.
  test.describe.configure({ timeout: 240_000 });

  test('redirects unauthenticated visitors from registration to sign-in', async ({
    targetPage,
    urls,
  }) => {
    await targetPage.goto(`${urls.target}/registration/terms-and-conditions/edit`);
    await expect(targetPage).toHaveURL(/\/users\/sign-in/);
    await expect(
      targetPage.getByRole('heading', { name: 'How to access this training course' }),
    ).toBeVisible();
  });

  test('shows validation errors on required steps before completing outside England custom setting', async ({
    targetPage,
    urls,
  }) => {
    const user = newSimulatorUser('validation');
    await signInViaOneLogin(targetPage, user, urls.target);

    await expectPath(targetPage, '/registration/terms-and-conditions/edit');
    await continueRegistration(targetPage);
    await expectProblem(
      targetPage,
      'You must accept the terms and conditions and privacy policy to create an account.',
    );
    await acceptTermsAndContinue(targetPage);

    await expectPath(targetPage, '/registration/name/edit');
    await continueRegistration(targetPage);
    await expectProblem(targetPage, 'Enter a first name.');
    await expectProblem(targetPage, 'Enter a surname.');
    await fillNameAndContinue(targetPage, 'Jane', 'Doe');

    await chooseWhereYouLiveAndContinue(targetPage, 'Scotland');
    await expectPath(targetPage, '/registration/setting-type/edit');
    await continueRegistration(targetPage);
    await expectProblem(targetPage, 'Enter the setting type you work in.');
    await chooseCustomSettingAndContinue(targetPage, 'user defined setting type');

    await expectPath(targetPage, '/registration/role-type/edit');
    await continueRegistration(targetPage);
    await expectProblem(targetPage, 'Select your role.');
    await chooseCustomRoleAndContinue(targetPage, 'user defined job title');

    await expectPath(targetPage, '/registration/early-years-experience/edit');
    await continueRegistration(targetPage);
    await expectProblem(targetPage, 'Choose an option.');
    await chooseExperienceAndContinue(targetPage, 'Less than 2 years');

    await expectPath(targetPage, '/registration/training-emails/edit');
    await continueRegistration(targetPage);
    await expectProblem(targetPage, 'Choose an option.');
    await chooseTrainingEmailsAndContinue(targetPage, 'yes');

    await expectPath(targetPage, '/registration/research-participant/edit');
    await continueRegistration(targetPage);
    await expectProblem(targetPage, 'Choose an option.');
    await chooseResearchParticipantAndContinue(targetPage, 'Yes');

    await expectSummaryRow(targetPage, 'Setting type', 'user defined setting type');
    await expectSummaryRow(targetPage, 'Role', 'user defined job title');
    await expectSummaryRowAbsent(targetPage, 'Local authority');
    await confirmCheckYourAnswers(targetPage);
  });

  test.describe('England branches', () => {
    test('LA-required setting with early-years role asks LA, role and experience', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'england-la-role',
        firstName: 'John',
        surname: 'Smith',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'England');
      await chooseSettingByLabelAndContinue(
        targetPage,
        'Local authority maintained nursery school',
      );
      await chooseLocalAuthorityAndContinue(targetPage, 'Leeds');
      await expectRoleRadios(
        targetPage,
        ['Student', 'Early years practitioner', 'Manager or team leader'],
        ['Childminder', 'Childminder assistant'],
      );
      await chooseRoleAndContinue(targetPage, 'Student');
      await chooseExperienceAndContinue(targetPage, 'Less than 2 years');
      await completePreferences(targetPage, { emails: 'yes', research: 'No' });

      await expectSummaryRow(
        targetPage,
        'Setting type',
        'Local authority maintained nursery school',
      );
      await expectSummaryRow(targetPage, 'Local authority', 'Leeds');
      await expectSummaryRow(targetPage, 'Role', 'Student');
      await confirmCheckYourAnswers(targetPage);
    });

    test('LA-required setting without role asks LA then skips role and experience', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'england-la-no-role',
        firstName: 'Alex',
        surname: 'Local',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'England');
      await chooseSettingByLabelAndContinue(targetPage, 'Local authority');
      await chooseLocalAuthorityAndContinue(targetPage, 'Leeds');
      // role_type: none → straight to preferences
      await expectPath(targetPage, '/registration/training-emails/edit');
      await completePreferences(targetPage, { emails: 'no', research: 'No' });

      await expectSummaryRow(targetPage, 'Setting type', 'Local authority');
      await expectSummaryRow(targetPage, 'Local authority', 'Leeds');
      await expectSummaryRowAbsent(targetPage, 'Role');
      await confirmCheckYourAnswers(targetPage);
    });

    test('no-LA no-role setting skips LA, role and experience', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'england-dfe',
        firstName: 'Sam',
        surname: 'Gov',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'England');
      await chooseSettingByLabelAndContinue(targetPage, 'Department for Education');
      await expectPath(targetPage, '/registration/training-emails/edit');
      await completePreferences(targetPage, { emails: 'yes', research: 'Yes' });

      await expectSummaryRow(targetPage, 'Setting type', 'Department for Education');
      await expectSummaryRowAbsent(targetPage, 'Local authority');
      await expectSummaryRowAbsent(targetPage, 'Role');
      await confirmCheckYourAnswers(targetPage);
    });

    test('custom setting skips LA and role (unlike outside England)', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'england-custom',
        firstName: 'Pat',
        surname: 'Other',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'England');
      await chooseCustomSettingAndContinue(targetPage, 'parent carer setting');
      await expectPath(targetPage, '/registration/training-emails/edit');
      await completePreferences(targetPage, { emails: 'no', research: 'Yes' });

      await expectSummaryRow(targetPage, 'Setting type', 'parent carer setting');
      await expectSummaryRowAbsent(targetPage, 'Local authority');
      await expectSummaryRowAbsent(targetPage, 'Role');
      await confirmCheckYourAnswers(targetPage);
    });

    test('LA-required setting with custom role description still asks experience', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'england-custom-role',
        firstName: 'Riley',
        surname: 'Nursery',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'England');
      await chooseSettingByLabelAndContinue(targetPage, 'Private nursery');
      await chooseLocalAuthorityAndContinue(targetPage, 'Manchester');
      await chooseCustomRoleAndContinue(targetPage, 'Forest school lead');
      await chooseExperienceAndContinue(targetPage, 'Between 2 and 5 years');
      await completePreferences(targetPage, { emails: 'yes', research: 'No' });

      await expectSummaryRow(targetPage, 'Setting type', 'Private nursery');
      await expectSummaryRow(targetPage, 'Local authority', 'Manchester');
      await expectSummaryRow(targetPage, 'Role', 'Forest school lead');
      await confirmCheckYourAnswers(targetPage);
    });
  });

  test.describe('Outside England branches', () => {
    test('childminder setting asks childminder roles and never local authority', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'scotland-childminder',
        firstName: 'Chris',
        surname: 'Mind',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'Scotland');
      await chooseSettingByLabelAndContinue(targetPage, 'Independent childminder');
      await expectPath(targetPage, '/registration/role-type/edit');
      await expectRoleRadios(
        targetPage,
        ['Childminder', 'Childminder assistant'],
        ['Student', 'Early years practitioner'],
      );
      await chooseRoleAndContinue(targetPage, 'Childminder');
      await chooseExperienceAndContinue(targetPage, '10 years or more');
      await completePreferences(targetPage, { emails: 'yes', research: 'Yes' });

      await expectSummaryRow(targetPage, 'Setting type', 'Independent childminder');
      await expectSummaryRow(targetPage, 'Role', 'Childminder');
      await expectSummaryRowAbsent(targetPage, 'Local authority');
      await confirmCheckYourAnswers(targetPage);
    });

    test('childminder assistant role is available and completes', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'wales-childminder-assistant',
        firstName: 'Casey',
        surname: 'Assist',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'Wales');
      await chooseSettingByLabelAndContinue(targetPage, 'Childminder as part of an agency');
      await chooseRoleAndContinue(targetPage, 'Childminder assistant');
      await chooseExperienceAndContinue(targetPage, 'Not applicable');
      await completePreferences(targetPage, { emails: 'no', research: 'No' });

      await expectSummaryRow(targetPage, 'Setting type', 'Childminder as part of an agency');
      await expectSummaryRow(targetPage, 'Role', 'Childminder assistant');
      // Experience "Not applicable" is omitted from CYA when equal to N/A.
      await expectSummaryRowAbsent(targetPage, 'Local authority');
      await confirmCheckYourAnswers(targetPage);
    });

    test('no-role setting skips role and experience', async ({ targetPage, urls }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'ni-dfe',
        firstName: 'Drew',
        surname: 'Skip',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'Northern Ireland');
      await chooseSettingByLabelAndContinue(targetPage, 'Department for Education');
      await expectPath(targetPage, '/registration/training-emails/edit');
      await completePreferences(targetPage, { emails: 'yes', research: 'No' });

      await expectSummaryRow(targetPage, 'Setting type', 'Department for Education');
      await expectSummaryRowAbsent(targetPage, 'Local authority');
      await expectSummaryRowAbsent(targetPage, 'Role');
      await confirmCheckYourAnswers(targetPage);
    });

    test('early-years setting outside England asks role but not LA', async ({
      targetPage,
      urls,
    }) => {
      await startFreshRegistration(targetPage, urls.target, {
        label: 'outside-uk-nursery',
        firstName: 'Eden',
        surname: 'Abroad',
      });
      await chooseWhereYouLiveAndContinue(targetPage, 'Outside the UK');
      await chooseSettingByLabelAndContinue(targetPage, 'Private nursery');
      await expectPath(targetPage, '/registration/role-type/edit');
      await chooseRoleAndContinue(targetPage, 'Manager or team leader');
      await chooseExperienceAndContinue(targetPage, 'Between 6 and 9 years');
      await completePreferences(targetPage, { emails: 'yes', research: 'Yes' });

      await expectSummaryRow(targetPage, 'Setting type', 'Private nursery');
      await expectSummaryRow(targetPage, 'Role', 'Manager or team leader');
      await expectSummaryRowAbsent(targetPage, 'Local authority');
      await confirmCheckYourAnswers(targetPage);
    });
  });
});
