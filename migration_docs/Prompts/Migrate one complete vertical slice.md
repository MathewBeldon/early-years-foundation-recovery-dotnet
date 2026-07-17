---
title: Migrate one complete vertical slice
parent: "[[Prompts]]"
tags:
  - prompt
  - rails
  - dotnet
  - vertical-slice
---

# Migrate one complete vertical slice

You are migrating one complete user journey from the existing Ruby on Rails application to ASP.NET Core.

## Inputs

```text
SLICE NAME: [INSERT NAME]
USER JOURNEY: [INSERT COMPLETE USER JOURNEY]
EXPECTED ROUTES: [INSERT ROUTES OR "DISCOVER FROM REPOSITORY"]
```

Rails is the behavioural reference. The objective is externally observable parity, not a similar-looking reimplementation. Complete discovery, characterisation, implementation, differential verification and routing for this slice only.

## Non-negotiable rules

- Do not redesign, simplify, improve or reinterpret behaviour.
- Preserve HTML structure, text, IDs, names, classes, `data-*` attributes, CSS, JavaScript, focus behaviour and responsive layout.
- Reuse existing assets and frontend technology wherever technically possible.
- Do not infer runtime behaviour from names, conventions or comments. Support every claim with source evidence, an existing test or observed execution.
- Record unknowns instead of silently choosing an interpretation.
- Preserve paths, methods, status codes, redirect chains, query strings, headers, cookies, validation wording and error ordering.
- Preserve database effects, transaction boundaries, jobs and external calls.
- Do not reproduce an identified security vulnerability without explicit approval.
- Do not change YARP routing until every gate passes.
- Do not let Entity Framework modify the database schema.
- Screenshots alone do not establish parity.
- Do not refactor unrelated Rails or .NET code.

## Phase 1: Discover

Identify every relevant route, constraint, controller action, filter, concern, model, association, scope, validation, callback, constraint, view, layout, partial, helper, presenter, component, JavaScript controller, CSS selector, asset, cache operation, job, email, analytics event, external request and existing test.

Trace happy paths and failure paths through the complete execution chain.

Create:

```text
migration/slices/[SLICE-SLUG]/evidence.md
migration/slices/[SLICE-SLUG]/behaviour-contract.json
migration/slices/[SLICE-SLUG]/test-matrix.md
migration/slices/[SLICE-SLUG]/open-questions.md
```

Every behaviour-contract entry must include:

- behaviour, inputs and preconditions;
- output;
- database and other side effects;
- evidence;
- confidence in the evidence; and
- test coverage.

Do not implement the .NET slice until the evidence bundle is complete enough to write meaningful characterisation tests.

## Phase 2: Characterise Rails

Before implementation, add executable scenarios for all applicable cases:

- successful journey;
- unauthenticated and unauthorised access;
- invalid, missing and boundary input;
- duplicate submission and stale state;
- not found, refresh/retry and back-button behaviour;
- mobile and desktop layouts;
- keyboard navigation and accessibility;
- Contentful or other external-service failure; and
- unusually long content and relevant time boundaries.

Capture:

- status, redirect chain, selected headers and cookies;
- important HTML, field names/values and ordered validation errors;
- screenshots and accessibility results;
- database state;
- jobs, emails, cache changes and files;
- external requests and analytics events; and
- dependency-call counts where important.

Characterisation tests must pass against Rails before .NET implementation begins. If Rails cannot run, stop and report the concrete blocker.

## Phase 3: Implement the .NET slice

Use ASP.NET Core MVC and Razor.

- Match Rails route paths and methods exactly.
- Preserve rendered markup and the existing asset pipeline wherever possible.
- Match form names, submitted values, validation messages and error order.
- Match authentication, authorisation and CSRF outcomes.
- Map the existing PostgreSQL schema without modifying it.
- Preserve null/empty, enum, timestamp, decimal and time-zone semantics.
- Preserve transaction, concurrency and idempotency behaviour.
- Represent Contentful, Notify, One Login, storage and job boundaries through focused interfaces where applicable.
- Add structured logs and traces without secrets or personal data.
- Add focused unit and integration tests.
- Avoid speculative general-purpose abstractions.

## Phase 4: Verify differentially

Run the same parity scenarios against Rails and .NET.

For each write scenario:

1. restore the baseline;
2. run Rails and capture all outcomes;
3. restore the identical baseline;
4. run .NET and capture the same outcomes; and
5. compare them.

Compare HTTP, redirects, cookies, HTML, screenshots, accessibility, database state, jobs, emails, cache changes, external calls, analytics, Contentful requests, database queries and agreed response-time budgets.

Classify every difference as one of:

- **defect**;
- **approved intentional difference**;
- **volatile value requiring a narrow, documented normalisation**; or
- **unresolved blocker**.

Do not hide unexplained differences with broad exclusions, masks or screenshot tolerances.

## Phase 5: Review adversarially

Try to disprove parity. Check at least:

- callbacks, default scopes and hidden database effects;
- `nil`, missing and empty-string handling;
- route precedence, model binding and parameter tampering;
- duplicate requests, stale state and direct access to intermediate steps;
- session expiry, CSRF and malformed return URLs;
- missing content, timeouts and partial-failure rollback;
- daylight-saving boundaries and time-zone conversion;
- cached versus uncached execution;
- keyboard focus and JavaScript-enhanced states; and
- unexpected extra database or external-service calls.

Add a test for each material gap discovered, then rerun both targets.

## Phase 6: Route

Only after every required gate passes:

1. add the exact slice paths and methods to the .NET YARP cluster;
2. retain the Rails catch-all;
3. prove selected routes reach .NET through the gateway;
4. prove unrelated routes remain on Rails;
5. exercise the one-change rollback; and
6. update `route-manifest.json` and `migration-status.md`.

## Definition of done

- [ ] Evidence and open questions are complete.
- [ ] Rails characterisation tests pass.
- [ ] .NET unit and integration tests pass.
- [ ] Differential HTTP and data tests pass.
- [ ] Visual and accessibility tests pass at approved viewports.
- [ ] External side effects match.
- [ ] Performance and dependency-call budgets pass where applicable.
- [ ] No unexplained material difference remains.
- [ ] Gateway routing and rollback tests pass.
- [ ] An independent reviewer can reproduce the result.

## Required final report

Report the scope, evidence references, tests added, commands executed, full results, defects found, approved differences, unresolved risks, routes switched and rollback instructions.

Base confidence only on executed evidence. Do not use a confidence score to compensate for missing evidence. If any definition-of-done item is unmet, state that the slice is not ready to route.

