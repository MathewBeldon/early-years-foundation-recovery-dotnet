# Contentful tooling (original content only)

This folder holds **original** Contentful migrations and seed scripts for the personal .NET experiment.

Do **not** copy migrations or content from the Rails `cms/` folder or work Contentful space.

## 1. Create a free Contentful space

1. Sign up at [contentful.com](https://www.contentful.com/) (the free plan includes one space).
2. Create a new space — choose **Build your own** (empty space).
3. The default environment is **`master`** (free tier uses this; you do not need a separate `demo` environment).

## 2. Get API keys

### Management token (for migrations and seeding)

1. **Settings → CMA tokens → Create personal access token**
2. Copy the token — you only see it once.

### Delivery API key (for the .NET app)

1. **Settings → API keys → Add API key**
2. Copy the **Space ID** and **Content Delivery API - access token**.

## 3. Configure `.env`

```powershell
cd dotnet/tools/contentful
copy .env.example .env
```

Edit `.env`:

```env
CONTENTFUL_SPACE_ID=your_space_id
CONTENTFUL_ENVIRONMENT=master
CONTENTFUL_MANAGEMENT_TOKEN=your_cma_token
CONTENTFUL_DELIVERY_API_KEY=your_delivery_token
```

## 4. Run migrations and seed

```powershell
npm install
npm run verify
npm run migrate
npm run seed
```

- **`npm run verify`** — checks your token and lists spaces it can access (run this first if migrate fails)
- **`npm run migrate`** — creates content types: `page`, `question`, `trainingModule`, `static`, `course`, plus registration reference data types
- **`npm run seed`** — publishes demo content from `dotnet/data/demo-*.json` and anonymised registration fixtures from `dotnet/data/reference-data.json` into your space

Re-running seed is safe: entries are upserted by name / deterministic id.

## 5. Wire the .NET app

Add your delivery credentials to user secrets (recommended) or `appsettings.Development.json`:

```powershell
cd dotnet/src/EarlyYearsFoundationRecovery.Web
dotnet user-secrets set "Contentful:SpaceId" "your_space_id"
dotnet user-secrets set "Contentful:Environment" "master"
dotnet user-secrets set "Contentful:DeliveryApiKey" "your_delivery_token"
```

When `Contentful:SpaceId` and `Contentful:DeliveryApiKey` are set, the app uses Contentful for:

- Registration reference data (`userSetting`, `registrationRole`, `registrationCountry`, `registrationLocalAuthority`, `registrationExperience`)
- Training modules (`trainingModule` + linked `page` entries)
- Course feedback (`course` + linked `question` entries)
- Static pages (`static` entries)

If those settings are empty, the app falls back to the local JSON files in `dotnet/data/`.

Restart the app and open `/my-modules` — you should see the same Module 1–5 content, now served from Contentful.

## Webhooks (cache bust on publish)

Content is cached in memory (default 5 minutes). To refresh immediately when you publish in Contentful:

### 1. Set a webhook secret in the app

```powershell
cd dotnet/src/EarlyYearsFoundationRecovery.Web
dotnet user-secrets set "Contentful:WebhookSecret" "choose-a-long-random-secret"
```

### 2. Expose your local app (development only)

Contentful needs a public HTTPS URL. Use [ngrok](https://ngrok.com/) or similar:

```powershell
ngrok http 5000
```

Use the ngrok URL in step 3.

### 3. Create the webhook in Contentful

1. Open your space → **Settings → Webhooks → Add webhook**
2. **URL:** `https://YOUR-PUBLIC-URL/contentful/webhook`  
   (Rails parity alias: `/change` also works)
3. **Headers:** add `X-Contentful-Webhook-Secret` = the same secret from step 1  
   (or `BOT` header — same value — if copying Rails-style config)
4. **Environment:** `master` (or your `CONTENTFUL_ENVIRONMENT`)
5. **Triggers:** `Entry` → **Publish** and **Unpublish** (and **Delete** if you want)

After publishing an entry, the app clears the relevant cache. The next page load fetches fresh content from Contentful.

## Content types

| Type | Purpose |
|------|---------|
| `page` | Training pages and in-module questions (formative/summative) |
| `question` | End-of-course feedback questions |
| `trainingModule` | Module metadata and ordered page links |
| `static` | Footer and standalone pages |
| `course` | Service config and feedback form ordering |
| `userSetting` | Registration setting types; uses original field ids `name`, `title`, `local_authority`, `role_type`, `active` |
| `registrationRole` | Registration role options and optional role hints |
| `registrationCountry` | Where-you-live country options |
| `registrationLocalAuthority` | Local authority options |
| `registrationExperience` | Early-years experience options |

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `Set CONTENTFUL_SPACE_ID...` | Fill in `.env` before migrate/seed |
| `space does not exist or you do not have access` | Run `npm run verify` — your token may not see that space ID. Create a space in the Contentful UI (same account as the CMA token), copy **Settings → General settings → Space ID**, update `.env` |
| `Authenticated, but this account has no spaces yet` | Create a space at [app.contentful.com](https://app.contentful.com/) first |
| Used Delivery API key as management token | Management token must start with `CFPAT-` (Settings → CMA tokens, not API keys) |
| App still shows old content | Set up **Webhooks** above, or wait for `CacheMinutes` (default 5), or restart the app |
| Empty modules list | Check entries are **Published** in Contentful; run `npm run test-delivery`; verify `dotnet user-secrets list` has correct Space ID (not `master`) |
| HTTP log shows `spaces/master/entries` | Wrong Contentful SDK client setup — Space ID and Environment were swapped; rebuild and restart the app |
