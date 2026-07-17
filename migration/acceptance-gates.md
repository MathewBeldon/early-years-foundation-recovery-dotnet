# Migration acceptance gates

A route may move from `rails-reference` to `approved-dotnet` only when all applicable gates have linked evidence.
`migration/tools/check-acceptance-evidence.mjs` enforces that every `approved-dotnet` route has an explicit applicable/not-applicable decision for every gate in `migration/acceptance-evidence.json`. Applicable gates require evidence; exclusions require a written justification.

1. **Inventory:** method/path/controller/action, auth boundary, application/framework session purposes, data, external services, jobs and specs are confirmed by repository and runtime evidence.
2. **Contract:** status, redirects, selected headers, cookie roles and observable semantics, and normalised HTML match for success, validation and failure cases. Contract parity does not require byte-identical `Set-Cookie` values or a shared session-cookie format. Session cookie name, encoded value, cryptography and storage representation may differ only under an approved session-ownership record; cookie purpose, attributes, expiry, rotation, invalidation, authentication/authorisation effects and user-visible outcomes remain gated.
3. **Browser/UI:** structure, content, Turbo/Stimulus behaviour, keyboard/focus and exact-pixel baselines match; every dynamic mask is justified.
4. **Accessibility:** automated axe/Pa11y results pass and required manual checks are recorded.
5. **Data:** Rails and .NET scenarios run sequentially from the same fixture; canonical database JSON matches except approved volatile fields.
6. **Side effects:** mail, jobs, webhooks, storage and telemetry payloads/order match against local fakes/test systems.
7. **Security:** authentication, authorisation, CSRF, session/cookie rotation, tamper rejection, timeout, logout/replay, CSP and security header review passes. Security-related differences are approved in `known-differences.md`.
8. **Operations:** health, logs, traces, metrics, timeout/retry behaviour, session-key persistence/rotation and rollback are demonstrated.
9. **Boundary:** the route is part of a complete anonymous or authenticated ownership boundary; OIDC initiation/callback and session-coupled workflow routes have one owner, and a journey is not alternated between Rails and .NET.
10. **Review:** evidence is approved, the route manifest is updated, and rollback is a one-line configuration removal restoring the Rails catch-all.
