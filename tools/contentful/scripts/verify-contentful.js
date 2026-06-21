const { createClient } = require("contentful-management");
const { loadEnv } = require("./load-env");

loadEnv();

function trim(value) {
  return (value ?? "").trim().replace(/^["']|["']$/g, "");
}

async function checkDeliveryApi(spaceId, deliveryKey) {
  if (!spaceId || !deliveryKey) {
    return null;
  }

  const url = `https://cdn.contentful.com/spaces/${spaceId}?access_token=${encodeURIComponent(deliveryKey)}`;
  const response = await fetch(url);
  const body = await response.json();

  if (!response.ok) {
    return { ok: false, message: body.message ?? response.statusText };
  }

  return { ok: true, name: body.name, id: body.sys.id };
}

async function main() {
  const spaceId = trim(process.env.CONTENTFUL_SPACE_ID);
  const environmentId = trim(process.env.CONTENTFUL_ENVIRONMENT) || "master";
  const token = trim(process.env.CONTENTFUL_MANAGEMENT_TOKEN);
  const deliveryKey = trim(process.env.CONTENTFUL_DELIVERY_API_KEY);

  if (!token) {
    console.error("Missing CONTENTFUL_MANAGEMENT_TOKEN in .env");
    process.exit(1);
  }

  if (!token.startsWith("CFPAT-")) {
    console.warn(
      "Warning: management token should start with CFPAT-. You may have pasted a Delivery API key by mistake.",
    );
  }

  const client = createClient({ accessToken: token });

  console.log("Checking Contentful credentials...\n");

  let deliveryCheck = null;
  if (spaceId && deliveryKey) {
    try {
      deliveryCheck = await checkDeliveryApi(spaceId, deliveryKey);
      if (deliveryCheck.ok) {
        console.log(`Delivery API: can read space "${deliveryCheck.name}" (${deliveryCheck.id})`);
      } else {
        console.log(`Delivery API: ${deliveryCheck.message}`);
      }
    } catch (error) {
      console.log(`Delivery API: ${error.message ?? error}`);
    }
    console.log();
  }

  let spaces;
  try {
    spaces = await client.getSpaces();
  } catch (error) {
    console.error("Management API: could not authenticate.");
    console.error("Create a token at Contentful → open your space → Settings → CMA tokens");
    console.error(error.message ?? error);
    process.exit(1);
  }

  if (spaces.items.length === 0) {
    console.log("Management API: token is valid, but lists 0 spaces.");

    if (spaceId) {
      try {
        const space = await client.getSpace(spaceId);
        const environment = await space.getEnvironment(environmentId);
        console.log(`\nDirect access to ${spaceId} works: "${space.name}", environment "${environment.sys.id}"`);
        console.log("\nCredentials look good. Run: npm run migrate");
        return;
      } catch (error) {
        const message = error.message ?? String(error);
        console.log(`\nDirect access to configured space ${spaceId} failed.`);

        if (deliveryCheck?.ok) {
          console.log("\nLikely cause: your CMA token is from a different Contentful login than this space.");
          console.log("\nFix:");
          console.log("  1. Log in at https://app.contentful.com/ as the user who owns the Blank space");
          console.log("  2. Open Blank in the space selector (top left)");
          console.log("  3. Settings → CMA tokens → Create personal access token");
          console.log("  4. Replace CONTENTFUL_MANAGEMENT_TOKEN in .env with the new CFPAT-… token");
          console.log("  5. Revoke the old token");
          console.log("\nThe Delivery API key in Settings → API keys is only for reading published content.");
          console.log("Migrate/seed need a CMA token created while your space is open.");
        } else {
          console.log(message);
          console.log("\nCreate a space at https://app.contentful.com/ or fix CONTENTFUL_SPACE_ID in .env");
        }
        process.exit(1);
      }
    }

    console.error("\nCreate a space at https://app.contentful.com/ then re-run npm run verify");
    process.exit(1);
  }

  console.log("Spaces your management token can access:");
  for (const space of spaces.items) {
    const marker = space.sys.id === spaceId ? "  ← configured in .env" : "";
    console.log(`  ${space.sys.id}  ${space.name}${marker}`);
  }
  console.log();

  if (!spaceId) {
    console.error("Set CONTENTFUL_SPACE_ID in .env to one of the IDs above.");
    process.exit(1);
  }

  const configuredSpace = spaces.items.find((space) => space.sys.id === spaceId);
  if (!configuredSpace) {
    console.error(`CONTENTFUL_SPACE_ID=${spaceId} is not in the list above.`);
    console.error("Copy Space ID from Contentful → Settings → General settings.");
    process.exit(1);
  }

  try {
    const space = await client.getSpace(spaceId);
    await space.getEnvironment(environmentId);
    console.log(`Management API OK: ${configuredSpace.name} (${spaceId}), environment ${environmentId}`);
  } catch (error) {
    console.error(`Space ${spaceId} found but environment "${environmentId}" could not be opened.`);
    console.error(error.message ?? error);
    process.exit(1);
  }

  if (deliveryKey) {
    console.log(`Delivery API key present (${deliveryKey.slice(0, 6)}…)`);
  } else {
    console.warn("CONTENTFUL_DELIVERY_API_KEY is not set — needed for the .NET app.");
  }

  console.log("\nCredentials look good. Run: npm run migrate");
}

main().catch((error) => {
  console.error(error.message ?? error);
  process.exit(1);
});
