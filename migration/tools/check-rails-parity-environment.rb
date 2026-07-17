expected = {
  rails_env: 'parity',
  environment: 'parity',
  cache_classes: true,
  eager_load: true,
  force_ssl: false,
  controller_caching: false,
  contentful_caching: false,
  mail_delivery: :test,
  live: false,
  database_host: 'db',
  database_name: 'early_years_foundation_recovery_development',
}

database = ActiveRecord::Base.connection_db_config
actual = {
  rails_env: Rails.env.to_s,
  environment: Rails.application.config.environment,
  cache_classes: Rails.application.config.cache_classes,
  eager_load: Rails.application.config.eager_load,
  force_ssl: Rails.application.config.force_ssl,
  controller_caching: Rails.application.config.action_controller.perform_caching,
  contentful_caching: ContentfulRails.configuration.perform_caching,
  mail_delivery: Rails.application.config.action_mailer.delivery_method,
  live: Rails.application.live?,
  database_host: database.host,
  database_name: database.database,
}

differences = expected.filter_map do |key, value|
  next if actual[key] == value

  "#{key}: expected #{value.inspect}, got #{actual[key].inspect}"
end

abort "Unsafe Rails parity environment:\n- #{differences.join("\n- ")}" if differences.any?

puts 'Rails parity environment safety check passed.'
