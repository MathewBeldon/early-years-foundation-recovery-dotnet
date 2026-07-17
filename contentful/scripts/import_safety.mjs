const forbiddenEnvironments = new Set(['production', 'staging']);

export function assertSafeDestination({ allowTestSeed, environmentId, sourceSpace, spaceId }) {
  if (allowTestSeed !== 'true') {
    throw new Error('Set CONTENTFUL_ALLOW_TEST_SEED=true to confirm this destructive non-production operation.');
  }

  if (forbiddenEnvironments.has(environmentId.toLowerCase())) {
    throw new Error(`Refusing to seed protected environment '${environmentId}'.`);
  }

  if (spaceId === sourceSpace) {
    throw new Error('Refusing to write to the source HFEYP Contentful space. Use a separate non-production destination space.');
  }
}
