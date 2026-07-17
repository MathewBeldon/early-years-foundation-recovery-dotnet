import { expect, test } from '../support/fixtures.js';
import { registerMinimalUser } from '../support/learning.js';
import {
  expectEmptyLearningLogModule,
  expectNoteBodyOnLearningLog,
  registerAndAssertLearningLogNavVisibility,
  revisitAlphaNotePage,
  saveNoteOnCurrentPage,
  startAlphaForLearningLog,
  visitLearningLog,
  walkToAlphaNotePage,
} from '../support/learning-log.js';

/**
 * Learning log journeys: account notes list and alpha reflection saves.
 * Target-agnostic: TARGET_BASE_URL + One Login simulator only.
 */
test.describe('Learning log journey', () => {
  test.describe.configure({ timeout: 240_000 });

  test('redirects unauthenticated visitors to sign-in', async ({
    targetPage,
    urls,
  }) => {
    await visitLearningLog(targetPage, urls.target);
    await expect(targetPage).toHaveURL(/\/users\/sign-in/);
    await expect(
      targetPage.getByRole('heading', { name: 'How to access this training course' }),
    ).toBeVisible();
  });

  test('hides Learning log nav before module start and shows it after', async ({
    targetPage,
    urls,
  }) => {
    await registerAndAssertLearningLogNavVisibility(
      targetPage,
      urls.target,
      'learning-log-nav',
    );
  });

  test('shows empty module messaging for a registered user with no notes', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'learning-log-empty' });
    await startAlphaForLearningLog(targetPage);

    await visitLearningLog(targetPage, urls.target);
    await expectEmptyLearningLogModule(targetPage);
  });

  test('lists a note saved on the alpha reflection page', async ({
    targetPage,
    urls,
  }) => {
    const noteBody = 'hello world';

    await registerMinimalUser(targetPage, urls.target, { label: 'learning-log-save' });
    await walkToAlphaNotePage(targetPage);
    await saveNoteOnCurrentPage(targetPage, noteBody);

    await visitLearningLog(targetPage, urls.target);
    await expectNoteBodyOnLearningLog(targetPage, noteBody);
    await expect(
      targetPage.getByRole('link', { name: '1-1-3-1' }),
    ).toBeVisible();
  });

  test('updates an existing note from the content page', async ({
    targetPage,
    urls,
  }) => {
    const initialBody = 'hello world';
    const updatedBody = 'updated reflection note';

    await registerMinimalUser(targetPage, urls.target, { label: 'learning-log-update' });
    await walkToAlphaNotePage(targetPage);
    await saveNoteOnCurrentPage(targetPage, initialBody);

    await visitLearningLog(targetPage, urls.target);
    await expectNoteBodyOnLearningLog(targetPage, initialBody);

    await revisitAlphaNotePage(targetPage, urls.target);
    await saveNoteOnCurrentPage(targetPage, updatedBody);

    await visitLearningLog(targetPage, urls.target);
    await expectNoteBodyOnLearningLog(targetPage, updatedBody);
    await expect(
      targetPage.locator('.log-entry').getByText(initialBody, { exact: true }),
    ).toHaveCount(0);
  });
});
