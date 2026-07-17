# Rails session characterization

Status: **Rails reference contract implemented; execution evidence is produced by migration validation.** This report does not approve an ASP.NET Core session design or `KD-SESSION-001`.

## Boundary and representation

Rails owns OIDC initiation, callback, Devise identity, logout and every session-coupled authenticated route. The reference uses an encrypted Rails CookieStore bearer cookie. Exact cookie name, ciphertext and cryptography are runtime representation, while the observed attributes and lifecycle below are contract evidence.

The machine-readable inventory and required evidence cases live in `migration/session-contract.json`. It includes application state (`gov_one_auth_state`, `gov_one_auth_nonce`, `gov_one_redirect_uri`, `id_token`, `registration_review`, `form_nonce`) and framework state (Warden identity and timeout, CSRF and flash).

## Characterized outcomes

| Area | Rails reference outcome | Evidence |
|---|---|---|
| Cookie attributes | The parity/test HTTP cookie is `Path=/`, `HttpOnly`, `SameSite=Lax`, session-scoped and not `Secure`. Absence of `Secure` is environment-derived because parity is deliberately HTTP; production forces SSL. | `spec/requests/users/session_characterization_spec.rb`; real-route `/settings` capture for the separate consent cookie |
| OIDC correlation and rotation | Initiation persists state, nonce and redirect URI; callback rejects missing/mismatched correlation. Successful sign-in changes the encrypted session value and removes the three pre-authentication keys. | `spec/requests/users/openid_connect_callback_spec.rb` |
| Returning identity | A registered user signing in again with the same One Login `sub` reaches `/my-modules` and retains the same application account. | `parity/tests/journeys/session-auth.spec.ts` |
| Email linking | An existing registered account with a matching email and no One Login id is updated with the authenticated `sub`, without creating a second user. | `spec/requests/users/openid_connect_callback_spec.rb`; browser precondition/journey in `session-auth.spec.ts` |
| Error and cancellation | Provider `error` parameters, missing state and state mismatch return to `/` with the generic sign-in alert and do not authenticate. | callback request specs and `parity/tests/journeys/session-auth.spec.ts` |
| Tampered cookie | A modified encrypted cookie is discarded, does not authenticate and does not produce a 500 response. | `spec/requests/users/session_characterization_spec.rb` |
| Timeout | Devise reads `TIMEOUT_IN_MINUTES`; migration validation starts the focused test with a one-minute value and advances the controlled Rails clock beyond it. The next protected request expires the identity, redirects once through its stored `/my-modules` location, then redirects the unauthenticated request to sign-in. | `spec/requests/users/session_characterization_spec.rb`; `bin/migration-validate` |
| RP-initiated logout | The application sends `id_token_hint`, a new `state`, and the browser-origin `/users/sign_out` callback to the One Login logout endpoint. The callback signs the browser out and rotates its cookie. | `parity/tests/journeys/session-auth.spec.ts`; helper specs; request characterization |
| Old-cookie replay | Rails CookieStore has no server-side revocation record: replaying the valid pre-logout cookie within its timeout restores the old Warden identity. This is a security-relevant reference observation, not behaviour automatically approved for the replacement. | `spec/requests/users/session_characterization_spec.rb` |
| Concurrent isolation | Separate browser contexts using different simulator identities retain separate registration state. | `parity/tests/concurrency/interactive-sign-in.spec.ts` |

## Security and design consequences

- Do not claim server-side logout revocation for Rails and do not infer that the replacement must reproduce old-cookie replay. `KD-SESSION-001` remains proposed pending explicit security and architecture approval.
- Production `Secure`, HSTS and proxy/TLS behaviour require deployment/configuration evidence; HTTP parity cannot establish them.
- OIDC initiation and callback must move together. CSRF, `form_nonce`, registration review state and every authenticated route coupled through the session move with the selected owner.
- A future candidate must rerun the observable cases, document key persistence and rotation, and prove rollback without authentication loops.
