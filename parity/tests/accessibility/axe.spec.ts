import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '../support/fixtures.js';

test('accessibility scanner accepts a deterministic clean control page', async ({ railsPage, urls }) => {
  await railsPage.goto(`${urls.sideEffectSink}/contract-target`, { waitUntil: 'networkidle' });
  const result = await new AxeBuilder({ page: railsPage }).analyze();

  expect(result.violations).toEqual([]);
});

test('Rails reference home page has no WCAG or new axe violations', async ({ railsPage, urls }, testInfo) => {
  await railsPage.goto(`${urls.rails}/?pp=disable`, { waitUntil: 'networkidle' });
  const result = await new AxeBuilder({ page: railsPage }).analyze();

  await testInfo.attach('rails-home-axe-results', {
    body: Buffer.from(JSON.stringify(result, null, 2)),
    contentType: 'application/json',
  });

  const wcagViolations = result.violations.filter((violation) =>
    violation.tags.some((tag) => tag.startsWith('wcag')),
  );
  expect(wcagViolations).toEqual([]);

  // Rails development diagnostics introduce heading-order noise, while the
  // existing skip link triggers axe's non-WCAG region best-practice rule.
  // Preserve both in the attachment, fail every WCAG rule, and reject any new
  // best-practice category rather than silently excluding all best practices.
  const knownRailsBestPractices = new Set(['heading-order', 'region']);
  const unexpectedBestPractices = result.violations.filter(
    (violation) => !knownRailsBestPractices.has(violation.id),
  );
  expect(unexpectedBestPractices).toEqual([]);
});

test('accessibility scanner detects a deterministic regression', async ({ railsPage, urls }, testInfo) => {
  await railsPage.goto(`${urls.sideEffectSink}/contract-inaccessible`, {
    waitUntil: 'networkidle',
  });
  const result = await new AxeBuilder({ page: railsPage }).analyze();

  await testInfo.attach('axe-negative-control-results', {
    body: Buffer.from(JSON.stringify(result, null, 2)),
    contentType: 'application/json',
  });
  expect(result.violations.map((violation) => violation.id)).toContain('image-alt');
});
