const { loadEnv, trimValue } = require("./load-env");

loadEnv();

const spaceId = trimValue(process.env.CONTENTFUL_SPACE_ID);
const environmentId = trimValue(process.env.CONTENTFUL_ENVIRONMENT) || "master";
const token = trimValue(process.env.CONTENTFUL_DELIVERY_API_KEY);

if (!spaceId || !token) {
  console.error("Set CONTENTFUL_SPACE_ID and CONTENTFUL_DELIVERY_API_KEY in .env");
  process.exit(1);
}

const base = `https://cdn.contentful.com/spaces/${spaceId}/environments/${environmentId}`;

async function fetchJson(path) {
  const url = `${base}${path}${path.includes("?") ? "&" : "?"}access_token=${encodeURIComponent(token)}`;
  const response = await fetch(url);
  const body = await response.json();
  return { status: response.status, body };
}

async function main() {
  console.log(`Testing Delivery API for space ${spaceId}, environment ${environmentId}\n`);

  for (const contentType of ["static", "trainingModule", "course"]) {
    const { status, body } = await fetchJson(`/entries?content_type=${contentType}&limit=3`);
    const count = body.items?.length ?? 0;
    console.log(
      `${contentType}: HTTP ${status}, published entries: ${count}${body.message ? ` (${body.message})` : ""}`,
    );
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
