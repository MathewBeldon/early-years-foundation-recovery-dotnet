const fs = require("fs");
const path = require("path");
const { loadEnv, trimValue } = require("./load-env");

const EXIT_CODES = Object.freeze({
  OK: 0,
  MISSING_CONFIG: 2,
  NETWORK_OR_AUTH: 3,
  MALFORMED_CONTENT: 4,
  CUTOVER_BLOCKER: 5,
});

const REQUIRED_PAGE_TYPES = [
  "text_page",
  "video_page",
  "assessment_intro",
  "confidence_intro",
  "recap_page",
  "summary_intro",
  "assessment_results",
  "formative",
  "feedback",
  "thankyou",
  "certificate",
];

const KNOWN_PAGE_TYPES = new Set([
  ...REQUIRED_PAGE_TYPES,
  "interruption_page",
  "sub_module_intro",
  "topic_intro",
  "summative",
  "confidence",
  "pre_confidence",
]);

const REPOSITORY_ROOT = path.resolve(__dirname, "..", "..", "..");
const DEFAULT_REPORT = path.join(REPOSITORY_ROOT, "TestResults", "contentful-inventory.json");

function safeText(value) {
  return String(value ?? "")
    .replace(/access_token=[^&\s]+/gi, "access_token=[REDACTED]")
    .replace(/(authorization\s*:\s*(?:bearer\s+)?)[^\s,]+/gi, "$1[REDACTED]")
    .replace(/CFPAT-[A-Za-z0-9._~-]+/g, "[REDACTED]");
}

function localized(value) {
  if (value === null || value === undefined) return value;
  if (typeof value !== "object" || Array.isArray(value)) return value;
  for (const locale of ["en-US", "en-GB", "default"]) {
    if (Object.prototype.hasOwnProperty.call(value, locale)) return value[locale];
  }
  const values = Object.values(value);
  return values.length === 1 ? values[0] : value;
}

function field(fields, ...names) {
  for (const name of names) {
    if (fields && Object.prototype.hasOwnProperty.call(fields, name)) {
      return localized(fields[name]);
    }
  }
  return undefined;
}

function contentType(entry) {
  return entry?.sys?.contentType?.sys?.id ?? entry?.sys?.contentType?.id;
}

function entryId(entry) {
  return entry?.sys?.id;
}

function entryLocation(entry, fallback, options = {}) {
  return options.includeEntryIds && entryId(entry) ? entryId(entry) : fallback;
}

function referenceId(value) {
  const id = value?.sys?.id;
  return typeof id === "string" && id.length > 0 ? id : undefined;
}

function parseAnswers(value, location, malformed, unsupported) {
  if (value === undefined || value === null) {
    // Rails' Training::Question#json supplies two draft options when the
    // external answers field is nil. Keep that effective shape visible in the
    // aggregate without claiming that Contentful contains answer text.
    return { options: [{ correct: false }, { correct: true }], supplied: false, defaulted: true };
  }

  let parsed = value;
  if (typeof value === "string") {
    try {
      parsed = JSON.parse(value);
    } catch {
      malformed.push({ location, reason: "answers is not valid JSON" });
      return { options: [], supplied: true };
    }
  }

  if (!Array.isArray(parsed)) {
    malformed.push({ location, reason: "answers is not an array" });
    return { options: [], supplied: true };
  }

  const options = [];
  parsed.forEach((option, index) => {
    if (Array.isArray(option)) {
      if (typeof option[0] !== "string" || option[0].trim() === "") {
        malformed.push({ location: `${location}[${index}]`, reason: "array option has no non-empty text" });
        return;
      }
      if (option.length > 1 && option[1] !== undefined && option[1] !== null && typeof option[1] !== "boolean") {
        malformed.push({ location: `${location}[${index}]`, reason: "array option correctness is not boolean" });
        return;
      }
      options.push({ correct: option[1] === true });
      return;
    }

    if (option && typeof option === "object" && !Array.isArray(option)) {
      if (typeof option.text !== "string" || option.text.trim() === "") {
        malformed.push({ location: `${location}[${index}]`, reason: "object option has no non-empty text" });
        return;
      }
      if (option.correct !== undefined && option.correct !== null && typeof option.correct !== "boolean") {
        malformed.push({ location: `${location}[${index}]`, reason: "object option correctness is not boolean" });
        return;
      }
      options.push({ correct: option.correct === true });
      return;
    }

    unsupported.push({ location: `${location}[${index}]`, reason: "answer option is neither a Rails array nor object" });
  });

  return { options, supplied: true };
}

function createEntryIndex(payload, malformed) {
  const entries = new Map();
  const add = (entry, source) => {
    if (!entry || typeof entry !== "object") {
      malformed.push({ location: source, reason: "entry is not an object" });
      return;
    }
    const id = entryId(entry);
    const type = contentType(entry);
    if (!id || !type) {
      malformed.push({ location: source, reason: "entry has no id or content type" });
      return;
    }
    entries.set(id, entry);
  };

  const items = Array.isArray(payload?.items) ? payload.items : [];
  const includedEntries = Array.isArray(payload?.includes?.Entry) ? payload.includes.Entry : [];
  for (const [index, entry] of items.entries()) add(entry, `items[${index}]`);
  for (const [index, entry] of includedEntries.entries()) add(entry, `includes.Entry[${index}]`);
  return entries;
}

function modulePages(module, entries, location, malformed) {
  const links = field(module.fields, "pages");
  if (!Array.isArray(links)) {
    malformed.push({ location: `${location}.pages`, reason: "module pages is not an array" });
    return [];
  }

  return links.map((link, index) => {
    const id = referenceId(link);
    if (!id) {
      malformed.push({ location: `${location}.pages[${index}]`, reason: "page link has no entry id" });
      return null;
    }
    const page = entries.get(id);
    if (!page) {
      malformed.push({ location: `${location}.pages[${index}]`, reason: "page link is not present in the Delivery response" });
      return null;
    }
    if (contentType(page) !== "page" && contentType(page) !== "question") {
      malformed.push({ location: `${location}.pages[${index}]`, reason: "module link does not target page/question content" });
      return null;
    }
    return page;
  }).filter(Boolean);
}

function pageTypeOf(entry) {
  return field(entry.fields, "page_type", "pageType");
}

function summarizePageTypes(pages) {
  const counts = {};
  for (const page of pages) {
    const type = pageTypeOf(page) ?? "<missing>";
    counts[type] = (counts[type] ?? 0) + 1;
  }
  return counts;
}

function sectionCounts(pages) {
  const content = pages.filter(page => pageTypeOf(page) !== "interruption_page");
  let submoduleCount = 0;
  let topicCount = 0;
  let previousWasFeedback = false;
  let startsSection = true;
  for (const page of content) {
    const type = pageTypeOf(page);
    const firstFeedback = type === "feedback" && !previousWasFeedback;
    const isSection = ["sub_module_intro", "summary_intro", "certificate"].includes(type) || firstFeedback;
    const isSubsection = ["topic_intro", "recap_page", "assessment_intro", "confidence_intro", "certificate"].includes(type);
    if (isSection) submoduleCount++;
    if (isSection) startsSection = true;
    if (startsSection || isSubsection) {
      topicCount++;
      startsSection = false;
    }
    previousWasFeedback = type === "feedback";
  }
  return { submoduleCount, topicCount };
}

function integritySummary(module, pages, malformed, unsupported, options = {}) {
  const types = pages.map(pageTypeOf);
  const presence = Object.fromEntries(REQUIRED_PAGE_TYPES.map(type => [type, types.filter(item => item === type).length]));
  const hasPreConfidence = types.includes("pre_confidence");
  const requiredMissing = REQUIRED_PAGE_TYPES.filter(type => presence[type] === 0);
  const order = {
    first: types[0] ?? null,
    second: types[1] ?? null,
    third: types[2] ?? null,
    penultimate: types.length > 1 ? types[types.length - 2] : null,
    last: types.at(-1) ?? null,
  };
  const expectedOrder = {
    first: "interruption_page",
    second: hasPreConfidence ? "text_page" : "sub_module_intro",
    third: hasPreConfidence ? "pre_confidence" : "topic_intro",
    penultimate: "thankyou",
    last: "certificate",
  };
  const orderMismatches = Object.keys(expectedOrder)
    .filter(key => order[key] !== expectedOrder[key])
    .map(key => `${key} expected ${expectedOrder[key]}, found ${order[key] ?? "<missing>"}`);

  const factual = pages.filter(page => ["formative", "summative"].includes(pageTypeOf(page)));
  const questionSummary = { total: pages.filter(page => pageTypeOf(page) === "formative" || pageTypeOf(page) === "summative").length, options: 0, correctOptions: 0, multiSelect: 0, defaultedAnswers: 0 };
  for (const [index, page] of factual.entries()) {
    const answerResult = parseAnswers(field(page.fields, "answers"), `${entryLocation(page, `pages[${index}]`, options)}.answers`, malformed, unsupported);
    questionSummary.options += answerResult.options.length;
    questionSummary.correctOptions += answerResult.options.filter(option => option.correct).length;
    if (answerResult.options.filter(option => option.correct).length >= 2) questionSummary.multiSelect++;
    if (answerResult.defaulted) questionSummary.defaultedAnswers++;
    if (answerResult.supplied && answerResult.options.length < 2) {
      malformed.push({ location: `${entryLocation(page, `pages[${index}]`, options)}.answers`, reason: "factual question has fewer than two usable options" });
    }
    if (answerResult.options.length > 0 && answerResult.options.every(option => !option.correct)) {
      malformed.push({ location: `${entryLocation(page, `pages[${index}]`, options)}.answers`, reason: "factual question has no correct option" });
    }
    void index;
  }

  const blockers = [];
  const sections = sectionCounts(pages);
  if (requiredMissing.length) blockers.push(`missing required page types: ${requiredMissing.join(", ")}`);
  if (orderMismatches.length) blockers.push(`page order mismatch: ${orderMismatches.join("; ")}`);
  if (presence.summative !== 10) blockers.push(`expected 10 summative questions, found ${presence.summative}`);
  if (presence.confidence < 4) blockers.push(`expected at least 4 confidence questions, found ${presence.confidence}`);
  if (hasPreConfidence && presence.pre_confidence < 4) blockers.push(`expected at least 4 pre_confidence questions, found ${presence.pre_confidence}`);
  if (sections.topicCount < sections.submoduleCount) blockers.push(`expected topic count (${sections.topicCount}) to be at least submodule count (${sections.submoduleCount})`);

  return {
    name: field(module.fields, "name") ?? (options.includeEntryIds ? entryId(module) : "<unnamed-module>"),
    entryId: entryId(module),
    pageCount: pages.length,
    pageTypes: summarizePageTypes(pages),
    pageOrder: types,
    integrity: {
      requiredPageTypes: presence,
      missingRequiredPageTypes: requiredMissing,
      order,
      expectedOrder,
      orderMismatches,
      summativeCount: presence.summative,
      confidenceCount: presence.confidence,
      preConfidenceCount: presence.pre_confidence ?? 0,
      sectionCounts: sections,
      valid: blockers.length === 0 && malformed.length === 0,
    },
    questions: questionSummary,
    blockers,
  };
}

function buildReport(payload, options = {}) {
  const malformed = [];
  const unsupported = [];
  if (!payload || typeof payload !== "object" || !Array.isArray(payload.items)) {
    malformed.push({ location: "response", reason: "Delivery response has no items array" });
  }

  const entries = createEntryIndex(payload ?? {}, malformed);
  const allEntries = [...entries.values()];
  const contentTypes = {};
  const pageTypes = {};
  const addCount = (map, key) => { map[key] = (map[key] ?? 0) + 1; };
  for (const entry of allEntries) {
    const type = contentType(entry) ?? "<missing>";
    addCount(contentTypes, type);
    const pageType = pageTypeOf(entry);
    if (pageType !== undefined) addCount(pageTypes, pageType);
    if (pageType && !KNOWN_PAGE_TYPES.has(pageType)) {
      unsupported.push({ location: `${entryLocation(entry, `entries[${allEntries.indexOf(entry)}]`, options)}.page_type`, reason: `unsupported page type ${pageType}` });
    }
  }

  const modules = allEntries.filter(entry => contentType(entry) === "trainingModule");
  const moduleSummaries = [];
  let blockers = [];

  for (const [index, module] of modules.entries()) {
    for (const requiredField of ["name", "title", "about", "description", "outcomes", "criteria", "duration", "position", "live", "image", "pages"]) {
      if (field(module.fields, requiredField) === undefined) {
        malformed.push({ location: `${entryLocation(module, `modules[${index}]`, options)}.${requiredField}`, reason: "training module field is missing" });
      }
    }
    const pages = modulePages(module, entries, `modules[${index}]`, malformed);
    const summary = integritySummary(module, pages, malformed, unsupported, options);
    const live = field(module.fields, "live") === true;
    summary.live = live;
    if (!live) summary.integrity.reviewOnly = true;
    if (live) blockers = blockers.concat(summary.blockers.map(reason => `${summary.name}: ${reason}`));
    if (!options.includeEntryIds) delete summary.entryId;
    moduleSummaries.push(summary);
  }

  const questionTypes = new Set(["formative", "summative", "confidence", "feedback"]);
  const questionTotals = { total: 0, options: 0, correctOptions: 0, multiSelect: 0, defaultedAnswers: 0 };
  for (const entry of allEntries) {
    const pageType = pageTypeOf(entry);
    if (!questionTypes.has(pageType)) continue;
    questionTotals.total++;
    if (["formative", "summative"].includes(pageType)) {
      const answerResult = parseAnswers(field(entry.fields, "answers"), `${entryLocation(entry, `entries[${allEntries.indexOf(entry)}]`, options)}.answers`, malformed, unsupported);
      questionTotals.options += answerResult.options.length;
      questionTotals.correctOptions += answerResult.options.filter(option => option.correct).length;
      if (answerResult.options.filter(option => option.correct).length >= 2) questionTotals.multiSelect++;
      if (answerResult.defaulted) questionTotals.defaultedAnswers++;
    } else {
      const options = field(entry.fields, "options");
      if (options === undefined || options === null) continue;
      if (!Array.isArray(options)) {
        malformed.push({ location: `${entryLocation(entry, `entries[${allEntries.indexOf(entry)}]`, options)}.options`, reason: "feedback options is not an array" });
        continue;
      }
      questionTotals.options += options.length;
      if (field(entry.fields, "multi_select") === true) questionTotals.multiSelect++;
    }
  }

  const report = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    readOnly: true,
    contentTypes,
    pageTypes,
    modules: moduleSummaries,
    questions: {
      ...questionTotals,
      byPageType: {
        formative: pageTypes.formative ?? 0,
        summative: pageTypes.summative ?? 0,
        confidence: pageTypes.confidence ?? 0,
        feedback: pageTypes.feedback ?? 0,
      },
    },
    malformedContent: malformed,
    unsupportedShapes: unsupported,
    cutover: {
      liveModuleCount: moduleSummaries.filter(module => module.live).length,
      blockerCount: blockers.length,
      blockers,
      status: malformed.length ? "malformed-content" : blockers.length || unsupported.length ? "blocked" : "ready",
    },
  };

  return report;
}

function classifyReport(report) {
  if (report.malformedContent.length) return EXIT_CODES.MALFORMED_CONTENT;
  if (report.cutover.blockerCount || report.unsupportedShapes.length) return EXIT_CODES.CUTOVER_BLOCKER;
  return EXIT_CODES.OK;
}

function classifyHttpStatus(status) {
  return status === 401 || status === 403 ? EXIT_CODES.NETWORK_OR_AUTH : EXIT_CODES.NETWORK_OR_AUTH;
}

function configFromEnvironment() {
  const spaceId = trimValue(process.env.CONTENTFUL_SPACE_ID ?? "");
  const environmentId = trimValue(process.env.CONTENTFUL_ENVIRONMENT ?? "") || "master";
  const token = trimValue(process.env.CONTENTFUL_DELIVERY_API_KEY ?? "");
  if (!spaceId || !token) {
    const missing = [!spaceId && "CONTENTFUL_SPACE_ID", !token && "CONTENTFUL_DELIVERY_API_KEY"].filter(Boolean);
    const error = new Error(`Missing Contentful configuration: ${missing.join(" and ")}`);
    error.exitCode = EXIT_CODES.MISSING_CONFIG;
    throw error;
  }
  return { spaceId, environmentId, token };
}

async function fetchEntries(config, fetchImpl = fetch) {
  const base = `https://cdn.contentful.com/spaces/${encodeURIComponent(config.spaceId)}/environments/${encodeURIComponent(config.environmentId)}/entries`;
  const all = { items: [], includes: { Entry: [] } };
  let skip = 0;
  let total;
  try {
    do {
      const url = new URL(base);
      url.searchParams.set("limit", "1000");
      url.searchParams.set("skip", String(skip));
      url.searchParams.set("include", "10");
      const response = await fetchImpl(url, {
        headers: { Authorization: `Bearer ${config.token}` },
      });
      let body;
      try { body = await response.json(); } catch { body = null; }
      if (!response.ok) {
        const error = new Error(`Contentful Delivery API returned HTTP ${response.status}`);
        error.exitCode = classifyHttpStatus(response.status);
        throw error;
      }
      if (!body || !Array.isArray(body.items)) {
        const error = new Error("Contentful Delivery API returned an invalid response");
        error.exitCode = EXIT_CODES.NETWORK_OR_AUTH;
        throw error;
      }
      all.items.push(...body.items);
      all.includes.Entry.push(...(body.includes?.Entry ?? []));
      total = Number.isInteger(body.total) ? body.total : all.items.length;
      skip += body.items.length;
      if (body.items.length === 0 || skip >= total) break;
    } while (true);
  } catch (error) {
    if (error.exitCode) throw error;
    const wrapped = new Error(`Could not read Contentful Delivery API: ${safeText(error.message ?? error)}`);
    wrapped.exitCode = EXIT_CODES.NETWORK_OR_AUTH;
    throw wrapped;
  }
  return all;
}

function parseArgs(argv) {
  const args = { report: DEFAULT_REPORT, fixture: null, includeEntryIds: false };
  for (let index = 0; index < argv.length; index++) {
    const arg = argv[index];
    if (arg === "--fixture") args.fixture = argv[++index];
    else if (arg === "--report") args.report = argv[++index];
    else if (arg === "--include-entry-ids") args.includeEntryIds = true;
    else if (arg === "--help" || arg === "-h") args.help = true;
    else throw new Error(`Unknown argument: ${arg}`);
  }
  return args;
}

function resolveReportPath(report) {
  if (path.isAbsolute(report)) return report;
  if (report === "TestResults" || report.startsWith(`TestResults${path.sep}`) || report.startsWith("TestResults/")) {
    return path.resolve(REPOSITORY_ROOT, report);
  }
  return path.resolve(report);
}

function summary(report) {
  const live = report.cutover.liveModuleCount;
  const blocker = report.cutover.blockerCount + report.unsupportedShapes.length;
  return [
    `Contentful inventory: ${live} live module(s), ${report.questions.total} factual question(s), ${report.questions.options} option(s), ${report.questions.correctOptions} correct option(s), ${report.questions.multiSelect} multi-select question(s).`,
    `Content types: ${Object.entries(report.contentTypes).map(([key, value]) => `${key}=${value}`).join(", ") || "none"}.`,
    `Cutover status: ${report.cutover.status}${blocker ? ` (${blocker} blocker finding(s))` : ""}. Report: ${report.reportPath ?? DEFAULT_REPORT}`,
  ].join("\n");
}

async function run(argv = process.argv.slice(2)) {
  loadEnv();
  let args;
  try {
    args = parseArgs(argv);
  } catch (error) {
    console.error(safeText(error.message));
    return EXIT_CODES.MISSING_CONFIG;
  }
  if (args.help) {
    console.log("Usage: npm run inventory [--report TestResults/contentful-inventory.json] [--include-entry-ids]");
    console.log("Offline tests may use --fixture path/to/delivery-response.json; fixture mode never reads the network or credentials.");
    return EXIT_CODES.OK;
  }

  let payload;
  try {
    if (args.fixture) {
      payload = JSON.parse(fs.readFileSync(path.resolve(args.fixture), "utf8"));
    } else {
      payload = await fetchEntries(configFromEnvironment());
    }
  } catch (error) {
    const code = error.exitCode ?? EXIT_CODES.MALFORMED_CONTENT;
    console.error(safeText(error.message ?? error));
    return code;
  }

  const report = buildReport(payload, args);
  const reportPath = resolveReportPath(args.report);
  report.reportPath = args.report;
  fs.mkdirSync(path.dirname(reportPath), { recursive: true });
  fs.writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
  console.log(summary(report));
  return classifyReport(report);
}

if (require.main === module) {
  run().then(code => process.exitCode = code).catch(error => {
    console.error(safeText(error.message ?? error));
    process.exitCode = EXIT_CODES.MALFORMED_CONTENT;
  });
}

module.exports = {
  EXIT_CODES,
  REQUIRED_PAGE_TYPES,
  buildReport,
  classifyHttpStatus,
  classifyReport,
  configFromEnvironment,
  fetchEntries,
  parseAnswers,
  run,
  safeText,
};
