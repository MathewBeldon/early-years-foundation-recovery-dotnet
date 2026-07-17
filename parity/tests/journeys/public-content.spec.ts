import { expect, test } from '../support/fixtures.js';
import { registerMinimalUser } from '../support/learning.js';
import {
  expectAboutAlphaHero,
  expectGuestStaticPageLoaded,
  expectWhatsNewPage,
  GUEST_STATIC_PAGES,
  visitAboutAlpha,
  visitGuestStaticPage,
  visitWhatsNew,
} from '../support/public-content.js';

/**
 * Public module about page, guest static CMS pages, and authenticated whats-new.
 * Target-agnostic: TARGET_BASE_URL (+ One Login simulator for whats-new only).
 *
 * Assumptions:
 * - Contentful test seed provides static page headings and synthetic body copy.
 * - Guest static routes are anonymous; whats-new requires a registered user session.
 * - display_whats_new one-time redirect after sign-in is not exercised here
 *   (registerMinimalUser leaves the flag false); direct /whats-new navigation only.
 */
test.describe('Public content journey', () => {
  test.describe.configure({ timeout: 120_000 });

  test('about alpha page shows first module hero content', async ({ targetPage, urls }) => {
    await visitAboutAlpha(targetPage, urls.target);
    await expectAboutAlphaHero(targetPage);
  });

  for (const { path, heading } of GUEST_STATIC_PAGES) {
    test(`${path} returns 200 with exact title, heading, and body`, async ({
      targetPage,
      urls,
    }) => {
      const status = await visitGuestStaticPage(targetPage, urls.target, path);
      await expectGuestStaticPageLoaded(targetPage, path, heading, status);
    });
  }

  test('authenticated whats-new page loads expected content', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'whats-new' });
    const status = await visitWhatsNew(targetPage, urls.target);
    expect(status).toBe(200);
    await expectWhatsNewPage(targetPage, status);
  });
});
