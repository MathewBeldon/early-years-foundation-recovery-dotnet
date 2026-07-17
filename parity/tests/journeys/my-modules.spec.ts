import { newSimulatorUser, signInViaOneLogin } from '../support/auth.js';
import { expect, test } from '../support/fixtures.js';
import {
  continueFromInterruption,
  expectAvailableModules,
  expectEmptyInProgress,
  expectInProgressModule,
  expectModuleOverview,
  expectMyModulesPage,
  expectUpcomingModule,
  openModuleFromCard,
  registerMinimalUser,
  startModule,
} from '../support/learning.js';
import { expectPath } from '../support/registration.js';

/**
 * Post-registration My modules / module entry journeys.
 * Target-agnostic: TARGET_BASE_URL + One Login simulator only.
 */
test.describe('My modules journey', () => {
  test.describe.configure({ timeout: 240_000 });

  test('redirects unauthenticated visitors to sign-in', async ({
    targetPage,
    urls,
  }) => {
    await targetPage.goto(`${urls.target}/my-modules`);
    await expect(targetPage).toHaveURL(/\/users\/sign-in/);
    await expect(
      targetPage.getByRole('heading', { name: 'How to access this training course' }),
    ).toBeVisible();
  });

  test('redirects incomplete registration to terms and conditions', async ({
    targetPage,
    urls,
  }) => {
    const user = newSimulatorUser('incomplete-reg');
    await signInViaOneLogin(targetPage, user, urls.target);
    await expectPath(targetPage, '/registration/terms-and-conditions/edit');

    await targetPage.goto(`${urls.target}/my-modules`);
    await expectPath(targetPage, '/registration/terms-and-conditions/edit');
  });

  test('shows empty in-progress, available, and upcoming modules after registration', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'my-modules-empty' });
    await expectMyModulesPage(targetPage);
    await expect(
      targetPage.getByText('You can now start your first module.'),
    ).toBeVisible();

    await expectEmptyInProgress(targetPage);
    await expectAvailableModules(targetPage, [
      'First Training Module',
      'Second Training Module',
      'Third Training Module',
    ]);
    await expectUpcomingModule(targetPage, 'Fourth Training Module');
    await expect(targetPage.locator('#completed')).toHaveCount(0);
  });

  test('opens module overview and starts through interruption to first section', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'start-module' });
    await openModuleFromCard(targetPage, /First Training Module/);
    await expectModuleOverview(targetPage);

    await targetPage.getByRole('link', { name: 'Back to My modules' }).click();
    await expectMyModulesPage(targetPage);

    await openModuleFromCard(targetPage, /First Training Module/);
    await startModule(targetPage);
    await continueFromInterruption(targetPage);
  });

  test('moves a started module into in-progress with resume', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'in-progress' });
    await openModuleFromCard(targetPage, /First Training Module/);
    await startModule(targetPage);
    await continueFromInterruption(targetPage);

    await targetPage.goto(`${urls.target}/my-modules`);
    await expectInProgressModule(targetPage, 'First Training Module');

    const started = targetPage.locator('#started');
    // After interruption + first section intro: at least one page counted.
    await expect(started.getByText(/You have read \d+ pages/)).toBeVisible();
    await expect(started.getByText(/Your progress: \d+%/)).toBeVisible();

    await openModuleFromCard(targetPage, /First Training Module|Module 1:/);
    const resume = targetPage.getByRole('link', { name: 'Resume module' }).first();
    await expect(resume).toBeVisible({ timeout: 30_000 });
    await Promise.all([
      targetPage.waitForURL(/\/modules\/alpha\/content-pages\//, {
        timeout: 30_000,
      }),
      resume.click(),
    ]);
  });

  test('redirects draft modules back to My modules', async ({ targetPage, urls }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'draft-module' });
    await targetPage.goto(`${urls.target}/modules/delta`);
    await expectMyModulesPage(targetPage);
  });
});
