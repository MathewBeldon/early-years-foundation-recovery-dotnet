import { test } from '../support/fixtures.js';
import {
  DEFAULT_CERTIFICATE_USER,
  expectCertificateCompletionDateIfPresent,
  expectCertificateOmitsUserName,
  expectCompleteCertificateWithName,
  expectIncompleteCertificateGuard,
  visitCertificatePage,
} from '../support/certificate.js';
import {
  openModuleFromCard,
  registerMinimalUser,
  startModule,
} from '../support/learning.js';
import {
  ALPHA_PASS_SKIP_FEEDBACK,
  expectCertificatePage,
  walkModuleSteps,
} from '../support/module-journey.js';

/**
 * Certificate guards for incomplete vs completed modules.
 * Target-agnostic: TARGET_BASE_URL + One Login simulator only.
 */
test.describe('Certificate journey', () => {
  test.describe.configure({ timeout: 240_000 });

  test('shows incomplete certificate guard for bravo before module completion', async ({
    targetPage,
    urls,
  }) => {
    await registerMinimalUser(targetPage, urls.target, {
      label: 'cert-incomplete',
      ...DEFAULT_CERTIFICATE_USER,
    });

    await visitCertificatePage(targetPage, urls.target, 'bravo');
    await expectIncompleteCertificateGuard(targetPage);
    await expectCertificateOmitsUserName(
      targetPage,
      DEFAULT_CERTIFICATE_USER.firstName,
      DEFAULT_CERTIFICATE_USER.surname,
    );
  });

  test.describe('completed certificate', () => {
    test.describe.configure({ timeout: 600_000 });

    test('shows congratulations and learner name after completing alpha', async ({
      targetPage,
      urls,
    }) => {
      await registerMinimalUser(targetPage, urls.target, {
        label: 'cert-complete',
        ...DEFAULT_CERTIFICATE_USER,
      });
      await openModuleFromCard(targetPage, /First Training Module/);
      await startModule(targetPage);

      await walkModuleSteps(targetPage, ALPHA_PASS_SKIP_FEEDBACK);
      await expectCertificatePage(targetPage);
      await expectCompleteCertificateWithName(targetPage, DEFAULT_CERTIFICATE_USER);
      await expectCertificateCompletionDateIfPresent(targetPage);
    });
  });
});
