# Early Years Foundation Recovery (.NET migration workspace)

This repository is a local Rails-to-.NET compatibility workspace. The Rails PostgreSQL schema at migration version `20260529104000` (upstream tag `v1.5.0`) is authoritative. The web application validates that contract at startup and never applies migrations implicitly.

## Prerequisites

- .NET 10 SDK
- Docker Desktop or Podman with Compose support
- PowerShell 7

## Run the .NET application against a Rails-shaped database

Start the ordinary local dependencies, load the Rails schema into PostgreSQL, apply the additive .NET migration, then run the app:

```powershell
docker compose up -d
dotnet run --project src/EarlyYearsFoundationRecovery.Web -- --schema-preflight
dotnet run --project src/EarlyYearsFoundationRecovery.Web -- --migrate
dotnet run --project src/EarlyYearsFoundationRecovery.Web
```

The preflight command exits with code 2 when any required Rails table is absent or `schema_migrations` is older than `20260529104000`. `--migrate` records the existing Rails schema as a no-op EF baseline, then applies only the .NET-owned additions: `background_jobs` and `mail_events.notification_id`. Rails Devise and Que columns/tables are deliberately preserved and unmapped. `users.country` is Rails-owned as of `20260529104000` and is never created or dropped by .NET.

The app is at <http://localhost:5000>, health is at <http://localhost:5000/health>, and the One Login simulator is at <http://localhost:3333>.

### Note.body encryption

PostgreSQL/shared persistence requires the Rails 7.2.3.1 Note.body encryption
configuration. Supply these narrowly scoped secrets through deployment secret
management; they are intentionally absent from `appsettings.json`:

```text
NoteEncryption__PrimaryKey
NoteEncryption__KeyDerivationSalt
NoteEncryption__PreviousPrimaryKeys__0
NoteEncryption__PreviousPrimaryKeys__1
```

The first two values map to Rails encrypted credentials
`active_record_encryption.primary_key` and
`active_record_encryption.key_derivation_salt`. Previous keys are optional and
are tried in the listed order after the current key. The Rails deterministic
key is not used by `Note.body`. Never provide `RAILS_MASTER_KEY` to this
application and never use it as a substitute for the two narrowly scoped
secrets.

Startup and `--schema-preflight` fail closed when the current key or salt is
missing, blank, duplicated in the previous-key list, or blank in that list.
Application code sees plaintext `Note.Body`; the PostgreSQL `notes.body` value
is the Rails JSON ciphertext envelope. The explicit InMemory persistence used
by Testing/demo hosts is the only passthrough boundary.

For rotation, deploy the new primary key with the old primary key as the first
`PreviousPrimaryKeys` value. New writes use the new key and reads accept both.
Keep the old key until all old notes have been re-encrypted or the rollback
window has ended. To roll back during that window, deploy the old key as
primary and the new key as its previous key. Remove the old key only after
that window and any required re-encryption have been verified. The protocol
adapter and its vectors are documented in
`tools/note-encryption-interop/README.md`.

Webhook credentials are separately owned. Configuration precedence is the native .NET
key (Audit:BotToken, Contentful:WebhookSecret, or Notify:CallbackToken), then the matching
Rails variable (AUDIT_BOT_TOKEN, CONTENTFUL_WEBHOOK_TOKEN, or NOTIFY_WEBHOOK_TOKEN), then
the transitional BOT_TOKEN fallback, then an appsettings.json value. No callback
credentials are committed. For local development, set them explicitly with user secrets:

    dotnet user-secrets set "Audit:BotToken" "<audit token>" --project src/EarlyYearsFoundationRecovery.Web
    dotnet user-secrets set "Contentful:WebhookSecret" "<Contentful token>" --project src/EarlyYearsFoundationRecovery.Web
    dotnet user-secrets set "Notify:CallbackToken" "<Notify token>" --project src/EarlyYearsFoundationRecovery.Web

## Rails switchover register

The following known deliberate differences or historical compatibility hazards must be
checked at cutover:

- [ ] An unconfigured Contentful webhook credential returns 503 in .NET; Rails v1.5.0
  returns 401. Ensure deployment supplies CONTENTFUL_WEBHOOK_TOKEN (or the higher
  precedence .NET key) and monitoring expects this diagnostic difference.
- [ ] Authenticated Contentful webhooks are intentionally hardened beyond Rails v1.5.0:
  .NET parses the body once and rejects empty, malformed, non-object, or route-invalid
  payloads with RFC ProblemDetails before cache, database, or job side effects. Rejected
  payload content is never logged.
- [ ] Failed-authentication throttling uses an in-process .NET failure tracker rather
  than Rails cache internals. The observable contract is matched: failures 1-20 return
  401, later failures within five minutes return 429, successful authentication clears
  the endpoint/client-IP counter, and successful requests are never counted. Verify the
  hosting topology preserves those semantics across any multi-instance deployment.
- [ ] X-Contentful-Webhook-Secret was removed as an accepted credential path. It was a
  .NET-only header and never existed upstream; Contentful must send the canonical BOT
  header to /change and /release.
- [ ] Client-IP throttling reads ASP.NET Core's resolved remote address. Verify trusted
  proxy/forwarded-header configuration in the target hosting environment before cutover.
- [ ] GOV.UK One Login users created by .NET receive `confirmed_at`, matching Rails mail
  recipient scopes, but their Rails `encrypted_password` remains the schema default empty
  string instead of a hash of Rails' random password. One Login does not use that password;
  verify it remains irrelevant or backfill valid Devise hashes before a Rails switchover.

## One-command parity environment

The parity environment creates a detached worktree at the last Rails commit, two PostgreSQL databases, Rails, .NET, the One Login simulator, and recording Notify/Contentful fakes. For a clean one-command rehearsal, use `reset`; it creates both databases from the Rails migrations and the same synthetic SQL fixture before starting either application:

```powershell
./parity.ps1 reset
./parity.ps1 test
```

Use `./parity.ps1 reset` before each scenario group to recreate both databases from the same Rails schema and synthetic fixture manifest. Use `./parity.ps1 down` to stop the environment. `reset` deletes only the parity Compose volumes.

For a deterministic manual click-through of the .NET app, use the demo launcher. It performs the same clean parity reset and startup, configures the local One Login simulator with a committed synthetic identity, and does not run parity tests:

```powershell
./parity.ps1 demo
./parity.ps1 demo -DemoUser assessment
```

The default identity is `existing@example.test`. The allowlisted `-DemoUser` values are `existing`, `resuming`, `assessment`, `questionnaire`, `certificate-complete`, `certificate-incomplete`, and `account-preferences`. The selector cannot provide arbitrary email addresses, subjects, SQL, or data files. The launcher prints the sign-in URL and safe starting pages, including account, modules, assessment, questionnaire, and certificate routes.

The demo is intentionally stateful: signing in, submitting answers, editing preferences, and visiting tracked pages can change the synthetic databases. Rerun `./parity.ps1 demo` to restore the fixture. Use `./parity.ps1 status` for a read-only Compose and HTTP readiness check. Use `./parity.ps1 down` to stop containers while retaining parity volumes; use `demo` or `reset` when you want the documented volume reset.

Suggested manual pages after signing in:

- `/my-account` and `/my-modules`
- `/registration/name/edit`, `/registration/training-emails/edit`, and `/registration/research-participant/edit`
- `/modules/module-1/content-pages/assessment-intro`
- `/modules/module-2/questionnaires/summative-q1`
- `/modules/module-2/content-pages/certificate` and its `.pdf` route

These are local parity pages only. Do not point the launcher at production services or add production credentials to the compose environment.

The committed fixture contract is [synthetic-fixtures.json](parity/fixtures/synthetic-fixtures.json). It contains no production-derived data. The recording fake exposes requests at `http://localhost:4010/_requests` for Notify and `http://localhost:4020/_requests` for Contentful.

[rails-contract.json](parity/rails-contract.json) is the single source of truth for the pin: upstream, release ref, full commit SHA, schema version, and the date currency was last reviewed. `parity.ps1` reads it and hard-codes no commit or tag. `RequiredRailsVersion` is the one permitted duplicate, because production code must not depend on the parity directory. Changing the pin is an explicit schema-contract update and must be reviewed together with intentional fixture-manifest changes. Never use production exports as parity fixtures.

The pin requires upstream objects. Add the reference remote once with `git remote add upstream https://github.com/DFE-Digital/early-years-foundation-recovery` and `git fetch upstream --tags`. `parity.ps1` refuses to run when an existing worktree sits at a different commit from the pin, rather than silently comparing against a stale Rails.

`RailsContractConsistencyTests` fails when the manifest, `RequiredRailsVersion`, `parity.ps1`, or a present worktree disagree. It cannot detect that the pin has fallen behind upstream — that needs the network:

```powershell
./parity.ps1 check-pin
```

`check-pin` is read-only and never advances anything. It inspects stable `vX.Y.Z` tags only, ignoring release candidates, verifies the pinned tag still resolves to the recorded SHA and schema version, and reports newer stable releases for review. Exit codes: `0` current, `3` a newer stable release requires review, `4` required upstream objects could not be reached, and `5` the recorded tag, commit, or schema do not agree with upstream.

To advance the pin, review the target release and its schema and fixture effects, then update `releaseRef`, `commit`, `schemaVersion`, and `reviewedOn` in `parity/rails-contract.json` together with `RequiredRailsVersion` and any intentional fixture-manifest changes. Run the contract tests and `./parity.ps1 check-pin` before committing the advance.

This workspace previously validated against a Rails commit three months stale while asserting it was authoritative: everything agreed with the pin, and nobody checked the pin. Consistency and currency are separate properties, and `check-pin` covers the second.

## Playwright and certificates

The parity `dotnet-app` uses the dedicated
[`parity/Dockerfile`](parity/Dockerfile) image. Its immutable build
layer runs the installer supplied by Microsoft.Playwright 1.61.0 with
`--with-deps`, installing Chromium and Chromium headless shell revision 1228
(149.0.7827.55) plus Playwright's matching Ubuntu libraries. The application
runs as the image's non-root `app` user; do not add `ExecutablePath` or
`--no-sandbox` overrides. Compose also allocates a larger shared-memory segment
for Chromium.

The image build fails if the package pin or expected browser payload has
drifted. Its container health check verifies the non-root identity, both pinned
browser executables and versions, and the .NET `/health` endpoint. Rebuild it
through `./parity.ps1 reset` after changing the Playwright package.

For browser-based tests run directly on the host, restore/build once and install
the pinned host Chromium version separately:

```powershell
dotnet build EarlyYearsFoundationRecovery.slnx
pwsh src/EarlyYearsFoundationRecovery.ParityTests/bin/Debug/net10.0/playwright.ps1 install chromium
```

Certificate PDFs in the parity stack use the browser baked into the parity
image; host-run browser tests use the host installation above. Certificates are
generated as real PDFs with extractable recipient and module text.

This image is parity packaging only. Production packaging must separately pin
Microsoft.Playwright and its matching browser revision and Linux dependencies,
install them at image-build time, run the application as a non-root user, and
add an equivalent browser/readiness proof before certificate generation is
released.

## Test suites and parity report

```powershell
dotnet restore EarlyYearsFoundationRecovery.slnx --force-evaluate
dotnet tool restore
dotnet build EarlyYearsFoundationRecovery.slnx --no-restore -warnaserror
dotnet test EarlyYearsFoundationRecovery.slnx --no-build --filter "Category!=Parity&Category!=Database"

$env:RAILS_BASE_URL = "http://localhost:3000"
$env:DOTNET_BASE_URL = "http://localhost:5000"
dotnet test src/EarlyYearsFoundationRecovery.ParityTests
```

The parity suite compares two running applications, so it never reports success
without them. It behaves as follows:

| Environment | Result |
| --- | --- |
| `RAILS_BASE_URL` and `DOTNET_BASE_URL` set | Runs normally |
| Neither set, `PARITY_ENV_OPTIONAL` set | Reported as **skipped**, with a reason |
| Neither set, no opt-out | **Fails**, naming the missing setup |

A configured environment always runs, so an opt-out left in a shell can never
quietly disable a parity environment that is present. The database suite similarly
requires a reachable Docker or Podman runtime; `DATABASE_TESTS_OPTIONAL=1` reports an explicit
skip only when the runtime is unreachable. Ordinary local work should use
`--filter "Category!=Parity&Category!=Database"` rather than counting checks that did not run.

Until a pipeline exists, the parity suite is not enforced anywhere automatically.
Adding a separately named CI job that runs it against a live environment is a
prerequisite for treating the migration as production-ready.

Certificate HTML is served at the Rails-compatible
`/modules/{module}/content-pages/{certificate-page}` route. Its canonical PDF
link is the same route with `.pdf`; `/modules/{module}/certificate.pdf` remains
only as a compatibility alias. Both routes require an authenticated registered
user, but direct certificate access is intentionally not assessment-gated: Rails
renders a named/dated certificate for completed progress and a 200 placeholder
for incomplete or failed progress. The assessment-results continuation remains
gated by a passed assessment. PDF comparisons use response metadata, the `%PDF-`
signature, page count, and extracted semantic text; PDF bytes are not compared.
The application does not add a filename or `Content-Disposition` header.

The parity suite compares semantic status, redirects, headings, validation messages, navigation, MIME types, and download filenames. It normalizes antiforgery values, framework cookie names, generated IDs, timestamps, and insignificant whitespace. Results are written to `TestResults/parity-report.json`, including every accepted normalization and unresolved difference.

The committed cross-application manifest currently covers 19 anonymous scenarios: public, error, health, authentication-redirect, feedback-introduction, and static-content. Authenticated GET /my-account and GET /my-modules for the existing synthetic learner are covered against both live applications. The /my-modules check asserts only stable shared semantics: 200, the My modules heading and intro, signed-in navigation, Rails-matching unstarted copy, and Rails-shaped module title hrefs (`/modules/{name}`). Available and upcoming bucket membership is not compared: the parity harness deliberately uses the separate demo JSON provider, whose fixture `live` field remains outside this Contentful-boundary slice. The real Contentful provider now classifies modules using the pinned Rails v1.5.0 ContentIntegrity contract; asset existence is not probed, matching Rails' documented network-check omission. Authenticated GET assessment results for the synthetic assessment learner (`assessment@example.test`) are covered against both live applications. That check asserts stable shared GET semantics: failed module-1 score/result with My modules and Retake test actions and no certificate/continue action; retake target 200 with Start test; passed module-2 score/result with certificate continuation href and no retake. The questionnaire submission slice now covers authenticated GET `summative-q1`, first-answer submission for `questionnaire@example.test` with an existing passed module-2 assessment, each app's redirect chain to the final `summative-q2` page, numeric answer persistence, submission nonce/422/CSRF behavior, and Rails-owned assessment-start/answer events. The certificate slice now covers completed and failed/incomplete authenticated learners, direct HTML and canonical PDF requests, placeholder/name/date/criteria semantics, one-page extractable PDFs, exact content type and disposition behavior, and certificate page-progress effects. Registration resumption is driven through the late check-your-answers and training-email validation path in both applications. The authenticated learning-log slice signs in `assessment@example.test`, renders the log and module note form, creates and updates the deterministic module-1 `key-concepts` note, and compares redirects and rendered values. The Rails content-page redirect is an accepted intermediate normalization; the final path and page must match. .NET records the Rails-owned assessment, questionnaire, and learning-log events, and database reconciliation verifies those per-user details plus certificate progress, assessment immutability, and event names. Live/draft classification remains outside the demo-provider parity contract. The ordinary .NET suites cover registration, modules, assessments, learning logs, account changes, callbacks, and persistence. Module-content breadth, account changes beyond /my-account, and other authenticated journeys remain outstanding and are not yet driven through both live applications. Do not treat the migration as production-ready until equivalent Rails/.NET scenarios for those remaining journey groups have been added to the manifest and pass.

The live authenticated questionnaire journey slice drives two clean deterministic identities through every module-1 summative question: `questionnaire-pass@example.test` submits answers `2,1` and must finish at 100%/passed; `questionnaire-fail@example.test` submits `1,2` and must finish at 0%/failed. The failing identity then drives module 4 with answers `2,1,2`, producing a deterministic 0%/failed result. The module-4 check continues through `feedback-q1`: both applications render the radio form, reject a missing answer with 422, persist the first option as an opinion response, emit Rails-shaped feedback telemetry, and redirect to the `thankyou` page. It also verifies that the failed result does not expose certificate eligibility and that the fixture order remains `feedback-q1` -> `thankyou` -> `certificate`. `parity/reconcile.ps1` proves the persisted assessments, summative and feedback responses, and Rails-owned assessment, answer, feedback-start, and confidence-completion events match across both databases. The only accepted route normalization is Rails' intermediate `/content-pages/summative-q2` redirect before the questionnaire route. The module-4 fixture proves focused synthetic navigation and grading only; it is intentionally not a production Contentful compatibility claim and remains incomplete against the full Rails/.NET ContentIntegrity contract. Real Contentful inventory and full release-valid module content remain required before cutover.

The authenticated formative slice uses the fresh `formative-questionnaire@example.test` identity for Module 1 `check-understanding`. Both applications must render the unanswered form, reject an empty answer with 422 and no writes, persist wrong answer `2` as one immutable formative response with no assessment, redirect to the canonical questionnaire URL, and preserve the read-only incorrect-answer explanation on revisit. The question itself must not create module progress or page-view telemetry. Reconciliation compares the response and normalized Rails-shaped `questionnaire_answer` event and explicitly requires empty assessment and progress collections.

`./parity/reconcile.ps1` compares canonical row counts, ID bounds, completions, and result values across both databases. It also compares the questionnaire fixture user's passed assessment, numeric response attached to that assessment, and Rails-shaped assessment-start/answer event properties, plus the pass/fail questionnaire journey assessment, responses, completion and module-feedback events, formative response/event state with explicitly absent assessment/progress, certificate fixture progress keys/completion flags, unchanged assessment results, and event names. The module-4 journey covers feedback rendering, missing-answer validation, radio response persistence, the thank-you redirect, deduplicated `feedback_start`, Rails' pinned single-question behavior where the normal journey emits no `feedback_complete`, and `confidence_check_complete` on the thank-you page. No event type is excluded from aggregate reconciliation. For the learning-log scenario it compares the deterministic note's user identity, title, training module, and page name without comparing encrypted body ciphertext; it separately compares the Rails-shaped `user_note_created` and `user_note_updated` event semantics and stable properties. The report is written to `TestResults/database-reconciliation.json` and the script runs automatically by `./parity.ps1 test`.

## Durable jobs, integrations, and exports

`IBackgroundJobService.EnqueueAsync(jobType, payload)` writes to PostgreSQL. Workers claim due jobs with `FOR UPDATE SKIP LOCKED`, retry with backoff, and retain permanent failures. The supported scheduled export type is `dashboard_export`; generated CSVs are stably ordered under `storage/exports/YYYY-MM-DD/`.

Notify uses the GOV.UK Notify email endpoint shape and persists returned notification IDs. Delivery callbacks update both `users.notify_callback` and the matching `mail_events.callback`, matching Rails behavior. Contentful `/release` and `/change` webhooks invalidate caches and persist the original release payload.

## Troubleshooting

- **Database is not Rails-shaped**: load `db/schema.rb` from Rails first; do not run the old create-from-empty EF chain.
- **Rails schema is too old**: migrate Rails to at least `20260529104000`, then rerun preflight.
- **Port already in use**: local defaults are 3000, 3333, 4010, 4020, 5000, 55431 and 55432.
- **Parity Rails build fails**: remove only `parity/.rails-source` with `git worktree remove parity/.rails-source`, then rerun `./parity.ps1 up`.
- **Notify fails locally**: verify `Notify:BaseUrl` points to the fake and inspect `/_requests`.
- **PDF browser missing in host tests**: install Chromium using the generated Playwright script shown above.
- **PDF browser missing in parity**: rebuild `dotnet-app` with `./parity.ps1 reset`; do not install a browser into a running container.

CI/CD, Azure, Terraform, production deployment, Rails debug/snippet routes, legacy Devise password flows, cross-deployment session continuity, and pixel-perfect rendering are out of scope.
## Authenticated normal module-content evidence

The live parity suite uses the fresh `module-content@example.test` identity to exercise the Module 1 overview, content-index redirect, interruption page, sub-module introduction, topic introduction, and text page. The synthetic module is deliberately outside the live/draft Contentful contract: Rails records `module_overview_page` and redirects its unreleased overview to `/my-modules`, while the .NET demo provider renders the overview. All ordinary content-page headings and paths must match. Revisits prove first-visit timestamps remain stable while `last_page` advances. Database reconciliation compares normalized progress plus Rails-shaped `module_overview_page` and single-shot `module_start` events, and rejects legacy `page_view` or `module_content_page` telemetry.

The authenticated video slice uses the fresh `video-content@example.test` identity and a separate synthetic Contentful `video` entry. It compares stable semantics for the Module 1 YouTube page: heading and body, provider/video ID, transcript details, previous/next navigation, and revisit stability. Rails' intermediate `/content-pages/check-understanding` link is normalized to .NET's direct canonical questionnaire link; volatile embed `origin` values and framework HTML are also excluded. Reconciliation requires one started, incomplete Module 1 progress row whose only visited page and last page are `expert-video`, with no assessment, response, `page_view`, or `module_content_page` writes.
