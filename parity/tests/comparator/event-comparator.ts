import assert from 'node:assert/strict';

export type EventPriority = 'P0' | 'P1';

export interface CapturedEvent {
  name: string;
  properties: Record<string, unknown>;
  timestamp?: string | null;
}

export interface EventProjection {
  name: string;
  priority: EventPriority;
  properties: string[];
  timestamp: {
    required: boolean;
    ordered: boolean;
    withinPreviousMs?: number;
  };
}

interface ProjectedEvent {
  name: string;
  priority: EventPriority;
  properties: Record<string, unknown>;
}

function selectedContract(
  contract: EventProjection[],
  priorities: readonly EventPriority[],
): EventProjection[] {
  const selected = new Set(priorities);
  return contract.filter((event) => selected.has(event.priority));
}

function project(
  events: CapturedEvent[],
  contract: EventProjection[],
): ProjectedEvent[] {
  const projections = new Map(contract.map((entry) => [entry.name, entry]));
  return events
    .filter((event) => projections.has(event.name))
    .map((event) => {
      const projection = projections.get(event.name);
      if (!projection) throw new Error(`Missing projection for ${event.name}.`);
      return {
        name: event.name,
        priority: projection.priority,
        properties: Object.fromEntries(
          projection.properties.map((property) => [property, event.properties[property]]),
        ),
      };
    });
}

function validateTimestamps(
  label: string,
  events: CapturedEvent[],
  contract: EventProjection[],
): void {
  const projections = new Map(contract.map((entry) => [entry.name, entry]));
  const selected = events.filter((event) => projections.has(event.name));
  let previous: number | undefined;

  for (const event of selected) {
    const rule = projections.get(event.name);
    if (!rule) continue;
    const timestamp = Date.parse(event.timestamp ?? '');
    if (rule.timestamp.required && !Number.isFinite(timestamp)) {
      throw new Error(`${label} ${event.name} has no valid timestamp.`);
    }
    if (!Number.isFinite(timestamp)) continue;

    if (rule.timestamp.ordered && previous !== undefined && timestamp < previous) {
      throw new Error(`${label} ${event.name} timestamp is out of order.`);
    }
    if (
      rule.timestamp.withinPreviousMs !== undefined &&
      previous !== undefined &&
      timestamp - previous > rule.timestamp.withinPreviousMs
    ) {
      throw new Error(
        `${label} ${event.name} is outside its ${rule.timestamp.withinPreviousMs}ms window.`,
      );
    }
    previous = timestamp;
  }
}

/**
 * Qualifies semantic event parity. Absolute timestamps are deliberately not
 * compared: presence, ordering and contract windows are compared per run.
 */
export function compareEventStreams(
  rails: CapturedEvent[],
  candidate: CapturedEvent[],
  contract: EventProjection[],
  priorities: readonly EventPriority[] = ['P0', 'P1'],
): void {
  const selected = selectedContract(contract, priorities);
  const railsProjection = project(rails, selected);
  const candidateProjection = project(candidate, selected);

  try {
    assert.deepStrictEqual(candidateProjection, railsProjection);
  } catch (error) {
    throw new Error(
      `Event projection differs for priorities ${priorities.join('/')}.`,
      { cause: error },
    );
  }

  validateTimestamps('Rails', rails, selected);
  validateTimestamps('Candidate', candidate, selected);
}
