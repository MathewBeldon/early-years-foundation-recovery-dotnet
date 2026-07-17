# Comparator qualification

The event comparator is qualified in two explicit stages against `parity/fixtures/registration-comparator-scenario.json`, whose One Login subject and email are pinned synthetic identifiers.

1. Stage one proves P0/P1 projection behaviour and runs missing-event, surplus-event and changed-property mutation controls. A P0-only gate is tested separately so it cannot be mistaken for the complete P0/P1 result.
2. Stage two proves that timestamp presence, event ordering and per-event elapsed windows are enforced. Absolute wall-clock equality is deliberately excluded so sequential Rails and candidate executions can be compared.

The qualification suite is `parity/tests/comparator/event-comparator.spec.ts` and is part of the platform check. This qualifies the comparator logic; database captures from executed product scenarios remain the evidence input for a route or journey gate.
