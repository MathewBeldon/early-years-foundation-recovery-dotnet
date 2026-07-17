# Session ownership and characterization plan

Status: **proposed pre-migration work**. This plan does not approve a session design or authorize an authenticated route migration.

## Objective

Pin the Rails session contract before ASP.NET Core session design begins, then require one explicitly approved session owner for the complete boundary. Rails and ASP.NET Core session representations are expected to differ; observable authentication and session security semantics are not allowed to drift without an approved known difference.

## Non-negotiable boundary rules

- OIDC initiation and callback have one owner because the pre-authentication session carries correlation state before a user identity exists.
- An authenticated journey never alternates between Rails and ASP.NET Core. All routes coupled through session state move as one reviewed boundary.
- Session-coupled workflow behaviour moves with its owner. In particular, summative duplicate-submit protection depends on `form_nonce`; it cannot be split from the questionnaire submission boundary.
- The gateway must not accidentally expose an incompatible session to the other application.
- Anonymous attribution cookies and Ahoy visit state are a separate ownership decision. They must not be silently folded into, or assumed to follow, the authenticated session decision.
- No Rails signing/encryption secret is shared merely to make ASP.NET Core accept Rails cookies.

## Rails session inventory to confirm

The current source inventory establishes these application-owned purposes:

| Purpose | Rails state | Evidence |
|---|---|---|
| OIDC state correlation | `gov_one_auth_state` | `app/helpers/authentication_helper.rb`; `app/controllers/users/omniauth_callbacks_controller.rb` |
| OIDC nonce correlation | `gov_one_auth_nonce` | `app/helpers/authentication_helper.rb`; `app/controllers/users/omniauth_callbacks_controller.rb` |
| OIDC callback URI | `gov_one_redirect_uri` | `app/helpers/authentication_helper.rb`; `app/controllers/users/omniauth_callbacks_controller.rb` |
| RP-initiated logout | `id_token` | `app/helpers/authentication_helper.rb`; `app/helpers/link_helper.rb` |
| Registration review mode | `registration_review` | `app/controllers/registration/base_controller.rb` |
| Summative duplicate-submit protection | `form_nonce` | `app/controllers/training/questions_controller.rb`; `app/controllers/training/responses_controller.rb` |

This is a minimum inventory, not a declaration of completeness. Characterization must also identify framework-owned Devise/Warden identity and timeout state, Rails CSRF state, flash state, and any other session reads/writes found through static and executed evidence. The replacement preserves the purposes and observable invariants, not Rails key names.

## Contract split

### Representation allowed to differ after approval

- cookie name and encoded value;
- signing/encryption format and key derivation;
- runtime-specific storage representation; and
- key-management implementation.

### Observable semantics that require evidence

- a valid OIDC flow creates a session for exactly the authenticated subject;
- the pre-authentication session is rotated on successful authentication;
- returning users retain the correct application identity and state;
- concurrent identities remain isolated without assuming device-bound bearer cookies;
- malformed, tampered and foreign-framework cookies do not authenticate or cause a server error;
- idle and absolute expiry behaviour is enforced by the session owner, not only by browser deletion;
- logout follows the complete GOV.UK One Login chain and has explicitly characterized invalidation and old-cookie replay behaviour;
- CSRF tokens and workflow nonces are bound to the correct session and fail across sessions or after invalidation;
- cookie `Path`, `Domain`, `HttpOnly`, `Secure`, `SameSite`, persistence, expiry and deletion attributes are deliberate; and
- cutover and rollback never create an authentication loop or ambiguous identity. Forced reauthentication on rollback must be explicitly approved if unavoidable.

A copied valid cookie is a bearer credential and is expected to authenticate its holder unless device binding is deliberately designed. The contract proves isolation, rotation, tamper rejection, expiry and revocation; it does not claim general non-transferability.

## Work plan

### Phase 1: Rails characterization before .NET session design

1. Complete the static inventory of application and framework session purposes.
2. Add focused Rails/browser characterization for:
   - cookie attributes and deletion attributes;
   - OIDC initiation/callback correlation and pre/post-login rotation;
   - returning-user sign-in and email-match account linking;
   - malformed state, nonce, token and cancellation/error paths;
   - tampered-cookie rejection;
   - shortened-timeout expiry using a controlled Rails clock/configuration rather than a long browser wait;
   - the full RP-initiated logout redirect chain, current GET `/users/sign_out` behaviour, and replay of the pre-logout cookie;
   - cross-session CSRF and `form_nonce` rejection; and
   - concurrent identity isolation, retaining the existing interactive-sign-in evidence.
3. Tag each cookie observation as intrinsic behaviour or environment-derived configuration. The HTTP parity environment has `force_ssl = false`; production `Secure`, HSTS and forwarded-header expectations require configuration and deployment evidence rather than copying parity observations.
4. Record results in a source-controlled Rails characterization report. Do not infer client-side-only logout or replay behaviour solely from the default CookieStore; execute it.
5. Add machine enforcement that the required characterization cases and report exist. Prefer a small session-contract manifest rather than overloading the Rails system-spec coverage manifest.

### Phase 2: ownership and security decision before implementation

1. Choose the single owner for the complete pre-authentication and authenticated boundary.
2. Document cookie role, timeout policy, rotation, server-side revocation, CSRF, key storage/rotation/retirement, proxy/TLS handling, logging redaction and session cleanup.
3. Threat-model cutover, deployment restart, key loss/rotation, logout and rollback.
4. Review `KD-SESSION-001` against executed Rails evidence. It remains proposed until named security and architecture approvers accept the replacement behaviour.
5. Keep R-01 and R-02 open until the decision and evidence are approved.

### Phase 3: candidate and routing evidence at migration time

1. Run the same observable contract against the approved ASP.NET Core owner.
2. Demonstrate that Rails cookies are rejected or ignored by ASP.NET Core and that Rails cannot interpret an ASP.NET Core cookie as authenticated.
3. Demonstrate key persistence and rotation across deployment and instance changes.
4. Use gateway provenance/origin evidence to prove that the complete authenticated journey has one owner.
5. Demonstrate rollback. If existing ASP.NET Core sessions require reauthentication after rollback to Rails, verify the approved user experience and absence of redirect loops.

## Acceptance-gate mapping

| Gate | Required session evidence |
|---|---|
| 1. Inventory | Complete boundary, sole owner, application/framework session purposes and anonymous-cookie exclusions |
| 2. Contract | Cookie roles, attributes, expiry/deletion and observable lifecycle semantics; approved representation differences only |
| 7. Security | Authentication, rotation, tamper rejection, timeout, logout/replay, CSRF, nonce behaviour and white-box security review |
| 8. Operations | Key persistence/rotation, multi-instance behaviour, deployment and rollback session outcome |
| 9. Boundary | OIDC initiation/callback and every session-coupled authenticated route remain on one owner |
| 10. Review | R-01/R-02 disposition, approved known difference, linked evidence and explicit rollback approval |

## Exit criteria before an authenticated migration may start

- Rails characterization is complete, reviewed and machine-enforced.
- The complete boundary and every session purpose are inventoried.
- One session owner and its operational/security model are approved.
- `KD-SESSION-001` is either approved with precise evidence and tests or removed because the selected design makes it unnecessary.
- Gate 2 distinguishes semantic cookie parity from runtime representation.
- R-01 and R-02 have linked evidence appropriate to their current status.
- No authenticated YARP route is changed until the applicable acceptance evidence is approved.

## Explicitly separate future decision

Guest course feedback and Ahoy attribution use anonymous visit/cookie state. If that boundary moves independently, create a separate ownership record covering visit attribution and the `course_feedback` cookie rather than extending `KD-SESSION-001` implicitly.
