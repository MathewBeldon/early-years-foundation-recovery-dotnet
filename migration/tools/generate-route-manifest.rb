# frozen_string_literal: true

require 'digest'
require 'json'

ROOT = Rails.root
MANIFEST_INPUTS = [
  'Gemfile.lock',
  'config/routes.rb',
  *Dir.glob('app/controllers/**/*.rb'),
  *Dir.glob('app/models/**/*.rb'),
  *Dir.glob('spec/**/*_spec.rb'),
].sort.freeze

def source_digest
  digest = Digest::SHA256.new
  MANIFEST_INPUTS.each do |relative_path|
    digest << relative_path << "\0" << File.binread(ROOT.join(relative_path)) << "\0"
  end
  digest.hexdigest
end

def controller_path(controller)
  ROOT.join('app/controllers', "#{controller}_controller.rb")
end

def evidence_range(relative_path, pattern = nil)
  path = ROOT.join(relative_path)
  return nil unless path.file?

  lines = path.readlines
  line_number = pattern ? lines.index { |line| line.match?(pattern) } : 0
  return nil unless line_number

  "#{relative_path}:#{line_number + 1}-#{line_number + 1}"
end

def action_range(controller, action)
  path = controller_path(controller)
  return nil unless path.file?

  lines = path.readlines
  start_index = lines.index { |line| line.match?(/^\s*def\s+#{Regexp.escape(action)}(?:\s|\(|$)/) }
  return nil unless start_index

  next_action = lines.each_index.find do |index|
    index > start_index && lines[index].match?(/^\s*def\s+/)
  end
  finish_index = [next_action ? next_action - 1 : lines.length - 1, start_index + 40].min
  relative = path.relative_path_from(ROOT).to_s.tr('\\', '/')
  "#{relative}:#{start_index + 1}-#{finish_index + 1}"
end

def route_source_reference(route)
  source = route.respond_to?(:source_location) ? route.source_location : nil
  return nil unless source

  source = source.to_s.tr('\\', '/')
  root = ROOT.to_s.tr('\\', '/')
  source.sub!("#{root}/", '')
  source.match?(/:\d+$/) ? "#{source}-#{source.split(':').last}" : source
end

def relevant_controller_sources(controller)
  sources = [controller_path(controller)]
  sources << ROOT.join('app/controllers/registration/base_controller.rb') if controller.start_with?('registration/')
  sources << ROOT.join('app/controllers/webhook_controller.rb') if %w[notify release].include?(controller)
  sources.select(&:file?).uniq
end

def before_action_applies?(line, action)
  only_actions = line[/only:\s*%i\[([^\]]+)\]/, 1]&.split ||
                 line.scan(/only:\s*:(\w+)/).flatten
  return false if only_actions.any? && !only_actions.include?(action)

  excluded_actions = line[/except:\s*%i\[([^\]]+)\]/, 1]&.split ||
                     line.scan(/except:\s*:(\w+)/).flatten
  !excluded_actions.include?(action)
end

def authentication(controller, action)
  sources = relevant_controller_sources(controller)
  matches = sources.flat_map do |path|
    path.readlines.each_with_index.filter_map do |line, index|
      next unless line.include?('before_action') && line.include?('authenticate')
      next unless before_action_applies?(line, action)

      [line, "#{path.relative_path_from(ROOT).to_s.tr('\\', '/')}:#{index + 1}-#{index + 1}"]
    end
  end
  text = matches.map(&:first).join(' ')
  requirement =
    if text.include?('authenticate_registered_user')
      'registered-user'
    elsif text.include?('authenticate_user')
      'user'
    elsif text.include?('authenticate_webhook')
      'webhook-token'
    elsif text.include?('authenticate_audit_bot')
      'bot-token'
    elsif controller == 'pages'
      'conditional-registered-user'
    else
      'anonymous'
    end
  [requirement, matches.map(&:last)]
end

def authorisation(controller, authentication_requirement, source)
  return 'webhook-secret' if authentication_requirement == 'webhook-token'
  return 'bot-token' if authentication_requirement == 'bot-token'
  return 'current-user-resource' if source.include?('current_user')
  return 'route-content-restriction' if controller == 'pages'

  'none-observed'
end

MODEL_NAMES = Dir.glob(ROOT.join('app/models/**/*.rb')).map do |path|
  File.basename(path, '.rb').split('_').map(&:capitalize).join
end.uniq.sort.freeze

def models_for(source)
  models = MODEL_NAMES.select { |name| source.match?(/\b#{Regexp.escape(name)}\b/) }
  models << 'User' if source.include?('current_user')
  models.uniq.sort
end

def external_services(source)
  services = []
  services << 'Contentful' if source.match?(/Contentful|prepare_cms|Training::(?:Module|Page|Question|Video)/)
  services << 'GOV.UK One Login' if source.match?(/GOV_ONE|GovOne|openid_connect|omniauth/)
  services << 'GOV.UK Notify' if source.match?(/Notify|deliver_(?:now|later)/)
  services << 'Google Cloud Storage' if source.match?(/GOOGLE_CLOUD|Google::Cloud/)
  services << 'Chromium/PDF' if source.match?(/Grover|Puppeteer|\.pdf/)
  services.uniq
end

def background_jobs(source)
  source.scan(/\b([A-Z][A-Za-z0-9_:]*Job)\b/).flatten.uniq.sort
end

def response_type(controller, action, source)
  return 'framework-response' unless controller_path(controller).file?

  view_glob = ROOT.join('app/views', controller, "#{action}.*").to_s
  views = Dir.glob(view_glob).map { |path| Pathname(path).relative_path_from(ROOT).to_s.tr('\\', '/') }
  return views.first unless views.empty?
  return 'redirect' if source.match?(/redirect_to/)
  return 'json' if source.match?(/render\s+json:/)
  return 'dynamic-template' if source.match?(/render\s+template|render\s+[^'\"]*template/)
  'implicit-or-inline-response'
end

SPEC_FILES = Dir.glob(ROOT.join('spec/**/*_spec.rb')).freeze

def spec_coverage(controller, action, path, route_name, ownership)
  return [] unless ownership == 'application'

  literal_path = path.sub('(.:format)', '')
  tokens = []
  tokens << "#{route_name}_path" if route_name
  tokens << literal_path if literal_path.start_with?('/') && !literal_path.match?(/[:*]/)
  controller_spec = "spec/controllers/#{controller}_controller_spec.rb"
  request_basename = controller.split('/').last.to_s
  SPEC_FILES.filter_map do |spec_path|
    relative = Pathname(spec_path).relative_path_from(ROOT).to_s.tr('\\', '/')
    lines = File.readlines(spec_path)
    index = if relative == controller_spec
              lines.index { |line| line.match?(/#{Regexp.escape(action)}/) } || 0
            elsif relative.start_with?('spec/requests/') && File.basename(relative).include?(request_basename.sub(/s$/, ''))
              lines.index { |line| tokens.any? { |token| line.include?(token) } } || 0
            else
              lines.index { |line| tokens.any? { |token| line.include?(token) } }
            end
    next unless index

    "#{relative}:#{index + 1}-#{index + 1}"
  end.first(8)
end

routes = Rails.application.routes.routes.each_with_index.map do |route, index|
  controller = route.defaults[:controller].to_s
  action = route.defaults[:action].to_s
  path = route.path.spec.to_s
  sources = relevant_controller_sources(controller)
  source = sources.map { |file| file.read }.join("\n")
  action_source = action_range(controller, action)
  action_text = if action_source
                  range = action_source.split(':').last.split('-').map(&:to_i)
                  controller_path(controller).readlines[(range.first - 1)..(range.last - 1)].join
                else
                  source
                end
  auth_requirement, auth_evidence = authentication(controller, action)
  models_read = models_for(action_text)
  mutating = route.verb.to_s.match?(/POST|PUT|PATCH|DELETE/) || action.match?(/create|update|destroy|close|release/)
  models_written = mutating && action_text.match?(/update|save|create|destroy|delete|insert|close_account/) ? models_read : []
  jobs = background_jobs(action_text)
  services = external_services("#{source}\n#{action_text}")
  ownership = controller_path(controller).file? ? 'application' : 'framework'
  route_evidence = route_source_reference(route)
  route_evidence ||= if ownership == 'application'
                       "config/routes.rb:1-#{File.readlines(ROOT.join('config/routes.rb')).length}"
                     else
                       evidence_range('Gemfile.lock', /^    rails \(/)
                     end
  evidence = [route_evidence, action_source, *auth_evidence].compact.uniq
  unresolved = []
  unresolved << 'Confirm runtime route constraints and environment availability.' if route.constraints.any?
  unresolved << 'Framework-owned route requires an explicit retain/remove decision.' if ownership == 'framework'
  unresolved << 'Static analysis found no action body; observe the runtime response before migration.' unless action_source || ownership == 'framework'
  unresolved << 'Confirm indirect model, callback, and concern interactions with an observed request.' if ownership == 'application'

  {
    id: format('rails-route-%03d', index + 1),
    ownership: ownership,
    name: route.name&.to_s,
    methods: route.verb.to_s.empty? ? ['ANY'] : route.verb.to_s.split('|'),
    path: path,
    railsController: controller,
    railsAction: action,
    authenticationRequirement: auth_requirement,
    authorisationRequirement: authorisation(controller, auth_requirement, source),
    primaryViewOrResponseType: response_type(controller, action, action_text),
    modelsRead: models_read,
    modelsWritten: models_written,
    externalServices: services,
    backgroundJobs: jobs,
    existingSpecCoverage: spec_coverage(controller, action, path, route.name&.to_s, ownership),
    migrationState: 'rails-reference',
    evidenceReferences: evidence,
    confidence: ownership == 'application' && action_source ? 'medium' : 'low',
    unresolvedQuestions: unresolved,
  }
end

manifest = {
  schemaVersion: 1,
  sourceDigest: source_digest,
  generatedInEnvironment: Rails.env,
  sourceOfTruth: 'Rails.application.routes.routes plus conservative static controller/spec analysis',
  routeCount: routes.length,
  applicationRouteCount: routes.count { |route| route[:ownership] == 'application' },
  frameworkRouteCount: routes.count { |route| route[:ownership] == 'framework' },
  routes: routes,
}

output = ROOT.join('migration/route-manifest.json')
output.dirname.mkpath
output.write("#{JSON.pretty_generate(manifest)}\n")
puts "Wrote #{routes.length} routes to #{output.relative_path_from(ROOT)}"
