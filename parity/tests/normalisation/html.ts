import { maskDynamicValues } from './dynamic-values.js';

export function normaliseHtml(html: string): string {
  return maskDynamicValues(html)
    .replace(/\r\n/g, '\n')
    .replace(/[ \t]+$/gm, '')
    .trim();
}
