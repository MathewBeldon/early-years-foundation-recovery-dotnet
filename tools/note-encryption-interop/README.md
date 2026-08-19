# Rails Note.body encryption interop proof

This is the pinned Rails protocol proof and deployment runbook for the
production `Note.body` compatibility adapter. The adapter lives in the
Infrastructure assembly, while this folder contains only sanitized vectors,
the Rails verifier, and test-only keys. The proof does not read Rails
encrypted credentials or application environment secrets.

The reference is Rails commit `ac5467218a49c9de58a32a69d4edc01ce37710cf`
(`v1.5.0`) with Active Record and Active Support `7.2.3.1`, Ruby `3.4.5`.
The pinned local worktree SHA and gem version are checked by the Ruby script.

The committed `test-keys.json` values are explicitly non-production fixtures.
They must never be copied into deployment configuration.

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

## Production configuration and rotation

The application binds only these options:

```text
NoteEncryption:PrimaryKey             <- Rails active_record_encryption.primary_key
NoteEncryption:KeyDerivationSalt      <- Rails active_record_encryption.key_derivation_salt
NoteEncryption:PreviousPrimaryKeys    <- ordered prior primary keys, optional
```

For environment-variable configuration, use the ASP.NET Core names
`NoteEncryption__PrimaryKey`, `NoteEncryption__KeyDerivationSalt`, and
`NoteEncryption__PreviousPrimaryKeys__0` (then `__1`, and so on). The Rails
deterministic key is deliberately not configured because this column is
non-deterministic. Do not give the .NET application `RAILS_MASTER_KEY` and do
not derive these values from it.

During rotation, set the new key as `PrimaryKey` and place the old key first in
`PreviousPrimaryKeys`. Writes use the new key; reads try current then previous
keys. Keep the old key until old rows have been re-encrypted or the rollback
window has expired. A rollback deployment reverses the order: old key as
primary and new key as a previous key. Remove a previous key only after the
retention/rollback decision is complete. There is no schema migration and no
Rails-owned data rewrite in this change.
