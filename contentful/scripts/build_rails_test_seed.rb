# frozen_string_literal: true

require 'fileutils'
require_relative '../lib/rails_test_seed'

builder = ContentfulRailsTestSeed::Builder.new
payload = builder.build
document = JSON.pretty_generate(payload) << "\n"
fingerprint = "#{builder.schema_fingerprint}\n"
output = ContentfulRailsTestSeed::GENERATED_IMPORT
fingerprint_path = File.expand_path('../generated/schema.sha256', __dir__)

if ARGV.include?('--check')
  actual_payload =
    begin
      JSON.parse(File.binread(output)) if File.exist?(output)
    rescue JSON::ParserError
      nil
    end
  abort "Generated Contentful seed is stale; run npm run build in contentful/." unless actual_payload == payload

  actual_fingerprint = File.binread(fingerprint_path).strip if File.exist?(fingerprint_path)
  abort "Contentful schema fingerprint is stale; run npm run build in contentful/." unless actual_fingerprint == builder.schema_fingerprint

  puts "Contentful seed is current (schema SHA-256 #{builder.schema_fingerprint})."
else
  FileUtils.mkdir_p(File.dirname(output))
  File.binwrite(output, document)
  File.binwrite(fingerprint_path, fingerprint)
  puts "Wrote #{output} with #{payload.fetch('entries').count} synthetic entries."
  puts "Schema SHA-256: #{builder.schema_fingerprint}"
end
