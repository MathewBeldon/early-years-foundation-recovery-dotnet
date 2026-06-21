const fs = require("fs");
const path = require("path");
const { createClient } = require("contentful-management");
const { loadEnv } = require("./load-env");

loadEnv();

const locale = "en-US";
const dataDir = path.join(__dirname, "..", "..", "..", "data");

function readJson(filename) {
  return JSON.parse(fs.readFileSync(path.join(dataDir, filename), "utf8"));
}

function field(value) {
  return { [locale]: value };
}

function entryId(prefix, value) {
  return `${prefix}-${value}`
    .toLowerCase()
    .replace(/[^a-z0-9-]/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "");
}

async function findByName(environment, contentType, name) {
  const result = await environment.getEntries({
    content_type: contentType,
    "fields.name": name,
    limit: 1,
  });
  return result.items[0] ?? null;
}

async function findEntryById(environment, entryId) {
  try {
    return await environment.getEntry(entryId);
  } catch {
    return null;
  }
}

async function upsertEntryById(environment, entryId, contentType, fields) {
  let entry = await findEntryById(environment, entryId);
  if (entry) {
    entry.fields = { ...entry.fields, ...fields };
    entry = await entry.update();
  } else {
    entry = await environment.createEntryWithId(contentType, entryId, { fields });
  }

  if (!entry.isPublished() || entry.isUpdated()) {
    entry = await entry.publish();
  }

  return entry;
}

async function upsertEntry(environment, contentType, name, fields) {
  let entry = await findByName(environment, contentType, name);
  if (entry) {
    entry.fields = { ...entry.fields, ...fields };
    entry = await entry.update();
  } else {
    entry = await environment.createEntry(contentType, { fields });
  }

  if (!entry.isPublished() || entry.isUpdated()) {
    entry = await entry.publish();
  }

  return entry;
}

function mapPageFields(page) {
  const fields = {
    name: field(page.name),
    page_type: field(page.pageType),
    heading: field(page.heading),
    body: field(page.body),
    notes: field(page.notes ?? false),
  };

  if (page.answers?.length) {
    fields.answers = field(page.answers);
  }

  if (page.successMessage) {
    fields.success_message = field(page.successMessage);
  }

  if (page.failureMessage) {
    fields.failure_message = field(page.failureMessage);
  }

  return fields;
}

function mapQuestionFields(question) {
  const fields = {
    name: field(question.name),
    page_type: field(question.pageType),
    input_type: field(question.inputType),
    heading: field(question.heading),
    legend: field(question.legend ?? ""),
    body: field(question.body),
    skippable: field(question.skippable ?? false),
    more: field(question.hasMore ?? false),
    multi_select: field(question.inputType === "checkbox"),
  };

  if (question.options?.length) {
    fields.options = field(question.options);
  }

  if (question.hasOther && question.otherLabel) {
    fields.other = field(question.otherLabel);
  }

  if (question.hasOr && question.orLabel) {
    fields.or = field(question.orLabel);
  }

  return fields;
}

async function seedStaticPages(environment, pages) {
  console.log(`Seeding ${pages.length} static pages...`);
  for (const page of pages) {
    await upsertEntry(environment, "static", page.name, {
      name: field(page.name),
      title: field(page.title ?? page.heading),
      heading: field(page.heading),
      body: field(page.body),
      footer: field(page.footer ?? false),
      requires_auth: field(page.requiresAuth ?? false),
    });
  }
}

async function seedFeedbackQuestions(environment, questions) {
  console.log(`Seeding ${questions.length} feedback questions...`);
  const links = [];

  for (const question of questions) {
    const entry = await upsertEntry(
      environment,
      "question",
      question.name,
      mapQuestionFields(question),
    );
    links.push({
      sys: { type: "Link", linkType: "Entry", id: entry.sys.id },
    });
  }

  return links;
}

async function seedCourse(environment, feedbackLinks) {
  console.log("Seeding course configuration...");
  const existing = await environment.getEntries({ content_type: "course", limit: 1 });
  const fields = {
    service_name: field("Early years child development training (.NET demo)"),
    internal_mailbox: field("child-development.training@education.gov.uk"),
    privacy_policy_url: field("https://www.gov.uk/help/privacy-notice"),
    feedback: field(feedbackLinks),
  };

  let entry = existing.items[0];
  if (entry) {
    entry.fields = { ...entry.fields, ...fields };
    entry = await entry.update();
  } else {
    entry = await environment.createEntry("course", { fields });
  }

  if (!entry.isPublished() || entry.isUpdated()) {
    entry = await entry.publish();
  }

  return entry;
}

async function seedTrainingModules(environment, modules) {
  console.log(`Seeding ${modules.length} training modules...`);

  for (const module of modules) {
    const pageLinks = [];

    for (const page of module.pages) {
      const entryId = `${module.name}-${page.name}`;
      const entry = await upsertEntryById(
        environment,
        entryId,
        "page",
        mapPageFields(page),
      );
      pageLinks.push({
        sys: { type: "Link", linkType: "Entry", id: entry.sys.id },
      });
    }

    const fields = {
      title: field(module.title),
      name: field(module.name),
      description: field(module.description),
      outcomes: field(module.outcomes),
      criteria: field(module.criteria),
      duration: field(module.duration),
      position: field(module.position),
      live: field(module.live ?? true),
      pages: field(pageLinks),
    };

    if (module.upcoming) {
      fields.upcoming = field(module.upcoming);
    }

    await upsertEntry(environment, "trainingModule", module.name, fields);
  }
}

async function seedRegistrationReferenceData(environment, referenceData) {
  console.log(`Seeding ${referenceData.settingTypes.length - 1} registration setting types...`);
  for (const setting of referenceData.settingTypes.filter((item) => item.id !== "other")) {
    await upsertEntryById(
      environment,
      entryId("registration-setting", setting.id),
      "userSetting",
      {
        name: field(setting.id),
        title: field(setting.label),
        local_authority: field(setting.requiresLocalAuthority),
        role_type: field(setting.roleGroup),
        active: field(true),
      },
    );
  }

  console.log(`Seeding ${referenceData.roles.length} registration roles...`);
  for (const role of referenceData.roles) {
    const fields = {
      name: field(role.label),
      group: field(role.group),
    };

    if (role.hint) {
      fields.hint_text = field(role.hint);
    }

    await upsertEntryById(
      environment,
      entryId("registration-role", role.label),
      "registrationRole",
      fields,
    );
  }

  console.log(`Seeding ${referenceData.countries.length} registration countries...`);
  for (const country of referenceData.countries) {
    await upsertEntryById(
      environment,
      entryId("registration-country", country.id),
      "registrationCountry",
      {
        id: field(country.id),
        name: field(country.label),
      },
    );
  }

  console.log(`Seeding ${referenceData.localAuthorities.length} registration local authorities...`);
  for (const authority of referenceData.localAuthorities) {
    await upsertEntryById(
      environment,
      entryId("registration-local-authority", authority.label),
      "registrationLocalAuthority",
      {
        name: field(authority.label),
      },
    );
  }

  console.log(`Seeding ${referenceData.experienceLevels.length} registration experience options...`);
  for (const experience of referenceData.experienceLevels) {
    await upsertEntryById(
      environment,
      entryId("registration-experience", experience.id),
      "registrationExperience",
      {
        id: field(experience.id),
        name: field(experience.label),
      },
    );
  }
}

async function main() {
  const spaceId = process.env.CONTENTFUL_SPACE_ID;
  const environmentId = process.env.CONTENTFUL_ENVIRONMENT || "master";
  const token = process.env.CONTENTFUL_MANAGEMENT_TOKEN;

  if (!spaceId || !token) {
    console.error("Set CONTENTFUL_SPACE_ID and CONTENTFUL_MANAGEMENT_TOKEN in .env before seeding.");
    process.exit(1);
  }

  const client = createClient({ accessToken: token });
  const space = await client.getSpace(spaceId);
  const environment = await space.getEnvironment(environmentId);

  const staticDocument = readJson("demo-static-pages.json");
  const feedbackDocument = readJson("demo-feedback-content.json");
  const trainingDocument = readJson("demo-training-content.json");
  const referenceDocument = readJson("reference-data.json");

  await seedStaticPages(environment, staticDocument.pages);
  const feedbackLinks = await seedFeedbackQuestions(environment, feedbackDocument.questions);
  await seedCourse(environment, feedbackLinks);
  await seedTrainingModules(environment, trainingDocument.modules);
  await seedRegistrationReferenceData(environment, referenceDocument);

  console.log("Seed complete. Publish any pending entries in Contentful if needed.");
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
