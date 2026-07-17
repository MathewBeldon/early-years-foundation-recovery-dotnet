import { expect, test } from '../support/fixtures.js';
import {
  expectAccessibilityStatementPage,
  expectAnonymousHomePage,
  expectCourseOverviewPage,
  expectExpertsPage,
  expectNotFoundPage,
  followLearnMoreAboutTraining,
  followRegisterOrSignIn,
  visitHome,
} from '../support/static-pages.js';

/**
 * Public informational surfaces: home, about, footer static pages, and errors.
 * Target-agnostic: TARGET_BASE_URL only — anonymous browsing, no One Login.
 */
test.describe('Static pages journey', () => {
  test.describe.configure({ timeout: 120_000 });

  test('anonymous home page shows key content and primary CTAs', async ({
    targetPage,
    urls,
  }) => {
    await visitHome(targetPage, urls.target);
    await expectAnonymousHomePage(targetPage);

    await followRegisterOrSignIn(targetPage);
    await expect(targetPage).toHaveURL(/\/users\/sign-in/);
    await expect(
      targetPage.getByRole('heading', { name: 'How to access this training course' }),
    ).toBeVisible();

    await visitHome(targetPage, urls.target);
    await followLearnMoreAboutTraining(targetPage);
    await expectCourseOverviewPage(targetPage);
  });

  test('about training course overview shows expected headings', async ({
    targetPage,
    urls,
  }) => {
    await targetPage.goto(`${urls.target}/about-training`);
    await expectCourseOverviewPage(targetPage);
  });

  test('experts page shows expected headings', async ({ targetPage, urls }) => {
    await targetPage.goto(`${urls.target}/about/the-experts`);
    await expectExpertsPage(targetPage);
  });

  test('accessibility statement footer page loads', async ({ targetPage, urls }) => {
    await targetPage.goto(`${urls.target}/accessibility-statement`);
    await expectAccessibilityStatementPage(targetPage);
  });

  test('404 page shows expected messaging', async ({ targetPage, urls }) => {
    await targetPage.goto(`${urls.target}/404`);
    await expectNotFoundPage(targetPage);
  });
});
