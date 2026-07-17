import { expect, test } from '../support/fixtures.js';
import {
  openModuleFromCard,
  registerMinimalUser,
  startModule,
} from '../support/learning.js';
import {
  ALPHA_FAIL,
  ALPHA_PASS_SKIP_FEEDBACK,
  clickTrainingAction,
  expectCertificatePage,
  expectCompletedModuleCard,
  expectFailedAssessment,
  walkModuleSteps,
} from '../support/module-journey.js';

/**
 * Full alpha module completion: content → formative → summative → confidence → certificate.
 * Canonical walk mirrors Rails AST YAML (spec/support/ast/alpha-*-response*.yml).
 */
test.describe('Module completion journey', () => {
  test.describe.configure({ timeout: 600_000 });

  test('passes summative assessment, skips feedback, and completes with certificate', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'module-pass',
    });
    await openModuleFromCard(targetPage, /First Training Module/);
    await startModule(targetPage);

    await walkModuleSteps(targetPage, ALPHA_PASS_SKIP_FEEDBACK);
    await expectCertificatePage(targetPage);

    await targetPage.getByRole('link', { name: 'Go to my modules' }).click();
    await expectCompletedModuleCard(targetPage, 'First Training Module');
  });

  test('fails summative assessment and offers retake without certificate', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'module-fail',
    });
    await openModuleFromCard(targetPage, /First Training Module/);
    await startModule(targetPage);

    await walkModuleSteps(targetPage, ALPHA_FAIL);
    await expectFailedAssessment(targetPage);
    await expect(targetPage.getByText('You scored 0%')).toBeVisible();

    await expect(
      targetPage.getByRole('link', { name: 'View certificate' }),
    ).toHaveCount(0);

    await targetPage.getByRole('link', { name: 'Retake test' }).click();
    await expect(targetPage).toHaveURL(/\/modules\/alpha\/content-pages\/1-3-2/);
    await expect(
      targetPage.getByRole('heading', { name: 'End of module test' }),
    ).toBeVisible();
    // Visible label "Start test"; accessible name is "Next Page".
    await clickTrainingAction(targetPage, 'Start test');
    await expect(targetPage).toHaveURL(
      /\/modules\/alpha\/questionnaires\/1-3-2-1/,
    );
    await expect(
      targetPage.locator('.govuk-checkboxes__input:disabled'),
    ).toHaveCount(0);

    await targetPage.goto(`${urls.target}/my-modules`);
    await expect(targetPage.locator('#completed')).toHaveCount(0);
    await expect(
      targetPage.locator('#started').getByText('First Training Module'),
    ).toBeVisible();
  });
});
