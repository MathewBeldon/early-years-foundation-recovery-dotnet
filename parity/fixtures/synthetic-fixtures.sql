INSERT INTO users (
  email, encrypted_password, created_at, updated_at, registration_complete,
  gov_one_id, first_name, last_name, training_emails, research_participant,
  setting_type_id, setting_type_other, country, terms_and_conditions_agreed_at
) VALUES
  ('existing@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-existing', 'Synthetic', 'Learner', false, false,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('new@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', false,
   'synthetic-new', null, null, null, null,
   null, null, null, null),
  ('resuming@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', false,
   'synthetic-resuming', 'Resuming', 'Learner', false, null,
   'other', 'Childminder', 'England', '2026-01-01 00:00:00+00'),
  ('assessment@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-assessment', 'Assessment', 'Learner', false, false,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('questionnaire@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-questionnaire', 'Questionnaire', 'Learner', false, false,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('questionnaire-pass@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-questionnaire-pass', 'Questionnaire', 'Pass', false, false,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('questionnaire-fail@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-questionnaire-fail', 'Questionnaire', 'Fail', false, false,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('formative-questionnaire@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-formative-questionnaire', 'Formative', 'Questionnaire', false, false,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('certificate-complete@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-certificate-complete', 'Certificate', 'Complete', false, false,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('certificate-incomplete@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-certificate-incomplete', 'Certificate', 'Incomplete', false, false,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('account-preferences@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-account-preferences', 'Account', 'Preferences', true, true,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('module-content@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-module-content', 'Module', 'Content', false, false,
   'other', null, null, '2026-01-01 00:00:00+00'),
  ('video-content@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-video-content', 'Video', 'Content', false, false,
   'other', null, null, '2026-01-01 00:00:00+00')
ON CONFLICT (email) DO UPDATE SET
  registration_complete = EXCLUDED.registration_complete,
  gov_one_id = EXCLUDED.gov_one_id,
  first_name = EXCLUDED.first_name,
  last_name = EXCLUDED.last_name,
  setting_type_id = EXCLUDED.setting_type_id,
  setting_type_other = EXCLUDED.setting_type_other,
  country = EXCLUDED.country,
  training_emails = EXCLUDED.training_emails,
  research_participant = EXCLUDED.research_participant,
  terms_and_conditions_agreed_at = EXCLUDED.terms_and_conditions_agreed_at,
  updated_at = EXCLUDED.updated_at;

DELETE FROM events
WHERE user_id IN (SELECT id FROM users WHERE email IN (
  'questionnaire@example.test', 'questionnaire-pass@example.test', 'questionnaire-fail@example.test', 'formative-questionnaire@example.test', 'certificate-complete@example.test',
  'certificate-incomplete@example.test', 'account-preferences@example.test', 'module-content@example.test', 'video-content@example.test'));

DELETE FROM responses
WHERE user_id IN (SELECT id FROM users WHERE email IN (
  'questionnaire@example.test', 'questionnaire-pass@example.test', 'questionnaire-fail@example.test', 'formative-questionnaire@example.test',
  'certificate-complete@example.test', 'certificate-incomplete@example.test', 'video-content@example.test'));

DELETE FROM assessments
WHERE user_id IN (SELECT id FROM users WHERE email IN (
  'assessment@example.test', 'questionnaire@example.test', 'questionnaire-pass@example.test', 'questionnaire-fail@example.test', 'formative-questionnaire@example.test',
  'certificate-complete@example.test', 'certificate-incomplete@example.test', 'module-content@example.test', 'video-content@example.test'));

INSERT INTO assessments (user_id, training_module, score, passed, started_at, completed_at)
VALUES
  ((SELECT id FROM users WHERE email = 'assessment@example.test'), 'module-1', 50, false,
   '2026-01-02 00:00:00+00', '2026-01-02 00:30:00+00'),
  ((SELECT id FROM users WHERE email = 'assessment@example.test'), 'module-2', 75, true,
   '2026-01-03 00:00:00+00', '2026-01-03 00:30:00+00'),
  ((SELECT id FROM users WHERE email = 'questionnaire@example.test'), 'module-2', 75, true,
   '2026-01-04 00:00:00+00', '2026-01-04 00:30:00+00'),
  ((SELECT id FROM users WHERE email = 'certificate-complete@example.test'), 'module-2', 75, true,
   '2026-01-05 00:00:00+00', '2026-01-05 00:30:00+00'),
  ((SELECT id FROM users WHERE email = 'certificate-incomplete@example.test'), 'module-2', 50, false,
   '2026-01-06 00:00:00+00', '2026-01-06 00:30:00+00');

DELETE FROM user_module_progress
WHERE user_id IN (SELECT id FROM users WHERE email IN (
  'assessment@example.test', 'questionnaire@example.test', 'questionnaire-pass@example.test', 'questionnaire-fail@example.test', 'formative-questionnaire@example.test',
  'certificate-complete@example.test', 'certificate-incomplete@example.test', 'module-content@example.test', 'video-content@example.test'));

INSERT INTO user_module_progress (
  user_id, module_name, started_at, completed_at, visited_pages, last_page, created_at, updated_at
) VALUES
  ((SELECT id FROM users WHERE email = 'assessment@example.test'), 'module-1',
   '2026-01-02 00:00:00+00', null,
   '{"what-to-expect":"2026-01-02T00:00:00Z","key-concepts":"2026-01-02T00:01:00Z","applying-learning":"2026-01-02T00:02:00Z","check-understanding":"2026-01-02T00:03:00Z","assessment-intro":"2026-01-02T00:04:00Z","summative-q1":"2026-01-02T00:05:00Z","summative-q2":"2026-01-02T00:06:00Z","assessment-results":"2026-01-02T00:07:00Z"}'::jsonb,
   'assessment-results', '2026-01-02 00:00:00+00', '2026-01-02 00:30:00+00'),
  ((SELECT id FROM users WHERE email = 'assessment@example.test'), 'module-2',
   '2026-01-03 00:00:00+00', null,
   '{"what-to-expect":"2026-01-03T00:00:00Z","why-communication-matters":"2026-01-03T00:01:00Z","everyday-strategies":"2026-01-03T00:02:00Z","quick-check":"2026-01-03T00:03:00Z","assessment-intro":"2026-01-03T00:04:00Z","summative-q1":"2026-01-03T00:05:00Z","summative-q2":"2026-01-03T00:06:00Z","summative-q3":"2026-01-03T00:07:00Z","summative-q4":"2026-01-03T00:08:00Z","assessment-results":"2026-01-03T00:09:00Z"}'::jsonb,
   'assessment-results', '2026-01-03 00:00:00+00', '2026-01-03 00:30:00+00'),
  ((SELECT id FROM users WHERE email = 'questionnaire@example.test'), 'module-2',
   '2026-01-04 00:00:00+00', null,
   '{"what-to-expect":"2026-01-04T00:00:00Z","why-communication-matters":"2026-01-04T00:01:00Z","everyday-strategies":"2026-01-04T00:02:00Z","quick-check":"2026-01-04T00:03:00Z","assessment-intro":"2026-01-04T00:04:00Z","summative-q1":"2026-01-04T00:05:00Z","summative-q2":"2026-01-04T00:06:00Z","summative-q3":"2026-01-04T00:07:00Z","summative-q4":"2026-01-04T00:08:00Z","assessment-results":"2026-01-04T00:09:00Z"}'::jsonb,
   'assessment-results', '2026-01-04 00:00:00+00', '2026-01-04 00:30:00+00'),
  ((SELECT id FROM users WHERE email = 'certificate-complete@example.test'), 'module-2',
   '2026-01-05 00:00:00+00', '2026-01-05 00:30:00+00',
   '{"what-to-expect":"2026-01-05T00:00:00Z","why-communication-matters":"2026-01-05T00:01:00Z","everyday-strategies":"2026-01-05T00:02:00Z","quick-check":"2026-01-05T00:03:00Z","assessment-intro":"2026-01-05T00:04:00Z","summative-q1":"2026-01-05T00:05:00Z","summative-q2":"2026-01-05T00:06:00Z","summative-q3":"2026-01-05T00:07:00Z","summative-q4":"2026-01-05T00:08:00Z","assessment-results":"2026-01-05T00:09:00Z","certificate":"2026-01-05T00:10:00Z"}'::jsonb,
   'certificate', '2026-01-05 00:00:00+00', '2026-01-05 00:30:00+00'),
  ((SELECT id FROM users WHERE email = 'certificate-incomplete@example.test'), 'module-2',
   '2026-01-06 00:00:00+00', null,
   '{"what-to-expect":"2026-01-06T00:00:00Z","why-communication-matters":"2026-01-06T00:01:00Z","everyday-strategies":"2026-01-06T00:02:00Z","quick-check":"2026-01-06T00:03:00Z","assessment-intro":"2026-01-06T00:04:00Z","summative-q1":"2026-01-06T00:05:00Z","summative-q2":"2026-01-06T00:06:00Z","summative-q3":"2026-01-06T00:07:00Z","summative-q4":"2026-01-06T00:08:00Z","assessment-results":"2026-01-06T00:09:00Z"}'::jsonb,
   'assessment-results', '2026-01-06 00:00:00+00', '2026-01-06 00:30:00+00');
