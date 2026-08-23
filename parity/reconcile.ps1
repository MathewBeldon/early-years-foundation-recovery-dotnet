$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $PSScriptRoot "docker-compose.yml"
$containerEngine = if ($env:CONTAINER_ENGINE) { $env:CONTAINER_ENGINE } else { "podman" }
$sql = @"
SELECT json_build_object(
  'users', (SELECT json_build_object('count', count(*), 'min_id', min(id), 'max_id', max(id)) FROM users),
  'progress', (SELECT json_build_object('count', count(*), 'completed', count(completed_at)) FROM user_module_progress),
  'assessments', (SELECT json_build_object('count', count(*), 'passed', count(*) FILTER (WHERE passed)) FROM assessments),
  'responses', (SELECT json_build_object('count', count(*), 'correct', count(*) FILTER (WHERE correct)) FROM responses),
  'questionnaire', (SELECT jsonb_build_object(
    'email', u.email,
    'assessments', COALESCE((SELECT jsonb_agg(
      jsonb_build_object(
        'training_module', a.training_module,
        'score', a.score,
        'passed', a.passed,
        'completed', a.completed_at IS NOT NULL)
      ORDER BY a.training_module, a.started_at)
      FROM assessments a WHERE a.user_id = u.id), '[]'::jsonb),
    'responses', COALESCE((SELECT jsonb_agg(
      jsonb_build_object(
        'training_module', r.training_module,
        'question_name', r.question_name,
        'question_type', r.question_type,
        'answers', r.answers,
        'correct', r.correct,
        'assessment', (SELECT jsonb_build_object(
          'training_module', a.training_module,
          'score', a.score,
          'passed', a.passed,
          'completed', a.completed_at IS NOT NULL)
          FROM assessments a WHERE a.id = r.assessment_id))
      ORDER BY r.training_module, r.question_name)
      FROM responses r WHERE r.user_id = u.id), '[]'::jsonb),
    'events', COALESCE((SELECT jsonb_agg(
      jsonb_build_object('name', e.name, 'properties', e.properties)
      ORDER BY e.name, e.properties::text)
      FROM events e WHERE e.user_id = u.id
        AND e.name IN ('summative_assessment_start', 'questionnaire_answer', 'summative_assessment_complete')), '[]'::jsonb)
  ) FROM users u WHERE u.email = 'questionnaire@example.test'),
  'questionnaireJourneys', COALESCE((SELECT jsonb_agg(
    jsonb_build_object(
      'email', u.email,
      'assessments', COALESCE((SELECT jsonb_agg(
        jsonb_build_object(
          'training_module', a.training_module,
          'score', a.score,
          'passed', a.passed,
          'completed', a.completed_at IS NOT NULL)
        ORDER BY a.training_module, a.started_at)
        FROM assessments a WHERE a.user_id = u.id), '[]'::jsonb),
      'responses', COALESCE((SELECT jsonb_agg(
        jsonb_build_object(
          'training_module', r.training_module,
          'question_name', r.question_name,
          'question_type', r.question_type,
          'answers', r.answers,
          'correct', r.correct,
          'assessment', (SELECT jsonb_build_object(
            'training_module', a.training_module,
            'score', a.score,
            'passed', a.passed,
            'completed', a.completed_at IS NOT NULL)
            FROM assessments a WHERE a.id = r.assessment_id))
        ORDER BY r.training_module, r.question_name)
        FROM responses r WHERE r.user_id = u.id), '[]'::jsonb),
      'events', COALESCE((SELECT jsonb_agg(
        jsonb_build_object('name', e.name, 'properties', CASE
          WHEN e.name = 'summative_assessment_complete' AND e.properties ? 'score'
            THEN jsonb_set(e.properties, '{score}', to_jsonb(((e.properties->>'score')::numeric)::int))
          ELSE e.properties END)
        ORDER BY e.name, e.properties::text)
        FROM events e WHERE e.user_id = u.id
          AND e.name IN ('summative_assessment_start', 'questionnaire_answer', 'summative_assessment_complete', 'feedback_start', 'feedback_complete', 'confidence_check_complete')), '[]'::jsonb))
    ORDER BY u.email)
    FROM users u WHERE u.email IN ('questionnaire-pass@example.test', 'questionnaire-fail@example.test')), '[]'::jsonb),
  'formativeQuestionnaire', (SELECT jsonb_build_object(
    'email', u.email,
    'assessments', COALESCE((SELECT jsonb_agg(a.id ORDER BY a.id)
      FROM assessments a WHERE a.user_id = u.id), '[]'::jsonb),
    'progress', COALESCE((SELECT jsonb_agg(p.id ORDER BY p.id)
      FROM user_module_progress p WHERE p.user_id = u.id), '[]'::jsonb),
    'responses', COALESCE((SELECT jsonb_agg(jsonb_build_object(
      'training_module', r.training_module,
      'question_name', r.question_name,
      'question_type', r.question_type,
      'answers', r.answers,
      'correct', r.correct,
      'assessment', r.assessment_id IS NOT NULL)
      ORDER BY r.training_module, r.question_name)
      FROM responses r WHERE r.user_id = u.id), '[]'::jsonb),
    'events', COALESCE((SELECT jsonb_agg(jsonb_build_object(
      'name', e.name,
      'properties', jsonb_build_object(
        'path', e.properties->>'path',
        'controller', e.properties->>'controller',
        'action', e.properties->>'action',
        'training_module_id', e.properties->>'training_module_id',
        'id', e.properties->>'id',
        'uid', e.properties->>'uid',
        'mod_uid', e.properties->>'mod_uid',
        'type', e.properties->>'type',
        'success', CASE WHEN e.properties ? 'success' THEN to_jsonb((e.properties->>'success')::boolean) ELSE 'null'::jsonb END,
        'answers', e.properties->'answers'))
      ORDER BY e.name, e.properties::text)
      FROM events e WHERE e.user_id = u.id
        AND e.name IN ('questionnaire_answer', 'page_view', 'module_content_page')), '[]'::jsonb)
  ) FROM users u WHERE u.email = 'formative-questionnaire@example.test'),
  'accountPreferences', (SELECT jsonb_build_object(
    'email', u.email,
    'trainingEmails', u.training_emails,
    'researchParticipant', u.research_participant,
    'events', COALESCE((SELECT jsonb_agg(
      jsonb_build_object('name', e.name, 'properties', e.properties)
      ORDER BY e.name, e.properties::text)
      FROM events e WHERE e.user_id = u.id
        AND e.name IN ('user_training_emails_change', 'user_research_participant_change')), '[]'::jsonb)
  ) FROM users u WHERE u.email = 'account-preferences@example.test'),
  'certificates', COALESCE((SELECT jsonb_agg(
    jsonb_build_object(
      'email', u.email,
      'progress', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'module_name', p.module_name,
        'last_page', p.last_page,
        'completed', p.completed_at IS NOT NULL,
        'visited_pages', COALESCE((SELECT jsonb_agg(page_name ORDER BY page_name)
          FROM jsonb_object_keys(p.visited_pages) AS page_name), '[]'::jsonb))
        ORDER BY p.module_name)
        FROM user_module_progress p WHERE p.user_id = u.id), '[]'::jsonb),
      'assessments', COALESCE((SELECT jsonb_agg(jsonb_build_object(
        'training_module', a.training_module,
        'score', a.score,
        'passed', a.passed,
        'completed', a.completed_at IS NOT NULL)
        ORDER BY a.training_module, a.started_at)
        FROM assessments a WHERE a.user_id = u.id), '[]'::jsonb),
      'events', COALESCE((SELECT jsonb_agg(e.name ORDER BY e.name, e.time)
        FROM events e WHERE e.user_id = u.id), '[]'::jsonb))
    ORDER BY u.email)
    FROM users u WHERE u.email IN ('certificate-complete@example.test', 'certificate-incomplete@example.test')), '[]'::jsonb),
  'learningLog', (SELECT jsonb_build_object(
    'email', u.email,
    'notes', COALESCE((SELECT jsonb_agg(jsonb_build_object(
      'title', n.title,
      'training_module', n.training_module,
      'name', n.name)
      ORDER BY n.training_module, n.name, n.title)
      FROM notes n
      WHERE n.user_id = u.id
        AND n.training_module = 'module-1'
        AND n.name = 'key-concepts'), '[]'::jsonb),
    'events', COALESCE((SELECT jsonb_agg(jsonb_build_object(
      'name', e.name,
      'properties', jsonb_build_object(
        'path', e.properties->>'path',
        'controller', e.properties->>'controller',
        'action', e.properties->>'action',
        'length', CASE WHEN e.properties ? 'length'
          THEN to_jsonb((e.properties->>'length')::int) ELSE 'null'::jsonb END,
        'title', e.properties->>'title',
        'training_module', e.properties->>'training_module',
        'name', e.properties->>'name'))
      ORDER BY e.name, e.properties->>'action')
      FROM events e
      WHERE e.user_id = u.id
        AND e.name IN ('user_note_created', 'user_note_updated')
        AND e.properties->>'training_module' = 'module-1'
        AND e.properties->>'name' = 'key-concepts'), '[]'::jsonb)
  ) FROM users u WHERE u.email = 'assessment@example.test'),
  'moduleContent', (SELECT jsonb_build_object(
    'email', u.email,
    'progress', COALESCE((SELECT jsonb_agg(jsonb_build_object(
      'module_name', p.module_name,
      'last_page', p.last_page,
      'completed', p.completed_at IS NOT NULL,
      'started', p.started_at IS NOT NULL,
      'visited_pages', COALESCE((SELECT jsonb_agg(page_name ORDER BY page_name)
        FROM jsonb_object_keys(p.visited_pages) AS page_name), '[]'::jsonb))
      ORDER BY p.module_name)
      FROM user_module_progress p WHERE p.user_id = u.id), '[]'::jsonb),
    'events', COALESCE((SELECT jsonb_agg(jsonb_build_object('name', e.name, 'properties', e.properties)
      ORDER BY e.name, e.properties::text)
      FROM events e WHERE e.user_id = u.id
        AND e.name IN ('module_overview_page', 'module_start', 'page_view', 'module_content_page')), '[]'::jsonb)
  ) FROM users u WHERE u.email = 'module-content@example.test'),
  'videoContent', (SELECT jsonb_build_object(
    'email', u.email,
    'progress', COALESCE((SELECT jsonb_agg(jsonb_build_object(
      'module_name', p.module_name,
      'last_page', p.last_page,
      'completed', p.completed_at IS NOT NULL,
      'started', p.started_at IS NOT NULL,
      'visited_pages', COALESCE((SELECT jsonb_agg(page_name ORDER BY page_name)
        FROM jsonb_object_keys(p.visited_pages) AS page_name), '[]'::jsonb))
      ORDER BY p.module_name)
      FROM user_module_progress p WHERE p.user_id = u.id), '[]'::jsonb),
    'assessments', COALESCE((SELECT jsonb_agg(a.id ORDER BY a.id)
      FROM assessments a WHERE a.user_id = u.id), '[]'::jsonb),
    'responses', COALESCE((SELECT jsonb_agg(r.id ORDER BY r.id)
      FROM responses r WHERE r.user_id = u.id), '[]'::jsonb),
    'events', COALESCE((SELECT jsonb_agg(e.name ORDER BY e.name, e.time)
      FROM events e WHERE e.user_id = u.id
        AND e.name IN ('page_view', 'module_content_page')), '[]'::jsonb)
  ) FROM users u WHERE u.email = 'video-content@example.test'),
  'fullRegistration', (SELECT jsonb_build_object(
    'email', u.email, 'registrationComplete', u.registration_complete,
    'firstName', u.first_name, 'lastName', u.last_name, 'country', u.country,
    'settingTypeId', u.setting_type_id, 'settingTypeOther', u.setting_type_other,
    'localAuthority', u.local_authority, 'roleType', u.role_type,
    'roleTypeOther', u.role_type_other, 'earlyYearsExperience', u.early_years_experience,
    'trainingEmails', u.training_emails, 'researchParticipant', u.research_participant,
    'termsAccepted', u.terms_and_conditions_agreed_at IS NOT NULL,
    'registrationEvents', COALESCE((SELECT jsonb_agg(jsonb_build_object(
      'name', e.name, 'success', CASE WHEN e.properties ? 'success'
        THEN to_jsonb((e.properties->>'success')::boolean) ELSE 'null'::jsonb END)
      ORDER BY e.name, e.time)
      FROM events e WHERE e.user_id = u.id AND e.name = 'user_registration'), '[]'::jsonb),
    'progressCount', (SELECT count(*) FROM user_module_progress p WHERE p.user_id = u.id),
    'assessmentCount', (SELECT count(*) FROM assessments a WHERE a.user_id = u.id),
    'responseCount', (SELECT count(*) FROM responses r WHERE r.user_id = u.id)
  ) FROM users u WHERE u.email = 'full-registration@example.test'),
  'notes', (SELECT json_build_object('count', count(*), 'min_id', min(id), 'max_id', max(id)) FROM notes),
  'visits', (SELECT count(*) FROM visits),
  'events', (SELECT count(*) FROM events),
  'mail_events', (SELECT count(*) FROM mail_events)
)::text;
"@

function Snapshot([string]$service, [string]$database) {
    if ($containerEngine -eq "podman") {
        $value = python -m podman_compose -f $compose exec -T $service psql -U postgres -d $database -Atc $sql
    } else {
        $value = docker compose -f $compose exec -T $service psql -U postgres -d $database -Atc $sql
    }
    if ($LASTEXITCODE -ne 0) { throw "Could not snapshot $database" }
    return ($value | ConvertFrom-Json)
}

$rails = Snapshot "rails-db" "rails_parity"
$dotnet = Snapshot "dotnet-db" "dotnet_parity"

foreach ($snapshot in @(@{ Name = "Rails"; Value = $rails }, @{ Name = ".NET"; Value = $dotnet })) {
    $registration = $snapshot.Value.fullRegistration
    if ($registration.email -ne 'full-registration@example.test' -or -not $registration.registrationComplete) {
        throw "$($snapshot.Name) full-registration identity is missing or incomplete."
    }
    if ($registration.firstName -ne 'Parity' -or $registration.lastName -ne 'Registrant' -or
        $registration.country -ne 'England' -or $registration.settingTypeId -ne 'setting_la_general_role' -or
        $null -ne $registration.settingTypeOther -or $registration.localAuthority -ne 'local_authority_a' -or
        $registration.roleType -ne 'other' -or $registration.roleTypeOther -ne 'Early years parity specialist' -or
        $registration.earlyYearsExperience -ne '2-5' -or $registration.trainingEmails -ne $false -or
        $registration.researchParticipant -ne $true -or -not $registration.termsAccepted) {
        throw "$($snapshot.Name) full-registration persisted profile does not match the England/custom-role contract."
    }
    if (@($registration.registrationEvents).Count -ne 1 -or
        @($registration.registrationEvents)[0].name -ne 'user_registration' -or
        @($registration.registrationEvents)[0].success -ne $true) {
        throw "$($snapshot.Name) full-registration must emit exactly one successful user_registration event."
    }
    if ($registration.progressCount -ne 0 -or $registration.assessmentCount -ne 0 -or $registration.responseCount -ne 0) {
        throw "$($snapshot.Name) registration journey mutated learning state."
    }

    $video = $snapshot.Value.videoContent
    if ($video.email -ne 'video-content@example.test') { throw "$($snapshot.Name) video reconciliation identity is missing." }
    if (@($video.progress).Count -ne 1) { throw "$($snapshot.Name) video journey must have exactly one progress row." }
    $progress = @($video.progress)[0]
    if ($progress.module_name -ne 'module-1' -or $progress.last_page -ne 'expert-video' -or
        -not $progress.started -or $progress.completed -or
        @($progress.visited_pages).Count -ne 1 -or @($progress.visited_pages)[0] -ne 'expert-video') {
        throw "$($snapshot.Name) video journey progress is not the exact expected started/incomplete expert-video state."
    }
    if (@($video.assessments).Count -ne 0 -or @($video.responses).Count -ne 0 -or @($video.events).Count -ne 0) {
        throw "$($snapshot.Name) video journey wrote an assessment, response, page_view, or module_content_page event."
    }
}

$railsJson = $rails | ConvertTo-Json -Depth 8 -Compress
$dotnetJson = $dotnet | ConvertTo-Json -Depth 8 -Compress
$result = [ordered]@{ rails = $rails; dotnet = $dotnet; matches = ($railsJson -eq $dotnetJson) }
$reportDirectory = Join-Path $root "TestResults"
New-Item -ItemType Directory -Force $reportDirectory | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $reportDirectory "database-reconciliation.json")
if (-not $result.matches) { throw "Rails/.NET database reconciliation differs. See TestResults/database-reconciliation.json" }
Write-Host "Database reconciliation passed."
