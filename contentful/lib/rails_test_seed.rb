# frozen_string_literal: true

require 'digest'
require 'json'

module ContentfulRailsTestSeed
  LOCALE = 'en-US'
  SOURCE_EXPORT = File.expand_path('../contentful-export/content-types.json', __dir__)
  GENERATED_IMPORT = File.expand_path('../generated/rails-test-import.json', __dir__)

  class Builder
    attr_reader :source_export

    def initialize(source_export: SOURCE_EXPORT)
      @source_export = JSON.parse(File.read(source_export))
      @entries = []
    end

    def build
      @entries = []
      validate_source_export!
      build_shared_entries
      %w[alpha bravo charlie].each { |name| build_module(name) }
      build_module('delta', pages: false)
      build_course
      build_static_pages
      build_resource
      build_user_settings

      source_export.merge(
        'assets' => [thumbnail_asset],
        'entries' => @entries,
      )
    end

    def schema_fingerprint
      schema = source_export.slice('contentTypes', 'editorInterfaces', 'locales', 'tags')
      Digest::SHA256.hexdigest(JSON.generate(deep_sort(schema)))
    end

  private

    MODULES = {
      'alpha' => ['First Training Module', 1],
      'bravo' => ['Second Training Module', 2],
      'charlie' => ['Third Training Module', 3],
      'delta' => ['Fourth Training Module', 4],
    }.freeze

    FEEDBACK_OPTIONS = (1..5).map { |number| ["Option #{number}"] }.freeze

    FEEDBACK = [
      ['feedback-radio-only', 'Feedback radio buttons only', { 'answers' => [['Option 1'], ['Option 2']], 'multi_select' => false }],
      ['feedback-checkbox-only', 'Feedback checkboxes only', { 'answers' => [['Option 1'], ['Option 2'], ['Option 3']], 'multi_select' => true }],
      ['feedback-textarea-only', 'Feedback textarea only', { 'answers' => [], 'more' => true, 'multi_select' => false }],
      ['feedback-radio-other-more', 'Feedback radio buttons with large other', { 'answers' => FEEDBACK_OPTIONS, 'other' => 'Other', 'more' => true, 'multi_select' => false }],
      ['feedback-checkbox-other-more', 'Feedback checkbox with large other', { 'answers' => FEEDBACK_OPTIONS, 'other' => 'Other', 'more' => true, 'multi_select' => true }],
      ['feedback-radio-more', 'Feedback radio buttons with additional reasons', { 'answers' => FEEDBACK_OPTIONS, 'more' => true, 'multi_select' => false }],
      ['feedback-checkbox-other-or', 'Feedback checkboxes with Other and Or', { 'answers' => FEEDBACK_OPTIONS, 'other' => 'Other', 'or' => 'None of the above', 'multi_select' => true }],
      ['feedback-skippable', 'Skippable', { 'answers' => [%w[Yes], %w[No]], 'skippable' => true, 'multi_select' => false }],
    ].freeze

    SETTINGS = [
      ['childminder_agency', 'Childminder as part of an agency', true, 'childminder'],
      ['childminder_independent', 'Independent childminder', true, 'childminder'],
      ['nursery_local_authority', 'Local authority maintained nursery school', true, 'other'],
      ['nursery_private', 'Private nursery', true, 'other'],
      ['nurser_independent', 'Independent nursery', true, 'other'],
      ['daycare', 'Daycare', true, 'other'],
      ['preschool', 'Preschool', true, 'other'],
      ['playgroup', 'Playgroup', true, 'other'],
      ['childcare_centre', 'Childcare centre', true, 'other'],
      ['nursery_attached_to_school', 'Nursery attached to a school', true, 'other'],
      ['free_school', 'Free School', true, 'other'],
      ['special_school', 'Special school', true, 'other'],
      ['academy', 'Academy', true, 'other'],
      ['training_provider', 'Training provider', false, 'none'],
      ['central_government', 'Central government', false, 'none'],
      ['department_for_education', 'Department for Education', false, 'none'],
      ['local_authority', 'Local authority', true, 'none'],
    ].freeze

    STATIC_PAGES = [
      ['accessibility-statement', 'Accessibility statement', true],
      ['maintenance', 'Sorry, the service is unavailable', false],
      ['new-registration', 'Update your registration details', false],
      ['other-problems-signing-in', 'Other problems signing in', false],
      ['promotional-materials', 'Promotional materials', true],
      ['sitemap', 'Sitemap', true],
      ['terms-and-conditions', 'Terms and conditions', true],
      ['wifi-and-data', 'Free internet, wifi and data resources', false],
      ['whats-new', "What's new in the training", false],
    ].freeze

    def validate_source_export!
      expected = %w[static trainingModule page question video resource userSetting course helpPage helpResource]
      actual = source_export.fetch('contentTypes').map { |type| type.dig('sys', 'id') }
      missing = expected - actual
      raise "Schema export is missing content types: #{missing.join(', ')}" if missing.any?
      raise 'Schema export must not contain entries or assets' if source_export.key?('entries') || source_export.key?('assets')
    end

    def build_shared_entries
      FEEDBACK.each do |name, body, options|
        add_entry('question', "shared:#{name}", question_fields(name, 'feedback', body, **options))
      end
    end

    def build_module(name, pages: true)
      title, position = MODULES.fetch(name)
      page_links = pages ? build_module_pages(name) : []
      fields = {
        'title' => title,
        'name' => name,
        'position' => position,
        'image' => asset_link(thumbnail_id),
        'upcoming' => "Upcoming #{name} module",
        'about' => "About the #{name} module",
        'description' => name == 'alpha' ? 'first module description' : "Synthetic description for #{name}.\n\nThis content exists only for automated tests.",
        'outcomes' => "- first outcome\n- second outcome",
        'criteria' => "- first criterion\n- second criterion",
        'duration' => 3,
        'short_description' => "Synthetic #{name} module description",
        'objective' => "Synthetic #{name} module objective",
        'summative_threshold' => 70,
      }
      fields['pages'] = page_links.map { |id| entry_link(id) } if pages
      fields['depends_on'] = entry_link(entry_id('trainingModule', dependency_for(name))) if dependency_for(name)
      add_entry('trainingModule', name, fields)
    end

    def build_module_pages(scope)
      ids = []
      add = lambda do |type, name, fields|
        key = "#{scope}:#{name}"
        add_entry(type, key, fields)
        ids << entry_id(type, key)
      end

      add.call('page', 'what-to-expect', page_fields('what-to-expect', 'interruption_page', 'What to expect during the training'))
      add.call('page', '1-1', page_fields('1-1', 'sub_module_intro', 'The first submodule'))
      add.call('page', '1-1-1', page_fields('1-1-1', 'topic_intro', '1-1-1'))
      if scope == 'charlie'
        add.call('page', '1-1-1-1', page_fields('1-1-1-1', 'text_page', 'text_page'))
      end
      unless scope == 'bravo'
        add.call('page', '1-1-2', page_fields('1-1-2', 'topic_intro', '1-1-2'))
        add.call('page', '1-1-3', page_fields('1-1-3', 'topic_intro', '1-1-3'))
        add.call('page', '1-1-3-1', page_fields('1-1-3-1', 'text_page', '1-1-3-1', notes: true))
      end
      add.call('page', '1-1-4', page_fields('1-1-4', 'topic_intro', '1-1-4'))
      add.call('question', '1-1-4-1', question_fields('1-1-4-1', 'formative', 'Question One - Select from following', answers: [['Correct answer 1', true], ['Wrong answer 1']]))
      add.call('page', '1-2', page_fields('1-2', 'sub_module_intro', 'The second submodule'))
      add.call('page', '1-2-1', page_fields('1-2-1', 'topic_intro', '1-2-1'))
      add.call('question', '1-2-1-1', question_fields('1-2-1-1', 'formative', 'Question Two - Select from following', answers: [['Correct answer 1', true], ['Wrong answer'], ['Correct answer 2', true]]))
      add.call('video', '1-2-1-2', video_fields('1-2-1-2'))
      formative = if scope == 'bravo'
                    question_fields('1-2-1-3', 'formative', 'Binary choice', answers: [['True', true], %w[False]])
                  else
                    question_fields('1-2-1-3', 'formative', 'Question Three - Select from following', answers: [['Correct answer 1', true], ['Wrong answer 1'], ['Correct answer 2', true], ['Wrong answer 2']])
                  end
      add.call('question', '1-2-1-3', formative)
      add.call('page', '1-3', page_fields('1-3', 'summary_intro', 'Summary and next steps'))
      add.call('page', '1-3-1', page_fields('1-3-1', 'recap_page', 'Recap'))
      add.call('page', '1-3-2', page_fields(
                                  '1-3-2',
                                  'assessment_intro',
                                  'End of module test',
                                  body: <<~MARKDOWN.strip,
                                    This end of module test is here to revisit what you have learned

                                    If you do not score 70%, you will be able to see which questions you got wrong.
                                  MARKDOWN
                                ))
      (1..10).each do |number|
        name = "1-3-2-#{number}"
        add.call('question', name, summative_fields(scope, name, number))
      end
      add.call('page', '1-3-2-11', page_fields('1-3-2-11', 'assessment_results', 'Assessment results'))
      add.call('page', '1-3-3', page_fields(
                                  '1-3-3',
                                  'confidence_intro',
                                  'Reflect on your learning',
                                  body: 'To help DfE to measure our impact, please answer the following questions.',
                                ))
      (1..4).each do |number|
        name = "1-3-3-#{number}"
        add.call('question', name, question_fields(name, 'confidence', "Question #{%w[zero One Two Three Four][number]} - Select from 1 to 5"))
      end
      add.call('page', 'feedback-intro', page_fields('feedback-intro', 'text_page', 'Additional feedback'))
      FEEDBACK.each { |name, _body, _options| ids << entry_id('question', "shared:#{name}") }
      thankyou = scope == 'bravo' ? '1-3-3-5-bravo' : '1-3-3-5'
      add.call('page', thankyou, page_fields(
                                   thankyou,
                                   'thankyou',
                                   'Thank you',
                                   body: 'You can also [give feedback about the training (opens in new window)](https://forms.office.com/Pages/ResponsePage.aspx?id=xxxxx)',
                                 ))
      add.call('page', '1-3-4', page_fields('1-3-4', 'certificate', 'Download your certificate'))
      ids
    end

    def build_course
      feedback_links = FEEDBACK.map { |name, _body, _options| entry_link(entry_id('question', "shared:#{name}")) }
      thank_you_id = add_entry('page', 'course:thank-you', page_fields('thank-you', 'text_page', 'Thank you'))
      feedback_links << entry_link(thank_you_id)
      add_entry('course', 'course', {
        'service_name' => 'Early years child development training',
        'internal_mailbox' => 'child-development.training@education.gov.uk',
        'privacy_policy_url' => 'https://www.gov.uk/government/publications/privacy-information-members-of-the-public/privacy-information-members-of-the-public',
        'feedback' => feedback_links,
      })
    end

    def build_static_pages
      STATIC_PAGES.each do |name, heading, footer|
        add_entry('static', name, {
          'name' => name,
          'heading' => heading,
          # pages/show already renders the entry heading as the sole page h1.
          'body' => 'Synthetic content for automated tests.',
          'footer' => footer,
        })
      end
    end

    def build_resource
      add_entry('resource', 'test.resource', {
        'name' => 'test.resource',
        'body' => 'Synthetic resource for automated tests.',
      })
    end

    def build_user_settings
      SETTINGS.each do |name, title, local_authority, role_type|
        add_entry('userSetting', name, {
          'name' => name,
          'title' => title,
          'local_authority' => local_authority,
          'role_type' => role_type,
          'active' => true,
        })
      end
    end

    def page_fields(name, page_type, heading, notes: false, body: heading)
      {
        'name' => name,
        'page_type' => page_type,
        'heading' => heading,
        'body' => body,
        'notes' => notes,
      }
    end

    def video_fields(name)
      {
        'name' => name,
        'heading' => name,
        'body' => 'In this video an early years expert explains',
        'title' => 'Vimeo Video Title',
        'transcript' => 'The children have gone outside and started a bug hunt.',
        'video_id' => '743243040',
        'video_provider' => 'vimeo',
      }
    end

    def question_fields(name, page_type, body, answers: nil, **options)
      fields = {
        'name' => name,
        'page_type' => page_type,
        'body' => body,
        'success_message' => 'You selected the correct answers',
        'failure_message' => 'You did not select the correct answers',
      }
      fields['answers'] = answers unless answers.nil?
      fields.merge(options.transform_keys(&:to_s))
    end

    def summative_fields(scope, name, number)
      correct = { 1 => [1, 3], 2 => [2, 3], 3 => [3, 4] }.fetch(number, [3])
      labels = if number <= 3
                 ['Wrong answer 1', 'Wrong answer 2', 'Correct answer 1', 'Correct answer 2']
               else
                 ['Wrong answer 1', 'Wrong answer 2', 'Correct answer 3', 'Wrong answer 3']
               end
      answers = labels.each_with_index.map do |label, index|
        correct.include?(index + 1) ? [label, true] : [label]
      end
      question_fields(
        name,
        'summative',
        "Question #{%w[zero One Two Three Four Five Six Seven Eight Nine Ten][number]} - Select from following",
        answers: answers,
      ).merge('failure_message' => "You did not select the correct answers. [revisit topic](/modules/#{scope}/content-pages/1-1-1)")
    end

    def add_entry(type, key, fields)
      id = entry_id(type, key)
      @entries << {
        'metadata' => { 'tags' => [] },
        'sys' => {
          'id' => id,
          'type' => 'Entry',
          'contentType' => { 'sys' => { 'type' => 'Link', 'linkType' => 'ContentType', 'id' => type } },
          'publishedVersion' => 1,
        },
        'fields' => fields.transform_values { |value| { LOCALE => value } },
      }
      id
    end

    def thumbnail_asset
      {
        'metadata' => { 'tags' => [] },
        'sys' => { 'id' => thumbnail_id, 'type' => 'Asset', 'publishedVersion' => 1 },
        'fields' => {
          'title' => { LOCALE => 'Synthetic Rails test thumbnail' },
          'description' => { LOCALE => 'Synthetic test asset; contains no production content.' },
          'file' => {
            LOCALE => {
              'url' => '//seed/test-thumbnail.svg',
              'details' => { 'size' => 634, 'image' => { 'width' => 780, 'height' => 519 } },
              'fileName' => 'test-thumbnail.svg',
              'contentType' => 'image/svg+xml',
            },
          },
        },
      }
    end

    def dependency_for(name)
      { 'bravo' => 'alpha', 'charlie' => 'bravo' }[name]
    end

    def entry_id(type, key)
      "eyfs#{Digest::SHA256.hexdigest("#{type}:#{key}")[0, 40]}"
    end

    def thumbnail_id
      'eyfsRailsTestThumbnail'
    end

    def entry_link(id)
      { 'sys' => { 'type' => 'Link', 'linkType' => 'Entry', 'id' => id } }
    end

    def asset_link(id)
      { 'sys' => { 'type' => 'Link', 'linkType' => 'Asset', 'id' => id } }
    end

    def deep_sort(value)
      case value
      # This standalone tool intentionally does not load ActiveSupport#index_with.
      when Hash then value.keys.sort.to_h { |key| [key, deep_sort(value[key])] } # rubocop:disable Rails/IndexWith
      when Array then value.map { |item| deep_sort(item) }
      else value
      end
    end
  end
end
