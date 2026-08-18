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
          AND e.name IN ('summative_assessment_start', 'questionnaire_answer', 'summative_assessment_complete')), '[]'::jsonb))
    ORDER BY u.email)
    FROM users u WHERE u.email IN ('questionnaire-pass@example.test', 'questionnaire-fail@example.test')), '[]'::jsonb),
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
$railsJson = $rails | ConvertTo-Json -Depth 8 -Compress
$dotnetJson = $dotnet | ConvertTo-Json -Depth 8 -Compress
$result = [ordered]@{ rails = $rails; dotnet = $dotnet; matches = ($railsJson -eq $dotnetJson) }
$reportDirectory = Join-Path $root "TestResults"
New-Item -ItemType Directory -Force $reportDirectory | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $reportDirectory "database-reconciliation.json")
if (-not $result.matches) { throw "Rails/.NET database reconciliation differs. See TestResults/database-reconciliation.json" }
Write-Host "Database reconciliation passed."
