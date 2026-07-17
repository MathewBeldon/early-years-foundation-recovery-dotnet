import { expect, test } from '../support/fixtures.js';
import { registerMinimalUser } from '../support/learning.js';
import {
  changeToCustomSettingFromAccount,
  chooseResearchParticipantAndSave,
  chooseTrainingEmailsAndSave,
  expectDetailsUpdatedFlash,
  expectEmailPreferenceOnAccount,
  expectNameValidationErrors,
  expectResearchPreferenceOnAccount,
  expectScotlandAccountLocation,
  fillNameFields,
  openChangeEmailPreferencesFromAccount,
  openChangeNameFromAccount,
  openChangeResearchPreferencesFromAccount,
  saveNameFromAccount,
  visitAccountAfterWhereYouLiveChange,
  visitMyAccount,
} from '../support/account-preferences.js';

/**
 * My-account preference change journeys.
 * Target-agnostic: TARGET_BASE_URL + One Login simulator only.
 * Mirrors spec/system/changing_name_spec.rb and spec/system/account_page_spec.rb.
 */
test.describe('Account preferences journey', () => {
  test.describe.configure({ timeout: 240_000 });

  test('changes name with valid input and returns to my-account', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'acct-pref-name-valid',
      firstName: 'Taylor',
      surname: 'Original',
    });

    await visitMyAccount(targetPage, urls.target);
    await openChangeNameFromAccount(targetPage);
    await fillNameFields(targetPage, 'Foo', 'Bar');
    await saveNameFromAccount(targetPage);

    await expect(targetPage).toHaveURL(/\/my-account$/, { timeout: 30_000 });
    await expect(
      targetPage.getByRole('heading', { name: 'Manage your account' }),
    ).toBeVisible();
    await expect(targetPage.getByText('Foo Bar')).toBeVisible();
  });

  test('shows validation errors when name fields are empty', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'acct-pref-name-empty',
    });

    await visitMyAccount(targetPage, urls.target);
    await openChangeNameFromAccount(targetPage);
    await fillNameFields(targetPage, '', '');
    await saveNameFromAccount(targetPage);

    await expectNameValidationErrors(targetPage);
  });

  test('changes where you live from England to Scotland on my-account', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'acct-pref-scotland',
    });

    await visitMyAccount(targetPage, urls.target);
    await visitAccountAfterWhereYouLiveChange(targetPage, urls.target, 'Scotland');
    await expectScotlandAccountLocation(targetPage);
  });

  test('changes setting details to a custom organisation on my-account', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'acct-pref-custom-setting',
    });

    await visitMyAccount(targetPage, urls.target);
    await changeToCustomSettingFromAccount(targetPage, 'DfE Updated');

    await expect(
      targetPage.getByRole('heading', { name: 'Manage your account' }),
    ).toBeVisible();
    await expect(
      targetPage.locator('.govuk-summary-list__value', { hasText: 'DfE Updated' }),
    ).toBeVisible();
    await expectDetailsUpdatedFlash(targetPage);
  });

  test('toggles research preferences with updated details flash', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'acct-pref-research-toggle',
    });

    await visitMyAccount(targetPage, urls.target);
    await expectResearchPreferenceOnAccount(targetPage, 'No');

    await openChangeResearchPreferencesFromAccount(targetPage);
    await chooseResearchParticipantAndSave(targetPage, 'Yes');
    await expectResearchPreferenceOnAccount(targetPage, 'Yes');
    await expectDetailsUpdatedFlash(targetPage);

    await openChangeResearchPreferencesFromAccount(targetPage);
    await chooseResearchParticipantAndSave(targetPage, 'No');
    await expectResearchPreferenceOnAccount(targetPage, 'No');
    await expectDetailsUpdatedFlash(targetPage);
  });

  test('toggles email preferences copy on my-account', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'acct-pref-email-toggle',
    });

    await visitMyAccount(targetPage, urls.target);
    await expectEmailPreferenceOnAccount(targetPage, 'no');

    await openChangeEmailPreferencesFromAccount(targetPage);
    await chooseTrainingEmailsAndSave(targetPage, 'yes');
    await expectEmailPreferenceOnAccount(targetPage, 'yes');
    await expectDetailsUpdatedFlash(targetPage);

    await openChangeEmailPreferencesFromAccount(targetPage);
    await chooseTrainingEmailsAndSave(targetPage, 'no');
    await expectEmailPreferenceOnAccount(targetPage, 'no');
    await expectDetailsUpdatedFlash(targetPage);
  });
});
