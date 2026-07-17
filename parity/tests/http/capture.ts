import type { APIRequestContext, APIResponse, Response } from '@playwright/test';
import { normaliseHtml } from '../normalisation/html.js';

type FetchOptions = NonNullable<Parameters<APIRequestContext['fetch']>[1]>;

export type HttpCaptureRequest = Omit<FetchOptions, 'maxRedirects'>;

const selectedHeaders = new Set([
  'cache-control',
  'content-security-policy',
  'content-language',
  'content-type',
  'location',
  'permissions-policy',
  'referrer-policy',
  'set-cookie',
  'strict-transport-security',
  'x-content-type-options',
  'x-frame-options',
]);

export interface HttpCapture {
  method: string;
  url: string;
  status: number;
  headers: Record<string, string[]>;
  cookies: string[];
  redirect: string | null;
  html: string;
}

async function captureResponse(
  response: APIResponse | Response,
  method: string,
): Promise<HttpCapture> {
  const headers: Record<string, string[]> = {};

  for (const header of await response.headersArray()) {
    const name = header.name.toLowerCase();
    if (selectedHeaders.has(name)) {
      (headers[name] ??= []).push(header.value);
    }
  }

  // Browser engines do not expose redirect bodies. They are not part of the
  // selected contract; status and Location are captured explicitly.
  const html = response.status() >= 300 && response.status() < 400
    ? ''
    : normaliseHtml(await response.text());

  return {
    method,
    url: response.url(),
    status: response.status(),
    headers,
    cookies: headers['set-cookie'] ?? [],
    redirect: headers.location?.[0] ?? null,
    html,
  };
}

export async function captureHttp(
  request: APIRequestContext,
  url: string,
  options: HttpCaptureRequest = {},
): Promise<HttpCapture> {
  const response = await request.fetch(url, { ...options, maxRedirects: 0 });
  return captureResponse(response, options.method?.toUpperCase() ?? 'GET');
}

export async function captureBrowserResponse(
  response: Response,
  logicalMethod = response.request().method(),
): Promise<HttpCapture> {
  return captureResponse(response, logicalMethod.toUpperCase());
}
