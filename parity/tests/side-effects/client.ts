import type { APIRequestContext } from '@playwright/test';

export interface CapturedSideEffect {
  sequence: number;
  method: string;
  path: string;
  headers: Record<string, string>;
  body: string;
}

export async function resetSideEffects(
  request: APIRequestContext,
  baseUrl: string,
): Promise<void> {
  const response = await request.delete(`${baseUrl}/captures`);
  if (!response.ok()) {
    throw new Error(`Side-effect reset failed with ${response.status()}.`);
  }
}

export async function captureSideEffects(
  request: APIRequestContext,
  baseUrl: string,
): Promise<CapturedSideEffect[]> {
  const response = await request.get(`${baseUrl}/captures`);
  if (!response.ok()) {
    throw new Error(`Side-effect capture failed with ${response.status()}.`);
  }

  return response.json() as Promise<CapturedSideEffect[]>;
}
