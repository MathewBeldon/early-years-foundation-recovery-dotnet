# Test-only encryption material for the parity reference application.
# Production Rails obtains these values from credentials; the disposable parity
# environment uses committed interop fixtures so encrypted notes can be exercised.
if ENV["ENVIRONMENT"] == "parity"
  ActiveRecord::Encryption.configure(
    primary_key: ENV.fetch("ACTIVE_RECORD_ENCRYPTION_PRIMARY_KEY"),
    deterministic_key: ENV.fetch("ACTIVE_RECORD_ENCRYPTION_DETERMINISTIC_KEY"),
    key_derivation_salt: ENV.fetch("ACTIVE_RECORD_ENCRYPTION_KEY_DERIVATION_SALT")
  )
end
