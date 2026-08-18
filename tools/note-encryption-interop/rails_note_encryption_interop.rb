# frozen_string_literal: true

# Test-only Rails Active Record encryption protocol proof.
#
# This intentionally loads only the pinned Active Record/Active Support gems;
# it does not boot the application or read Rails credentials.

require "base64"
require "json"
require "openssl"
require "optparse"
require "open3"
require "pathname"
require "zlib"

gem "activesupport", "7.2.3.1"
gem "activerecord", "7.2.3.1"
require "active_record"

ROOT = Pathname(__dir__).join("../..").realpath
RAILS_SOURCE = ROOT.join("parity/.rails-source")
EXPECTED_RAILS_COMMIT = "ac5467218a49c9de58a32a69d4edc01ce37710cf"
EXPECTED_RAILS_VERSION = "7.2.3.1"
KEYS_PATH = ROOT.join("tools/note-encryption-interop/test-keys.json")
CASES_PATH = ROOT.join("tools/note-encryption-interop/vector-cases.json")
DEFAULT_VECTORS_PATH = ROOT.join("tools/note-encryption-interop/fixtures/rails-vectors.json")

def fail(message)
  warn message
  exit 1
end

def assert(condition, message)
  fail message unless condition
end

def assert_raises(message)
  yield
  fail message
rescue StandardError
  true
end

def run_git(*arguments)
  output, status = Open3.capture2("git", "-C", RAILS_SOURCE.to_s, *arguments)
  fail "Could not inspect pinned Rails worktree" unless status.success?
  output.strip
end

assert RAILS_SOURCE.directory?, "parity/.rails-source is required for the pinned Rails proof"
assert run_git("rev-parse", "HEAD") == EXPECTED_RAILS_COMMIT,
       "Pinned Rails worktree is not #{EXPECTED_RAILS_COMMIT}"
assert ActiveRecord::VERSION::STRING == EXPECTED_RAILS_VERSION,
       "Loaded Active Record #{ActiveRecord::VERSION::STRING}, expected #{EXPECTED_RAILS_VERSION}"

keys = JSON.parse(KEYS_PATH.read)
CASES = JSON.parse(CASES_PATH.read).freeze

ActiveRecord::Encryption.configure(
  primary_key: keys.fetch("primaryKey"),
  deterministic_key: keys.fetch("deterministicKey"),
  key_derivation_salt: keys.fetch("keyDerivationSalt")
)

ENCRYPTOR = ActiveRecord::Encryption.encryptor

def encrypt(plaintext)
  ENCRYPTOR.encrypt(plaintext)
end

def decrypt(ciphertext, key_provider: nil)
  options = key_provider ? { key_provider: key_provider } : {}
  ENCRYPTOR.decrypt(ciphertext, **options)
end

def vector_for(test_case)
  plaintext = test_case.fetch("plaintext")
  ciphertext = encrypt(plaintext)
  parsed = JSON.parse(ciphertext)
  headers = parsed.fetch("h")

  {
    "name" => test_case.fetch("name"),
    "plaintext" => plaintext,
    "plaintextByteLength" => plaintext.encode(Encoding::UTF_8).bytesize,
    "compressed" => headers.fetch("c", false),
    "ciphertext" => ciphertext
  }
end

def assert_vector_shape(vector)
  parsed = JSON.parse(vector.fetch("ciphertext"))
  assert parsed.keys.sort == ["h", "p"], "Rails envelope has an unexpected top-level shape"
  assert parsed.fetch("p").is_a?(String), "Rails payload is not a Base64 string"
  headers = parsed.fetch("h")
  assert headers.fetch("iv").is_a?(String), "Rails IV is not a Base64 string"
  assert headers.fetch("at").is_a?(String), "Rails authentication tag is not a Base64 string"
  assert Base64.strict_decode64(headers.fetch("iv")).bytesize == 12, "Rails IV is not 12 bytes"
  assert Base64.strict_decode64(headers.fetch("at")).bytesize == 16, "Rails tag is not 16 bytes"
  assert vector.fetch("compressed") == headers.fetch("c", false), "Compression metadata is inconsistent"
end

def tamper_payload(ciphertext)
  parsed = JSON.parse(ciphertext)
  payload = Base64.strict_decode64(parsed.fetch("p"))
  if payload.empty?
    iv = Base64.strict_decode64(parsed.fetch("h").fetch("iv"))
    iv.setbyte(0, iv.getbyte(0) ^ 1)
    parsed.fetch("h")["iv"] = Base64.strict_encode64(iv)
  else
    payload.setbyte(0, payload.getbyte(0) ^ 1)
    parsed["p"] = Base64.strict_encode64(payload)
  end
  JSON.generate(parsed)
end

def tamper_tag(ciphertext)
  parsed = JSON.parse(ciphertext)
  tag = Base64.strict_decode64(parsed.fetch("h").fetch("at"))
  tag.setbyte(0, tag.getbyte(0) ^ 1)
  parsed.fetch("h")["at"] = Base64.strict_encode64(tag)
  JSON.generate(parsed)
end

def verify_rails_vectors(vectors)
  assert vectors.fetch("railsVersion") == EXPECTED_RAILS_VERSION, "Vector Rails version is not pinned"
  assert vectors.fetch("railsCommit") == EXPECTED_RAILS_COMMIT, "Vector Rails commit is not pinned"
  entries = vectors.fetch("vectors")
  entries.each do |vector|
    assert_vector_shape(vector)
    assert decrypt(vector.fetch("ciphertext")) == vector.fetch("plaintext"), "Rails self-decryption failed"
  end

  sample = entries.find { |entry| entry.fetch("plaintext") == "A short sanitized note." }
  assert sample, "The short vector is missing"
  assert encrypt(sample.fetch("plaintext")) != encrypt(sample.fetch("plaintext")), "Rails encryption was deterministic"
  assert_raises("Rails accepted a tampered payload") { decrypt(tamper_payload(sample.fetch("ciphertext"))) }
  assert_raises("Rails accepted a tampered authentication tag") { decrypt(tamper_tag(sample.fetch("ciphertext"))) }

  wrong_provider = ActiveRecord::Encryption::DerivedSecretKeyProvider.new(
    "wrong-test-only-key",
    key_generator: ActiveRecord::Encryption::KeyGenerator.new
  )
  assert_raises("Rails accepted a ciphertext under the wrong key") do
    decrypt(sample.fetch("ciphertext"), key_provider: wrong_provider)
  end
  assert_raises("Rails accepted nil as an encryptable plaintext") { encrypt(nil) }

  exact_threshold = encrypt("x" * 140)
  over_threshold = encrypt("x" * 141)
  assert !JSON.parse(exact_threshold).fetch("h").key?("c"), "Rails compressed a 140-byte plaintext"
  assert JSON.parse(over_threshold).fetch("h").fetch("c") == true, "Rails did not compress a 141-byte plaintext"
end

def generate_vectors(output_path)
  vectors = {
    "protocol" => "Active Record encryption JSON envelope",
    "railsVersion" => EXPECTED_RAILS_VERSION,
    "railsCommit" => EXPECTED_RAILS_COMMIT,
    "cipher" => "aes-256-gcm",
    "keyDerivation" => "PBKDF2-HMAC-SHA1",
    "iterations" => 65_536,
    "keyBytes" => 32,
    "associatedData" => "",
    "compressionThresholdBytesExclusive" => 140,
    "vectors" => CASES.map { |test_case| vector_for(test_case) }
  }
  verify_rails_vectors(vectors)
  output_path.dirname.mkpath
  output_path.write(JSON.pretty_generate(vectors) + "\n")
  puts "Rails vectors generated: #{vectors.fetch("vectors").length}"
end

def verify_dotnet_vectors(input_path)
  document = JSON.parse(input_path.read)
  assert document.fetch("railsVersion") == EXPECTED_RAILS_VERSION, ".NET vector Rails version is not pinned"
  assert document.fetch("railsCommit") == EXPECTED_RAILS_COMMIT, ".NET vector Rails commit is not pinned"
  vectors = document.fetch("vectors")
  vectors.each do |vector|
    assert decrypt(vector.fetch("ciphertext")) == vector.fetch("plaintext"), ".NET ciphertext did not decrypt in Rails"
  end
  puts "Rails decrypted .NET vectors: #{vectors.length}"
end

command = ARGV.shift
case command
when "generate"
  options = { output: DEFAULT_VECTORS_PATH }
  OptionParser.new { |parser| parser.on("--output PATH") { |path| options[:output] = Pathname(path).expand_path } }.parse!(ARGV)
  generate_vectors(options.fetch(:output))
when "verify-rails"
  options = { vectors: DEFAULT_VECTORS_PATH }
  OptionParser.new { |parser| parser.on("--vectors PATH") { |path| options[:vectors] = Pathname(path).realpath } }.parse!(ARGV)
  verify_rails_vectors(JSON.parse(options.fetch(:vectors).read))
  puts "Rails encryption proof passed"
when "verify-dotnet"
  options = {}
  OptionParser.new { |parser| parser.on("--input PATH") { |path| options[:input] = Pathname(path).realpath } }.parse!(ARGV)
  fail "--input is required" unless options[:input]
  verify_dotnet_vectors(options.fetch(:input))
else
  fail "Usage: ruby rails_note_encryption_interop.rb generate|verify-rails|verify-dotnet [options]"
end
