import fs from 'node:fs';
import { compareEventStreams } from './event-comparator.js';
import type {
  CapturedEvent,
  EventProjection,
} from './event-comparator.js';
import { expect, test } from '../support/fixtures.js';

interface Scenario {
  identity: { sub: string; email: string };
  contract: EventProjection[];
  railsEvents: CapturedEvent[];
  candidateEvents: CapturedEvent[];
}

const scenario = JSON.parse(
  fs.readFileSync('fixtures/registration-comparator-scenario.json', 'utf8'),
) as Scenario;

function clone(events: CapturedEvent[]): CapturedEvent[] {
  return structuredClone(events);
}

test.describe('stage one: P0/P1 event projection mutation controls', () => {
  test('accepts the pinned-identity registration projection', () => {
    expect(scenario.identity.sub).toBe(
      'urn:fdc:gov.uk:one-login:parity:pinned-registration-001',
    );
    expect(() =>
      compareEventStreams(
        scenario.railsEvents,
        scenario.candidateEvents,
        scenario.contract,
      ),
    ).not.toThrow();
  });

  test('detects a missing P0 event', () => {
    const candidate = clone(scenario.candidateEvents).filter(
      (event) => event.name !== 'user_registration',
    );
    expect(() =>
      compareEventStreams(scenario.railsEvents, candidate, scenario.contract),
    ).toThrow('Event projection differs for priorities P0/P1');
  });

  test('detects a surplus P1 event', () => {
    const candidate = clone(scenario.candidateEvents);
    candidate.splice(1, 0, structuredClone(candidate[1] as CapturedEvent));
    expect(() =>
      compareEventStreams(scenario.railsEvents, candidate, scenario.contract),
    ).toThrow('Event projection differs for priorities P0/P1');
  });

  test('detects a changed projected property', () => {
    const candidate = clone(scenario.candidateEvents);
    const registration = candidate.find((event) => event.name === 'user_registration');
    if (!registration) throw new Error('Pinned registration event is missing.');
    registration.properties.success = false;
    expect(() =>
      compareEventStreams(scenario.railsEvents, candidate, scenario.contract),
    ).toThrow('Event projection differs for priorities P0/P1');
  });

  test('supports a P0-only gate without silently weakening the P0/P1 gate', () => {
    const candidate = clone(scenario.candidateEvents);
    const p1 = candidate.find((event) => event.name === 'user_name_change');
    if (!p1) throw new Error('Pinned P1 event is missing.');
    p1.properties.success = false;

    expect(() =>
      compareEventStreams(scenario.railsEvents, candidate, scenario.contract, ['P0']),
    ).not.toThrow();
    expect(() =>
      compareEventStreams(scenario.railsEvents, candidate, scenario.contract),
    ).toThrow('Event projection differs for priorities P0/P1');
  });
});

test.describe('stage two: timestamp projections', () => {
  test('accepts different absolute times with valid presence, ordering and windows', () => {
    expect(() =>
      compareEventStreams(
        scenario.railsEvents,
        scenario.candidateEvents,
        scenario.contract,
      ),
    ).not.toThrow();
  });

  test('detects a missing timestamp', () => {
    const candidate = clone(scenario.candidateEvents);
    candidate[1]!.timestamp = null;
    expect(() =>
      compareEventStreams(scenario.railsEvents, candidate, scenario.contract),
    ).toThrow('Candidate user_name_change has no valid timestamp');
  });

  test('detects timestamp order reversal', () => {
    const candidate = clone(scenario.candidateEvents);
    candidate[1]!.timestamp = '2026-07-17T08:59:59.000Z';
    expect(() =>
      compareEventStreams(scenario.railsEvents, candidate, scenario.contract),
    ).toThrow('Candidate user_name_change timestamp is out of order');
  });

  test('detects a timestamp outside the projected window', () => {
    const candidate = clone(scenario.candidateEvents);
    candidate[1]!.timestamp = '2026-07-17T09:03:00.001Z';
    expect(() =>
      compareEventStreams(scenario.railsEvents, candidate, scenario.contract),
    ).toThrow('Candidate user_name_change is outside its 120000ms window');
  });
});
