require_relative 'boot'
require 'rails/all'

Bundler.require(*Rails.groups)

# Active Record encryption's railtie always calls credentials.dig before applying
# config.active_record.encryption. Migration Compose blanks RAILS_MASTER_KEY so a
# malformed developer key cannot decrypt production credentials.yml.enc; return an
# empty document instead so development.rb encryption keys can take effect.
if ENV['MIGRATION_SKIP_CREDENTIALS'] == 'true'
  module EarlyYearsMigrationCredentialsSkip
    def read
      "{}\n"
    end
  end
  ActiveSupport::EncryptedConfiguration.prepend(EarlyYearsMigrationCredentialsSkip)
end

module EarlyYearsFoundationRecovery
  class Application < Rails::Application
    # Resolve the GOV.UK One Login private key from GOV_ONE_PRIVATE_KEY_PATH, which must point to a readable PEM file.
    def self.gov_one_private_key_from_env
      path = ENV['GOV_ONE_PRIVATE_KEY_PATH']
      File.read(path) if path.present? && File.exist?(path)
    end

    # True when a usable Rails master key is present. A malformed 16-character
    # RAILS_MASTER_KEY in a developer .env must not force credentials.yml.enc decryption.
    def self.credentials_available?
      return false if ENV['MIGRATION_SKIP_CREDENTIALS'] == 'true'

      key = ENV['RAILS_MASTER_KEY'].presence
      return true if key && key.length >= 32
      return true if File.exist?(Rails.root.join('config/master.key'))

      false
    end

    def self.env_or_credentials(name)
      value = ENV[name]
      return value if value.present?
      return yield if block_given? && credentials_available?

      nil
    end

    config.load_defaults 7.0
    # @see ErrorsController
    config.exceptions_app = routes

    config.generators do |g|
      g.test_framework :rspec
    end

    # config.eager_load_paths << Rails.root.join("extras")
    # config.time_zone = ENV.fetch('TZ', 'Europe/London')
    config.service_url = (Rails.env.production? ? 'https://' : 'http://') + ENV.fetch('DOMAIN', 'child-development-training')

    # @see #maintenance?
    # These endpoints are exempt from maintenance page redirection
    config.protected_endpoints = %w[
      /maintenance
      /health
      /change
      /release
      /notify
    ]

    config.middleware.use Grover::Middleware
    config.active_record.yaml_column_permitted_classes = [Symbol]
    config.action_view.sanitized_allowed_tags = %w[p ul li div ol strong].freeze

    # Background Jobs
    config.active_job.queue_adapter               = :que
    config.action_mailer.deliver_later_queue_name = :default
    config.action_mailbox.queues.incineration     = :default
    config.action_mailbox.queues.routing          = :default
    config.active_storage.queues.analysis         = :default
    config.active_storage.queues.purge            = :default

    config.google_cloud_bucket       = ENV.fetch('GOOGLE_CLOUD_BUCKET', '#GOOGLE_CLOUD_BUCKET_env_var_missing')
    config.dashboard_update_interval = ENV.fetch('DASHBOARD_UPDATE_INTERVAL', '0 0 */2 * *') # Midnight every two days

    # Notify
    config.notify_token       = env_or_credentials('GOVUK_NOTIFY_API_KEY') { credentials.notify_api_key }
    # @note Nudge mail must only run once per day
    config.mail_job_interval  = ENV.fetch('MAIL_JOB_INTERVAL', '0 12 * * *') # Noon daily

    config.user_password = ENV.fetch('USER_PASSWORD', 'Str0ngPa$$w0rd12')
    # BOT_TOKEN is retained as a backwards-compatible fallback while environments migrate
    # to dedicated, per-service credentials.
    config.audit_bot_token = ENV.fetch('AUDIT_BOT_TOKEN', ENV['BOT_TOKEN'])
    config.contentful_webhook_token = ENV.fetch('CONTENTFUL_WEBHOOK_TOKEN', ENV['BOT_TOKEN'])
    config.notify_webhook_token = ENV.fetch('NOTIFY_WEBHOOK_TOKEN', ENV['BOT_TOKEN'])
    config.google_analytics_tracking_id = ENV.fetch('TRACKING_ID', '#TRACKING_ID_env_var_missing (google analytics tracking id)')
    config.clarity_tracking_id = ENV.fetch('CLARITY_TRACKING_ID', '#CLARITY_TRACKING_ID_env_var_missing')
    config.utm = config_for(:utm).deep_symbolize_keys

    # Devise — blank .env values (TIMEOUT_IN_MINUTES=) must not become 0 or
    # timeoutable signs every user out on the request after login.
    config.unlock_in_minutes  = ENV.fetch('UNLOCK_IN_MINUTES', '120').presence&.to_i || 120
    config.timeout_in_minutes = ENV.fetch('TIMEOUT_IN_MINUTES', '1440').presence&.to_i || 1440

    # Contentful
    config.contentful_space                   = env_or_credentials('CONTENTFUL_SPACE') { credentials.dig(:contentful, :space) }
    config.contentful_delivery_access_token   = env_or_credentials('CONTENTFUL_DELIVERY_TOKEN') { credentials.dig(:contentful, :delivery_access_token) }
    config.contentful_preview_access_token    = env_or_credentials('CONTENTFUL_PREVIEW_TOKEN') { credentials.dig(:contentful, :preview_access_token) }
    config.contentful_management_access_token = env_or_credentials('CONTENTFUL_MANAGEMENT_TOKEN') { credentials.dig(:contentful, :management_access_token) } # TODO: use service account management token
    config.contentful_environment             = env_or_credentials('CONTENTFUL_ENVIRONMENT') { credentials.dig(:contentful, :environment) }

    # Gov One
    config.gov_one_base_uri    = env_or_credentials('GOV_ONE_BASE_URI') { credentials.dig(:gov_one, :base_uri) }
    config.gov_one_client_id   = env_or_credentials('GOV_ONE_CLIENT_ID') { credentials.dig(:gov_one, :client_id) }
    config.gov_one_private_key = gov_one_private_key_from_env.presence || begin
      credentials.dig(:gov_one, :private_key) if credentials_available?
    end

    # Sentry
    config.sentry_dsn = ENV.fetch('SENTRY_DSN', '#SENTRY_DSN_env_var_missing')

    # Azure Application Insights
    config.application_insights_connection_string = ENV['APPLICATION_INSIGHTS_CONNECTION_STRING']

    config.environment = ENV.fetch('ENVIRONMENT')

    # @return [Boolean]
    def live?
      config.environment.eql?('production')
    end

    # @see ContentfulRails.configuration.enable_preview_domain
    # @see ContentfulModel.use_preview_api
    #
    # @return [Boolean]
    def preview?
      Dry::Types['params.bool'][ENV.fetch('CONTENTFUL_PREVIEW', false)]
    end

    # @return [Boolean] Upload to CSV files to the dashboard
    def dashboard?
      Types::Params::Bool[ENV.fetch('DASHBOARD_UPDATE', true)]
    end

    # @return [Boolean]
    def debug?
      Types::Params::Bool[ENV.fetch('DEBUG', false)]
    end

    # @return [Boolean]
    def maintenance?
      Types::Params::Bool[ENV.fetch('MAINTENANCE', false)]
    end

    # @return [ActiveSupport::TimeWithZone]
    def public_beta_launch_date
      Time.zone.local(2023, 2, 9, 15, 0, 0)
    end

    # @return [ActiveSupport::TimeWithZone]
    def non_linear_launch_date
      Time.zone.local(2023, 7, 10, 15, 43, 0)
    end
  end
end
