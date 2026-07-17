import { expect, test } from '../support/fixtures.js';
import {
  acceptAnalyticsCookiesFromBanner,
  expectCookieBanner,
  expectCookieBannerHidden,
  expectCookiePolicyPage,
  getAnalyticsCookieValue,
  openCookiePolicyFromBanner,
  rejectAnalyticsCookiesFromBanner,
  saveCookiePreference,
  setAnalyticsCookie,
  visitCookiePolicy,
  visitHomeAsFreshVisitor,
} from '../support/settings.js';

/**
 * Cookie banner and cookie policy settings journeys.
 * Mirrors spec/system/cookies_spec.rb — anonymous visitor, TARGET_BASE_URL only.
 */
test.describe('Settings and cookies journey', () => {
  test.describe('visitor without cookie preference', () => {
    test.beforeEach(async ({ targetPage, urls }) => {
      await visitHomeAsFreshVisitor(targetPage, urls.target);
    });

    test('displays cookie banner', async ({ targetPage }) => {
      await expectCookieBanner(targetPage);
    });

    test('visitor can click to read cookie policy', async ({ targetPage }) => {
      await expectCookieBanner(targetPage);
      await openCookiePolicyFromBanner(targetPage);
      await expectCookiePolicyPage(targetPage);
    });

    test('visitor can click to accept analytics cookies', async ({
      targetPage,
      urls,
    }) => {
      await expectCookieBanner(targetPage);
      await acceptAnalyticsCookiesFromBanner(targetPage);

      const cookieValue = await getAnalyticsCookieValue(
        targetPage.context(),
        urls.target,
      );
      expect(cookieValue).toBe('true');
      await expectCookieBannerHidden(targetPage);
    });

    test('visitor can click to reject analytics cookies', async ({
      targetPage,
      urls,
    }) => {
      await expectCookieBanner(targetPage);
      await rejectAnalyticsCookiesFromBanner(targetPage);

      const cookieValue = await getAnalyticsCookieValue(
        targetPage.context(),
        urls.target,
      );
      expect(cookieValue).toBe('false');
      await expectCookieBannerHidden(targetPage);
    });
  });

  test('visitor with rejected cookies can accept on cookie policy page', async ({
    targetPage,
    urls,
  }) => {
    await setAnalyticsCookie(targetPage, urls.target, 'false');
    await visitCookiePolicy(targetPage, urls.target);
    await expectCookiePolicyPage(targetPage);
    await saveCookiePreference(targetPage, true);

    const cookieValue = await getAnalyticsCookieValue(
      targetPage.context(),
      urls.target,
    );
    expect(cookieValue).toBe('true');
  });

  test('visitor with accepted cookies can reject on cookie policy page', async ({
    targetPage,
    urls,
  }) => {
    await setAnalyticsCookie(targetPage, urls.target, 'true');
    await visitCookiePolicy(targetPage, urls.target);
    await expectCookiePolicyPage(targetPage);
    await saveCookiePreference(targetPage, false);

    const cookieValue = await getAnalyticsCookieValue(
      targetPage.context(),
      urls.target,
    );
    expect(cookieValue).toBe('false');
  });
});
