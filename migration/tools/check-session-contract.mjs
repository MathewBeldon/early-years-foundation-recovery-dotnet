import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');
const contractPath = path.join(root, 'migration/session-contract.json');
const contract = JSON.parse(fs.readFileSync(contractPath, 'utf8'));
const requiredCaseIds = new Set([
  'SESSION-COOKIE-ATTRIBUTES',
  'SESSION-LOGIN-ROTATION',
  'SESSION-RETURNING-SUB',
  'SESSION-EMAIL-LINK',
  'SESSION-OIDC-ERROR-CANCEL',
  'SESSION-RP-LOGOUT',
  'SESSION-LOGOUT-REPLAY',
  'SESSION-TAMPER-REJECTION',
  'SESSION-SHORT-TIMEOUT',
  'SESSION-CONCURRENT-ISOLATION',
]);
const requiredKeys = new Set([
  'gov_one_auth_state',
  'gov_one_auth_nonce',
  'gov_one_redirect_uri',
  'id_token',
  'registration_review',
  'form_nonce',
  'warden.user.user.key',
  'warden.user.user.session.last_request_at',
  '_csrf_token',
  'flash',
]);
const errors = [];

if (contract.schemaVersion !== 1 || contract.owner !== 'rails-reference') {
  errors.push('Session contract must use schemaVersion 1 and identify Rails as the reference owner.');
}

const cases = new Map((contract.requiredCases ?? []).map((entry) => [entry.id, entry]));
for (const id of requiredCaseIds) {
  const entry = cases.get(id);
  if (!entry) {
    errors.push(`Missing required session case ${id}.`);
  } else if (typeof entry.evidence !== 'string' || !fs.existsSync(path.join(root, entry.evidence))) {
    errors.push(`${id} references missing evidence ${JSON.stringify(entry.evidence)}.`);
  }
}

const purposes = new Map((contract.sessionPurposes ?? []).map((entry) => [entry.key, entry]));
for (const key of requiredKeys) {
  const entry = purposes.get(key);
  if (!entry || !entry.purpose || !['application', 'framework'].includes(entry.owner)) {
    errors.push(`Missing or incomplete session-purpose inventory for ${key}.`);
  }
}

if (typeof contract.report !== 'string' || !fs.existsSync(path.join(root, contract.report))) {
  errors.push(`Session characterization report is missing: ${JSON.stringify(contract.report)}.`);
}

if (errors.length > 0) {
  throw new Error(`Session contract check failed:\n- ${errors.join('\n- ')}`);
}

process.stdout.write(
  `Session contract tracks ${purposes.size} purposes and ${cases.size} required cases.\n`,
);
