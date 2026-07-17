import { newSimulatorUser, signInViaOneLogin } from '../support/auth.js';
import { expect, test } from '../support/fixtures.js';
import { registerMinimalUser } from '../support/learning.js';
import { expectPath } from '../support/registration.js';
import {
  expectAlreadySignedInRedirect,
  expectSignInExplainerContent,
  expectSignedOutState,
  signOutFromNavigation,
  visitSignIn,
} from '../support/sign-in.js';

/**
 * Sign-in / GOV.UK One Login surfaces.
 * Target-agnostic: TARGET_BASE_URL + One Login simulator only.
 * Source of truth: spec/system/gov_one_spec.rb
 */
test.describe('Sign-in journey', () => {
  test.describe.configure({ timeout: 240_000 });

  test('shows One Login explainer content and Continue to GOV.UK One Login', async ({
    targetPage,
    urls,
  }) => {
    await visitSignIn(targetPage, urls.target);
    await expectSignInExplainerContent(targetPage);
  });

  test('redirects authenticated registered users from sign-in with already signed in message', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'sign-in-registered' });

    await visitSignIn(targetPage, urls.target);
    await expectAlreadySignedInRedirect(targetPage);
  });

  test('sign-out from navigation returns to a signed-out state', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'sign-out-nav' });
    await expect(targetPage.getByRole('link', { name: 'Sign out' })).toBeVisible();

    await signOutFromNavigation(targetPage);
    await expectSignedOutState(targetPage, urls.target);
  });

  test('lands incomplete registration on terms after One Login', async ({
    targetPage,
    urls,
  }) => {
    const user = newSimulatorUser('sign-in-incomplete');
    await signInViaOneLogin(targetPage, user, urls.target);
    await expectPath(targetPage, '/registration/terms-and-conditions/edit');
  });
});
