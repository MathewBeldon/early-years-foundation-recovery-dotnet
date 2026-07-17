import fs from 'node:fs';
import path from 'node:path';
import type { Locator, Page, PageAssertionsToHaveScreenshotOptions } from '@playwright/test';

interface DynamicRegionConfiguration {
  selectors: Array<{ selector: string; justification: string }>;
}

export function screenshotOptions(page: Page): PageAssertionsToHaveScreenshotOptions {
  const configurationPath = path.resolve('fixtures/dynamic-regions.json');
  const configuration = JSON.parse(
    fs.readFileSync(configurationPath, 'utf8'),
  ) as DynamicRegionConfiguration;
  const masks: Locator[] = configuration.selectors.map(({ selector, justification }) => {
    if (!justification.trim()) {
      throw new Error(`Dynamic screenshot mask ${selector} has no justification.`);
    }

    return page.locator(selector);
  });

  return {
    animations: 'disabled',
    caret: 'hide',
    mask: masks,
    maxDiffPixels: 0,
    threshold: 0,
  };
}
