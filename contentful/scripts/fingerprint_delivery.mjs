import crypto from 'node:crypto';

function required(name) {
  const value = process.env[name];
  if (!value) throw new Error(`${name} must be configured.`);
  return value;
}

function canonical(value) {
  if (Array.isArray(value)) return value.map(canonical);
  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.keys(value)
        .sort()
        .map((key) => [key, canonical(value[key])]),
    );
  }
  return value;
}

async function fetchCollection(baseUrl, collection, token) {
  const items = [];
  for (let skip = 0; ; skip += 1000) {
    const url = new URL(`${baseUrl}/${collection}`);
    url.searchParams.set('limit', '1000');
    url.searchParams.set('skip', String(skip));
    if (collection === 'entries') url.searchParams.set('include', '0');

    const response = await fetch(url, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok) {
      throw new Error(`Contentful ${collection} fingerprint failed with HTTP ${response.status}.`);
    }

    const body = await response.json();
    items.push(...body.items);
    if (items.length >= body.total) break;
  }

  return items.sort((left, right) => left.sys.id.localeCompare(right.sys.id));
}

const space = required('CONTENTFUL_SPACE');
const environment = required('CONTENTFUL_ENVIRONMENT');
const token = required('CONTENTFUL_DELIVERY_TOKEN');
if (['production', 'staging'].includes(environment.toLowerCase())) {
  throw new Error(`Refusing to fingerprint non-test Contentful environment ${environment}.`);
}

const baseUrl = `https://cdn.contentful.com/spaces/${encodeURIComponent(space)}/environments/${encodeURIComponent(environment)}`;
const [contentTypes, entries, assets] = await Promise.all([
  fetchCollection(baseUrl, 'content_types', token),
  fetchCollection(baseUrl, 'entries', token),
  fetchCollection(baseUrl, 'assets', token),
]);
const canonicalPayload = JSON.stringify(canonical({ contentTypes, entries, assets }));
const result = {
  schemaVersion: 1,
  environment,
  spaceIdHash: crypto.createHash('sha256').update(space).digest('hex'),
  contentTypeCount: contentTypes.length,
  entryCount: entries.length,
  assetCount: assets.length,
  digest: crypto.createHash('sha256').update(canonicalPayload).digest('hex'),
};

process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
