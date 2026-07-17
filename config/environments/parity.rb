require_relative 'production'

# Production-like execution for local/CI behavioural parity. This environment keeps
# production code loading and logging characteristics without production credentials,
# HTTPS, external delivery, or stateful application/Contentful caches.
Rails.application.configure do
  config.require_master_key = false
  config.force_ssl = false

  config.hosts.concat %w[app localhost]
  ENV.fetch('RAILS_ADDITIONAL_HOSTS', '').split(',').map(&:strip).reject(&:empty?).each do |host|
    config.hosts << host
  end

  config.public_file_server.enabled = true
  config.assets.compile = true

  config.action_controller.perform_caching = false
  config.cache_store = :null_store
  config.action_mailer.perform_caching = false
  config.action_mailer.raise_delivery_errors = false
  config.action_mailer.delivery_method = :test

  config.server_timing = false
  config.active_record.verbose_query_logs = false

  # Parity never reads production encryption credentials. These deterministic values
  # are local-only and match the migration stack's synthetic-data boundary.
  config.active_record.encryption.primary_key = ENV.fetch(
    'ACTIVE_RECORD_ENCRYPTION_PRIMARY_KEY',
    'parity-primary-key-32-characters!',
  )
  config.active_record.encryption.deterministic_key = ENV.fetch(
    'ACTIVE_RECORD_ENCRYPTION_DETERMINISTIC_KEY',
    'parity-deterministic-key-32-ch!',
  )
  config.active_record.encryption.key_derivation_salt = ENV.fetch(
    'ACTIVE_RECORD_ENCRYPTION_KEY_DERIVATION_SALT',
    'parity-key-derivation-salt-32-ch',
  )
end
