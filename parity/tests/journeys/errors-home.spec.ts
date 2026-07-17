import { test } from '../support/fixtures.js';
import {
  expectAuthenticatedHomePage,
  expectInternalServerErrorPage,
  expectServiceUnavailablePage,
  visitHome,
  visitInternalServerError,
  visitServiceUnavailable,
} from '../support/errors-home.js';
import { registerMinimalUser } from '../support/learning.js';

/**
 * Error pages and authenticated home variant.
 * Target-agnostic: TARGET_BASE_URL (+ One Login simulator for registered user home).
 * Source of truth: spec/system/errors_spec.rb, spec/system/front_page_spec.rb
 */
test.describe('Errors and authenticated home journey', () => {
  test.describe.configure({ timeout: 240_000 });

  test('500 page shows expected messaging', async ({ targetPage, urls }) => {
    const status = await visitInternalServerError(targetPage, urls.target);
    await expectInternalServerErrorPage(targetPage, status);
  });

  test('503 page shows expected messaging', async ({ targetPage, urls }) => {
    const status = await visitServiceUnavailable(targetPage, urls.target);
    await expectServiceUnavailablePage(targetPage, status);
  });

  test('authenticated registered user sees signed-in home variant', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'errors-home' });
    await visitHome(targetPage, urls.target);
    await expectAuthenticatedHomePage(targetPage);
  });
});
