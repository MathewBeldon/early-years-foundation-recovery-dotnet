import type { Browser, Page } from '@playwright/test';
import { newSimulatorUser, signInViaOneLogin } from '../support/auth.js';
import type { TargetUrls } from '../support/environment.js';
import { expect, test } from '../support/fixtures.js';
import { guardJourneyOrigins } from '../support/origin-guard.js';
import {
  acceptTermsAndContinue,
  expectPath,
  fillNameAndContinue,
} from '../support/registration.js';

interface ConcurrentUser {
  firstName: string;
  page: Page;
  surname: string;
}

async function openConcurrentUsers(
  browser: Browser,
  urls: TargetUrls,
  cohort: string,
): Promise<{ close: () => Promise<void>; users: ConcurrentUser[] }> {
  const [firstContext, secondContext] = await Promise.all([
    browser.newContext({ serviceWorkers: 'block' }),
    browser.newContext({ serviceWorkers: 'block' }),
  ]);
  const contexts = [firstContext, secondContext];
  const [firstPage, secondPage] = await Promise.all([
    firstContext.newPage(),
    secondContext.newPage(),
  ]);
  const pages = [firstPage, secondPage];
  const assertNoOriginEscapes = pages.map((page) => guardJourneyOrigins(page, urls));
  const users = [
    { firstName: `Alex ${cohort}`, surname: 'Concurrent', page: firstPage },
    { firstName: `Blair ${cohort}`, surname: 'Parallel', page: secondPage },
  ];

  return {
    users,
    close: async () => {
      try {
        for (const assertNoOriginEscape of assertNoOriginEscapes) {
          assertNoOriginEscape();
        }
      } finally {
        await Promise.all(contexts.map((context) => context.close()));
      }
    },
  };
}

async function proveIdentityAndSessionIsolation(
  browser: Browser,
  urls: TargetUrls,
  cohort: string,
): Promise<void> {
  const concurrent = await openConcurrentUsers(browser, urls, cohort);

  try {
    await Promise.all(
      concurrent.users.map(async ({ page }, index) => {
        const identity = newSimulatorUser(`concurrency-${cohort}-${index + 1}`);
        await signInViaOneLogin(page, identity, urls.target);
        await acceptTermsAndContinue(page);
      }),
    );

    await Promise.all(
      concurrent.users.map(async ({ firstName, page, surname }) => {
        await fillNameAndContinue(page, firstName, surname);
        // Do not revisit the name form until Rails has committed the update and
        // completed the POST redirect; click completion alone is not that boundary.
        await expectPath(page, '/registration/where-you-live/edit');
      }),
    );

    await Promise.all(
      concurrent.users.map(({ page }) =>
        page.goto(`${urls.target}/registration/name/edit`),
      ),
    );

    for (const [index, { firstName, page, surname }] of concurrent.users.entries()) {
      await expectPath(page, '/registration/name/edit');
      await expect(page.getByLabel('First name')).toHaveValue(firstName);
      await expect(page.getByLabel('Surname')).toHaveValue(surname);

      const other = concurrent.users[1 - index];
      if (!other) throw new Error(`Missing concurrent counterpart for user ${index}.`);
      await expect(page.getByLabel('First name')).not.toHaveValue(other.firstName);
      await expect(page.getByLabel('Surname')).not.toHaveValue(other.surname);
    }
  } finally {
    await concurrent.close();
  }
}

test.describe('Interactive One Login concurrency', () => {
  test.describe.configure({ mode: 'parallel', timeout: 240_000 });

  for (const cohort of ['one', 'two']) {
    test(`keeps simultaneous identities and sessions isolated (${cohort})`, async ({
      browser,
      urls,
    }) => {
      await proveIdentityAndSessionIsolation(browser, urls, cohort);
    });
  }
});
