import { test, expect } from '../support/fixtures.js';
import { screenshotOptions } from './screenshot-normalisation.js';

test('Rails reference screenshot is deterministic', async ({ railsPage, urls }) => {
  await railsPage.goto(`${urls.rails}/health`, { waitUntil: 'networkidle' });
  await expect(railsPage).toHaveScreenshot(
    'rails-health.png',
    screenshotOptions(railsPage),
  );
});

test('Rails reference home hero is pixel deterministic', async ({ railsPage, urls }) => {
  await railsPage.goto(`${urls.rails}/?pp=disable`, { waitUntil: 'networkidle' });
  const hero = railsPage.locator('main > .dfe-content-page--header');
  await expect(hero).toBeVisible();
  await expect(hero).toHaveScreenshot('rails-home-hero.png', screenshotOptions(railsPage));
});
