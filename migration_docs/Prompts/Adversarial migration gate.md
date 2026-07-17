---
title: Adversarial migration gate
aliases:
  - Run the adversarial migration gate
parent: "[[Prompts]]"
tags:
  - prompt
  - rails
  - dotnet
  - parity-review
---

# Adversarial migration gate

Act as an independent, adversarial reviewer. You did not implement this migration slice. Determine whether claimed Rails-to-.NET parity is supported by evidence and whether production-style traffic may safely route to .NET.

## Input

```text
SLICE OR PULL REQUEST: [INSERT REFERENCE]
```

Assume the implementation may contain plausible but incorrect AI-generated translations.

Do not trust implementation comments, status claims, confidence scores, screenshots alone, tests written only against .NET, or assertions that do not inspect meaningful outcomes.

## Review objectives

1. Reconstruct Rails behaviour independently.
2. Assess whether characterisation tests cover that behaviour.
3. Run equivalent scenarios against Rails and .NET.
4. Identify unsupported assumptions and silent differences.
5. verify that YARP routing and rollback are safe; and
6. return exactly one gate decision: `PASS` or `BLOCK`.

## Mandatory checks

### Repository evidence

- Routes, constraints, precedence and controller actions.
- Filters, concerns, helpers, models, scopes, validations and callbacks.
- Database constraints, indexes and defaults.
- Views, partials, frontend controllers and assets.
- Existing Rails tests, integrations and jobs.
- Relevant files omitted from the implementer's evidence.

### HTTP and identity

- Methods, paths, query preservation and content types.
- Status codes, redirect destinations and full chains.
- Relevant headers, cookies and expiry attributes.
- Authentication, authorisation, session and CSRF behaviour.
- Error, malformed-input and not-found behaviour.

### UI and accessibility

- HTML structure, text, field names and submitted values.
- IDs, classes and `data-*` attributes.
- Validation wording and error order.
- Keyboard operation, focus management and responsive breakpoints.
- JavaScript-enhanced states.
- Screenshots captured in the same deterministic environment.

### Data

- Rows inserted, updated and deleted.
- Transaction rollback, concurrency and idempotency.
- Defaults, generated values, null/empty values and enums.
- Decimal precision, date/time/time-zone behaviour.
- Association, callback, audit and analytics effects.

### Side effects

- Background and scheduled-job behaviour.
- GOV.UK Notify template and personalisation.
- Contentful payloads, link depth and request counts.
- Cache writes and invalidation.
- Storage, analytics and external-request payloads.
- Retry, timeout and partial-failure behaviour.

### Security

- Open redirects and malformed return URLs.
- Parameter tampering and authorisation bypass.
- Session handling, cookie flags and CSRF.
- Host and forwarded-header handling.
- Security headers, CSP and error-information leakage.
- Logs containing secrets or personal data.

### Performance and operability

- Database-query and external-call counts.
- Cache-hit behaviour and response-time budgets.
- Structured logs and trace correlation.
- Health checks and route-level rollback.

## False-positive discipline

For every proposed finding:

1. cite exact evidence;
2. attempt to disprove the finding;
3. check whether another layer already supplies the behaviour;
4. distinguish a static possibility from an observed runtime difference; and
5. assign confidence based on evidence quality.

Do not report convention-based speculation as fact.

## Required attack cases

Attempt every applicable case:

- missing, unexpected and malformed parameters;
- duplicate submission, refresh, browser back and resubmit;
- stale state and concurrent submission;
- unauthenticated or unauthorised direct access;
- invalid identifiers and deleted linked data;
- missing Contentful content, timeout and excess requests;
- Notify failure and external timeout;
- database failure after a partial operation;
- daylight-saving and other relevant time boundaries;
- unusually long content;
- cached and uncached execution; and
- direct access through Rails, .NET and YARP.

Document why any case is not applicable.

## Routing review

Verify that:

- only intended paths and methods route to .NET;
- route priority is correct and unrelated traffic remains on Rails;
- query strings, host and methods are preserved;
- incompatible session boundaries are not crossed accidentally;
- rollback is a small, documented configuration change; and
- applying the rollback restores Rails behaviour.

## Finding format

For every finding provide:

| Field | Required content |
|---|---|
| ID | Stable identifier |
| Severity | Critical, High, Medium, Low or Informational |
| Confidence | High, Medium or Low |
| Category | HTTP, UI, data, side effect, security, performance or routing |
| Scope | Affected route or journey |
| Evidence | Rails and .NET evidence with file/line or executed observation |
| Reproduction | Exact steps and required state |
| Difference | Expected Rails behaviour and observed .NET behaviour |
| Impact | User, security or operational consequence |
| Checks | False-positive checks performed |
| Remediation | Minimum required change |
| Test | Regression test that must be added |

## Final gate

Return exactly one decision heading:

### PASS

Use only when all required evidence and parity gates are present and no unexplained material difference remains. Still list residual risks, rollback instructions and evidence limitations.

### BLOCK

Use when any material difference, missing test, unsupported assumption or operational risk remains. List the minimum concrete work required to reach `PASS`.

Do not approve based on effort, code quality, architecture or implementer confidence. Approve only demonstrated externally observable parity.
