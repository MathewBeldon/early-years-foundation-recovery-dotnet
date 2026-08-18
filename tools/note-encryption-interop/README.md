# Rails Note.body encryption interop proof

This is a test-only protocol proof. It is deliberately isolated under
`tools/note-encryption-interop` and the .NET codec lives only in the unit-test
assembly. It does not change production DI, repositories, entities, secrets,
configuration, migrations, or database behavior.

The reference is Rails commit `ac5467218a49c9de58a32a69d4edc01ce37710cf`
(`v1.5.0`) with Active Record and Active Support `7.2.3.1`, Ruby `3.4.5`.
The pinned local worktree SHA and gem version are checked by the Ruby script.

The committed `test-keys.json` values are explicitly non-production fixtures.
They must never be copied into deployment configuration. The script does not
read Rails encrypted credentials or application environment secrets.

## Protocol proved

For `Note.body` (`encrypts :body`), the proof covers:

- PBKDF2-HMAC-SHA1, 65,536 iterations, 32-byte derived key;
- AES-256-GCM with a random 12-byte IV, 16-byte authentication tag, and empty
  associated data;
- Rails JSON envelope `{ "p": "...", "h": { "iv": "...", "at": "..." } }`;
- strict Base64 for payload, IV, and authentication tag;
- Rails `c: true` compression marker and Zlib DEFLATE payload when the UTF-8
  plaintext is more than 140 bytes;
- UTF-8 short, long/compressed, Unicode/multiline, and empty strings;
- nondeterministic encryption, tampering, wrong-key rejection, and plaintext
  type rejection.

`null` is not an encrypted body value and is intentionally handled outside the
codec, as it is by the Active Record attribute layer.

## Run the proof

The local machine must have the pinned `activerecord` and `activesupport`
7.2.3.1 gems and the .NET 10 SDK. The full Rails application bundle is not
needed. From the repository root:

```powershell
pwsh tools/note-encryption-interop/run.ps1
```

The harness generates ignored sanitized Rails vectors, verifies Rails can read
them, runs the isolated .NET tests, then asks the pinned Rails implementation
to decrypt the .NET-generated vectors. It prints counts and pass/fail status,
never plaintext, ciphertext, keys, or authentication material.

To regenerate only the committed fixture used by ordinary .NET unit tests:

```powershell
ruby tools/note-encryption-interop/rails_note_encryption_interop.rb generate `
  --output tools/note-encryption-interop/fixtures/rails-vectors.json
```

The fixture is sanitized test data and contains no production credential or
learner data. Ciphertexts are random and will change when regenerated.
