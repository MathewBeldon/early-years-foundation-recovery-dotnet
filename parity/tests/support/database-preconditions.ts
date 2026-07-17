import { Client } from 'pg';
import type { SimulatorUser } from './auth.js';

function journeyDatabaseUrl(): string {
  const value = process.env.PARITY_JOURNEY_DATABASE_URL;
  if (!value) {
    throw new Error(
      'PARITY_JOURNEY_DATABASE_URL is required for a journey with a database precondition.',
    );
  }
  return value;
}

/**
 * Create the synthetic legacy-account precondition that cannot be reached via
 * the current One Login-only UI. This is setup only; authentication still runs
 * through the application and the interactive simulator.
 */
export async function seedRegisteredAccountWithoutOneLogin(
  identity: SimulatorUser,
  options: { firstName: string; surname: string },
): Promise<void> {
  const client = new Client({ connectionString: journeyDatabaseUrl() });
  await client.connect();
  try {
    await client.query(
      `INSERT INTO users (
         email, encrypted_password, created_at, updated_at, confirmed_at,
         first_name, last_name, registration_complete,
         terms_and_conditions_agreed_at, setting_type_id, setting_type,
         country, local_authority, role_type, training_emails,
         research_participant, gov_one_id
       ) VALUES (
         $1, '', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP,
         $2, $3, TRUE,
         CURRENT_TIMESTAMP, 'department_for_education',
         'Department for Education', 'England', 'Not applicable',
         'Not applicable', FALSE, FALSE, NULL
       )`,
      [identity.email, options.firstName, options.surname],
    );
  } finally {
    await client.end();
  }
}

/**
 * Match the Rails module-feedback source spec's factory precondition: the
 * learner has already answered the shared one-off question on another module.
 */
export async function seedAnsweredSkippableFeedback(
  identity: SimulatorUser,
): Promise<void> {
  const client = new Client({ connectionString: journeyDatabaseUrl() });
  await client.connect();
  try {
    const result = await client.query(
      `INSERT INTO responses (
         user_id, training_module, question_name, answers, correct,
         question_type, created_at, updated_at
       )
       SELECT id, 'bravo', 'feedback-skippable', '[1]'::jsonb, TRUE,
              'feedback', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
       FROM users
       WHERE gov_one_id = $1`,
      [identity.sub],
    );
    if (result.rowCount !== 1) {
      throw new Error(
        `Expected one registered learner for the skippable-feedback precondition; inserted ${result.rowCount ?? 0} responses.`,
      );
    }
  } finally {
    await client.end();
  }
}
