INSERT INTO users (
  email, encrypted_password, created_at, updated_at, registration_complete,
  gov_one_id, first_name, last_name, training_emails, research_participant
) VALUES
  ('existing@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', true,
   'synthetic-existing', 'Synthetic', 'Learner', false, false),
  ('new@example.test', '', '2026-01-01 00:00:00+00', '2026-01-01 00:00:00+00', false,
   'synthetic-new', null, null, null, null)
ON CONFLICT (email) DO UPDATE SET
  registration_complete = EXCLUDED.registration_complete,
  gov_one_id = EXCLUDED.gov_one_id,
  first_name = EXCLUDED.first_name,
  last_name = EXCLUDED.last_name,
  updated_at = EXCLUDED.updated_at;
