import { test } from '../support/fixtures.js';
import {
  closeTranscript,
  expectAlphaVideoPage,
  openTranscript,
  walkToAlphaVideoPage,
} from '../support/content-pages.js';
import { registerMinimalUser } from '../support/learning.js';

/**
 * Video / content page behaviours on the alpha training path.
 * Target-agnostic: TARGET_BASE_URL + One Login simulator only.
 */
test.describe('Content pages journey', () => {
  test.describe.configure({ timeout: 300_000 });

  test('renders alpha video page with iframe and transcript toggle', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, { label: 'content-video' });
    await walkToAlphaVideoPage(targetPage);

    await expectAlphaVideoPage(targetPage);
    await openTranscript(targetPage);
    await closeTranscript(targetPage);
  });
});
