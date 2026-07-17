# frozen_string_literal: true

require 'minitest/autorun'
require 'yaml'
require_relative '../lib/rails_test_seed'

class RailsTestSeedTest < Minitest::Test
  def setup
    @document = ContentfulRailsTestSeed::Builder.new.build
    @entries = @document.fetch('entries')
  end

  def test_uses_authoritative_export_without_production_content
    assert_equal 10, @document.fetch('contentTypes').count
    assert_equal 10, @document.fetch('editorInterfaces').count
    assert_equal 1, @document.fetch('locales').count
    assert_equal 1, @document.fetch('assets').count
    assert_equal 144, @entries.count
    %w[contentTypes editorInterfaces locales tags].each do |key|
      assert_equal ContentfulRailsTestSeed::Builder.new.source_export.fetch(key), @document.fetch(key)
    end
  end

  def test_expected_module_inventory
    modules = entries_of_type('trainingModule').sort_by { |entry| field(entry, 'position') }
    assert_equal(%w[alpha bravo charlie delta], modules.map { |entry| field(entry, 'name') })
    assert_equal(['First Training Module', 'Second Training Module', 'Third Training Module', 'Fourth Training Module'], modules.map { |entry| field(entry, 'title') })
    assert_equal([43, 40, 44, 0], modules.map { |entry| Array(field(entry, 'pages')).count })
  end

  def test_alpha_structure_matches_rails_section_assertions
    pages = linked_pages('alpha').reject { |entry| page_type(entry) == 'interruption_page' }
    sections = pages.slice_before { |entry| section_start?(entry, pages) }.to_a
    assert_equal [7, 5, 20, 9, 1], sections.map(&:count)

    subsection_counts = sections.flat_map do |section|
      section.slice_before { |entry| subsection_start?(entry) }.map(&:count)
    end
    assert_equal [1, 1, 1, 2, 2, 1, 4, 1, 1, 12, 6, 9, 1], subsection_counts
  end

  def test_alpha_questions_match_model_and_journey_assertions
    pages = linked_pages('alpha')
    first_formative = pages.find { |entry| field(entry, 'name') == '1-1-4-1' }
    assert_equal [['Correct answer 1', true], ['Wrong answer 1']], field(first_formative, 'answers')

    matching = pages.filter_map do |entry|
      answers = Array(field(entry, 'answers')).flatten.compact.map(&:to_s)
      field(entry, 'name') if answers.any? { |answer| answer.match?(/Wrong\s.+ 3/i) }
    end
    assert_equal %w[1-3-2-4 1-3-2-5 1-3-2-6 1-3-2-7 1-3-2-8 1-3-2-9 1-3-2-10], matching

    confidence = pages.select { |entry| page_type(entry) == 'confidence' }
    assert_equal(%w[1-3-3-1 1-3-3-2 1-3-3-3 1-3-3-4], confidence.map { |entry| field(entry, 'name') })
  end

  def test_alpha_order_matches_the_committed_browser_journey
    fixture_path = File.expand_path('../../spec/support/ast/alpha-pass-response-with-feedback.yml', __dir__)
    expected_names = YAML.unsafe_load_file(fixture_path).map { |step| step.fetch(:path).split('/').last }
    assert_equal(expected_names, linked_pages('alpha').map { |entry| field(entry, 'name') })
  end

  def test_released_modules_meet_content_integrity_shape
    %w[alpha bravo charlie].each do |module_name|
      pages = linked_pages(module_name)
      types = pages.map { |entry| page_type(entry) }
      assert_equal 'interruption_page', types.first
      assert_equal 'sub_module_intro', types[1]
      assert_equal 'topic_intro', types[2]
      assert_equal 'thankyou', types[-2]
      assert_equal 'certificate', types[-1]
      assert_equal 10, types.count('summative')
      assert_operator types.count('confidence'), :>=, 4
      assert_includes types, 'formative'
      assert_includes types, 'feedback'
      assert_includes types, 'video_page'
      assert_includes types, 'assessment_results'
    end
  end

  def test_course_feedback_and_static_content_match_specs
    courses = entries_of_type('course')
    assert_equal 1, courses.count
    course = courses.first
    assert_equal 'Early years child development training', field(course, 'service_name')
    assert_equal 9, field(course, 'feedback').count
    assert_equal(8, field(course, 'feedback').filter_map { |link| entry_by_id(link.dig('sys', 'id')) }.count { |entry| page_type(entry) == 'feedback' })

    static = entries_of_type('static')
    assert_equal(4, static.count { |entry| field(entry, 'footer') })
    sitemap = static.find { |entry| field(entry, 'name') == 'sitemap' }
    assert_equal 'Sitemap', field(sitemap, 'heading')
    assert_equal 'Synthetic content for automated tests.', field(sitemap, 'body')
    refute_match(/^#\s/, field(sitemap, 'body'))
  end

  def test_registration_settings_and_resource_are_present
    assert_equal 17, entries_of_type('userSetting').count
    resources = entries_of_type('resource')
    assert_equal 1, resources.count
    assert_equal 'test.resource', field(resources.first, 'name')
  end

private

  def entries_of_type(type)
    @entries.select { |entry| entry.dig('sys', 'contentType', 'sys', 'id') == type }
  end

  def field(entry, name)
    entry.dig('fields', name, ContentfulRailsTestSeed::LOCALE)
  end

  def page_type(entry)
    return 'video_page' if entry.dig('sys', 'contentType', 'sys', 'id') == 'video'

    field(entry, 'page_type')
  end

  def entry_by_id(id)
    @entries.find { |entry| entry.dig('sys', 'id') == id }
  end

  def linked_pages(module_name)
    mod = entries_of_type('trainingModule').find { |entry| field(entry, 'name') == module_name }
    field(mod, 'pages').map { |link| entry_by_id(link.dig('sys', 'id')) }
  end

  def section_start?(entry, pages)
    type = page_type(entry)
    type == 'sub_module_intro' || type == 'summary_intro' || type == 'certificate' ||
      (type == 'feedback' && pages.find { |page| page_type(page) == 'feedback' } == entry)
  end

  def subsection_start?(entry)
    %w[topic_intro recap_page assessment_intro confidence_intro certificate].include?(page_type(entry))
  end
end
