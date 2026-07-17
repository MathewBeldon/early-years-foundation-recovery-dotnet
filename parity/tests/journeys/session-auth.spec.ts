import { newSimulatorUser, signInViaOneLogin } from '../support/auth.js';
import { seedRegisteredAccountWithoutOneLogin } from '../support/database-preconditions.js';
import { expect, test } from '../support/fixtures.js';
import {
  chooseSettingByLabelAndContinue,
  chooseWhereYouLiveAndContinue,
  completePreferences,
  confirmCheckYourAnswers,
  acceptTermsAndContinue,
  fillNameAndContinue,
  expectPath,
} from '../support/registration.js';
import { expectMyModulesPage } from '../support/learning.js';
import { expectSignedOutState } from '../support/sign-in.js';

async function completeMinimalRegistration(
  page: Parameters<typeof signInViaOneLogin>[0],
  user: ReturnType<typeof newSimulatorUser>,
  baseUrl: string,
  name: { firstName: string; surname: string },
): Promise<void> {
  await signInViaOneLogin(page, user, baseUrl);
  await acceptTermsAndContinue(page);
  await fillNameAndContinue(page, name.firstName, name.surname);
  await chooseWhereYouLiveAndContinue(page, 'England');
  await chooseSettingByLabelAndContinue(page, 'Department for Education');
  await completePreferences(page, { emails: 'no', research: 'No' });
  await confirmCheckYourAnswers(page);
}

test.describe('Rails authentication and session characterization', () => {
  test.describe.configure({ timeout: 240_000 });

  test('returns the same registered application account for the same One Login sub', async ({
    targetPage,
    urls,
  }) => {
    const identity = newSimulatorUser('returning-sub');
    const name = { firstName: 'Robin', surname: 'Returning' };
    await completeMinimalRegistration(targetPage, identity, urls.target, name);

    await targetPage.getByRole('link', { name: 'Sign out' }).click();
    await expectSignedOutState(targetPage, urls.target);
    await signInViaOneLogin(targetPage, identity, urls.target);
    await expectMyModulesPage(targetPage);

    await targetPage.goto(`${urls.target}/my-account`);
    await expect(targetPage.getByText(`${name.firstName} ${name.surname}`)).toBeVisible();
  });

  test('links a matching legacy email to the authenticated One Login sub', async ({
    targetPage,
    urls,
  }) => {
    const identity = newSimulatorUser('email-link');
    await seedRegisteredAccountWithoutOneLogin(identity, {
      firstName: 'Legacy',
      surname: 'Learner',
    });

    await signInViaOneLogin(targetPage, identity, urls.target);
    await expectMyModulesPage(targetPage);
    await targetPage.goto(`${urls.target}/my-account`);
    await expect(targetPage.getByText('Legacy Learner')).toBeVisible();
  });

  test('handles a provider cancellation without authenticating', async ({
    targetPage,
    urls,
  }) => {
    await targetPage.goto(`${urls.target}/users/sign-in`);
    const authorizeLink = targetPage.getByRole('link', {
      name: 'Continue to GOV.UK One Login',
    });
    const authorizeUrl = new URL((await authorizeLink.getAttribute('href')) ?? '');
    const state = authorizeUrl.searchParams.get('state');
    expect(state).toBeTruthy();

    const callback = new URL('/users/auth/openid_connect/callback', urls.target);
    callback.searchParams.set('error', 'access_denied');
    callback.searchParams.set('error_description', 'The user cancelled sign in');
    callback.searchParams.set('state', state ?? '');
    await targetPage.goto(callback.toString());

    await expectPath(targetPage, '/');
    await expect(
      targetPage.getByText('There was a problem signing in. Please try again.'),
    ).toBeVisible();
    await targetPage.goto(`${urls.target}/my-modules`);
    await expect(targetPage).toHaveURL(/\/users\/sign-in/);
  });

  test('sends the required RP-initiated logout parameters and completes local logout', async ({
    targetPage,
    urls,
  }) => {
    const identity = newSimulatorUser('rp-logout');
    await completeMinimalRegistration(targetPage, identity, urls.target, {
      firstName: 'Lou',
      surname: 'Logout',
    });

    const logoutRequestPromise = targetPage.waitForRequest((request) => {
      const url = new URL(request.url());
      return url.origin === new URL(urls.govOneSimulator).origin && url.pathname === '/logout';
    });
    await targetPage.getByRole('link', { name: 'Sign out' }).click();
    const logoutUrl = new URL((await logoutRequestPromise).url());

    expect(logoutUrl.searchParams.get('id_token_hint')).toBeTruthy();
    expect(logoutUrl.searchParams.get('state')).toBeTruthy();
    expect(logoutUrl.searchParams.get('post_logout_redirect_uri')).toBe(
      `${urls.target}/users/sign_out`,
    );
    await expectSignedOutState(targetPage, urls.target);
  });
});
