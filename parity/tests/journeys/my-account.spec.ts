import { expect, test } from '../support/fixtures.js';
import {
  chooseCloseReasonAndContinue,
  confirmCloseAccount,
  expectAccountClosed,
  expectCloseAccountConfirmPage,
  expectCloseReasonValidation,
  expectMyAccountPage,
  expectRegisteredAccountDetails,
  expectSignedOutFromMyAccount,
  openCloseAccountReason,
  submitCloseReasonWithoutSelection,
  visitMyAccount,
} from '../support/account.js';
import { registerMinimalUser } from '../support/learning.js';

/**
 * My account and close-account journeys.
 * Target-agnostic: TARGET_BASE_URL + One Login simulator only.
 */
test.describe('My account journey', () => {
  test.describe.configure({ timeout: 240_000 });

  test('redirects unauthenticated visitors to sign-in', async ({
    targetPage,
    urls,
  }) => {
    await visitMyAccount(targetPage, urls.target);
    await expect(targetPage).toHaveURL(/\/users\/sign-in/);
    await expect(
      targetPage.getByRole('heading', { name: 'How to access this training course' }),
    ).toBeVisible();
  });

  test('shows registration name and account details after minimal registration', async ({
    targetPage,
    urls,
  }) => {
    const firstName = 'Morgan';
    const surname = 'Account';

    await registerMinimalUser(targetPage, urls.target, {
      label: 'my-account-details',
      firstName,
      surname,
    });

    await visitMyAccount(targetPage, urls.target);
    await expectRegisteredAccountDetails(targetPage, {
      fullName: `${firstName} ${surname}`,
    });
  });
});

test.describe('Close account journey', () => {
  test.describe.configure({ timeout: 240_000 });

  test('closes account through reason, confirmation, and sign-out', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'close-account-happy' });

    await visitMyAccount(targetPage, urls.target);
    await expectMyAccountPage(targetPage);
    await openCloseAccountReason(targetPage);

    await chooseCloseReasonAndContinue(
      targetPage,
      'I did not find the training useful',
    );
    await expectCloseAccountConfirmPage(targetPage);

    await confirmCloseAccount(targetPage);
    await expectAccountClosed(targetPage);
    await expectSignedOutFromMyAccount(targetPage, urls.target);
  });

  test('requires a close reason before continuing', async ({ targetPage, urls }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'close-account-validation',
    });

    await visitMyAccount(targetPage, urls.target);
    await openCloseAccountReason(targetPage);

    await submitCloseReasonWithoutSelection(targetPage);
    await expectCloseReasonValidation(targetPage);
  });

  test('continues to confirmation when Another reason is selected', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'close-account-another-reason',
    });

    await visitMyAccount(targetPage, urls.target);
    await openCloseAccountReason(targetPage);
    await chooseCloseReasonAndContinue(targetPage, 'Another reason');
    await expectCloseAccountConfirmPage(targetPage);
  });
});
