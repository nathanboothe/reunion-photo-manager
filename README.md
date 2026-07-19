# Reunion Photos (Node.js)

A small web app so family members can browse photos from a shared OneDrive
folder and add name tags or stories to each one — permanently linked to that
exact photo, not just its filename.

- **Auth:** each family member has their own PIN (tracked, so you know who
  wrote what).
- **Storage:** Airtable holds all metadata (albums, photos, comments,
  family members). The actual photo files stay in OneDrive — nothing is
  copied or duplicated.
- **Hosting:** built for Render.
- **Extensible:** new features are self-contained modules under
  `src/modules/` — see "Adding a new feature" below.

---

## 1. Set up Airtable

Create a base with these five tables and exact field names:

**Albums**
| Field | Type |
|---|---|
| Name | Single line text |
| DriveId | Single line text |
| OneDriveFolderId | Single line text |
| Active | Checkbox |

**Photos**
| Field | Type |
|---|---|
| Album | Link to Albums |
| DriveId | Single line text |
| OneDriveItemId | Single line text |
| FileName | Single line text |
| DateTaken | Date (include time) |
| LastSynced | Date (include time) |

**Entries**
| Field | Type |
|---|---|
| Photo | Link to Photos |
| FamilyMember | Link to FamilyMembers |
| FamilyMemberName | Single line text |
| Type | Single select: `Name tag`, `Story` |
| Text | Long text |
| CreatedAt | Date (include time) |

**FamilyMembers**
| Field | Type |
|---|---|
| Name | Single line text |
| PinHash | Long text |
| Active | Checkbox |

**AppConfig**
| Field | Type |
|---|---|
| Key | Single line text |
| Value | Long text |

(`AppConfig` is used internally to persist the OneDrive refresh token,
which rotates automatically — you don't need to touch this table.)

Get your Airtable API key from your account's developer settings (Personal
access tokens, with `data.records:read` and `data.records:write` scope on
this base), and your Base ID from the base's URL or its API documentation
page (Help → API documentation).

### Adding family members

For each person, add a row to `FamilyMembers` with their name and `Active`
checked. For `PinHash`, run the included hashing tool rather than typing
their PIN directly:

```bash
npm run hash-pin
```

It'll ask for the PIN and print a hash. Paste that hash into `PinHash`. The
plain PIN is never stored anywhere by this tool.

### Adding albums

For each OneDrive folder you want to show, add a row to `Albums` with
`Active` checked. You'll fill in `DriveId` and `OneDriveFolderId` in the
next section.

---

## 2. Register an app with Microsoft (for OneDrive access)

Since you're using a work/business account, this happens in your
organization's Microsoft 365 tenant.

1. Go to [entra.microsoft.com](https://entra.microsoft.com) and sign in
   with your business account.
2. **App registrations** → **New registration**.
   - Name: `Reunion Photos` (anything works).
   - Supported account types: **Accounts in this organizational directory
     only (Single tenant)**.
   - Redirect URI: platform **Web**, value
     `https://oauth.pstmn.io/v1/callback` (Postman's own redirect capture
     page — only used once, during setup).
3. Note the **Application (client) ID** and **Directory (tenant) ID** on
   the overview page.
4. **Certificates & secrets** → **New client secret** → copy the value
   immediately.
5. **API permissions** → **Add a permission** → **Microsoft Graph** →
   **Delegated permissions** → add `Files.Read` and `offline_access`. If
   you see an admin-consent warning, click **Grant admin consent** (or ask
   whoever manages your tenant to).

### Get your first refresh token

One-time manual step. In a browser, go to (filling in your own values,
URL-encoded):

```
https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize
  ?client_id={clientId}
  &response_type=code
  &redirect_uri=https://oauth.pstmn.io/v1/callback
  &response_mode=query
  &scope=Files.Read%20offline_access
```

Sign in, approve consent, and you'll be redirected to a `postman://...` URL
containing `?code=...`. Copy that whole code value — it's long, and it
expires in about 10 minutes, so move to the next step right away.

Exchange it for tokens:

```bash
curl -X POST https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token \
  -d "client_id={clientId}" \
  -d "client_secret={clientSecret}" \
  -d "grant_type=authorization_code" \
  -d "code={code}" \
  -d "redirect_uri=https://oauth.pstmn.io/v1/callback" \
  -d "scope=Files.Read offline_access"
```

The response includes a `refresh_token` — that's your `GRAPH_REFRESH_TOKEN`
seed value below. After the app's first sync, it automatically rotates and
persists newer tokens into the `AppConfig` Airtable table, so you only need
to do this once.

### Find your DriveId and folder IDs

Using the `access_token` from the response above:

```bash
curl https://graph.microsoft.com/v1.0/me/drive -H "Authorization: Bearer {access_token}"
curl https://graph.microsoft.com/v1.0/me/drive/root/children -H "Authorization: Bearer {access_token}"
```

The first gives you `DriveId` (the `id` field). The second lists root
folders — find the one you want and copy its `id` as `OneDriveFolderId`.
Put both into the matching `Albums` row in Airtable.

**If you're on PowerShell**, avoid pasting long tokens directly into a
`curl -H "..."` argument — PowerShell can mangle very long quoted strings.
Store the token in a variable first:

```powershell
$token = "paste-your-access-token-here"
curl https://graph.microsoft.com/v1.0/me/drive/root/children -H "Authorization: Bearer $token"
```

---

## 3. Run locally

Requires [Node.js](https://nodejs.org) 18 or newer.

```bash
npm install
cp .env.example .env
```

Open `.env` and fill in every value (Airtable key/base id, Graph client
id/secret/tenant/refresh token, and a `SESSION_SECRET` — generate one with):

```bash
node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"
```

Then start the app:

```bash
npm start
```

Open `http://localhost:3000`. The background sync runs 10 seconds after
startup, then on the interval set by `GRAPH_SYNC_INTERVAL_MINUTES` — watch
the terminal for a line like `Synced N photo(s) for album`, then refresh
the gallery.

---

## 4. Deploy to Render

1. Push this repo to GitHub.
2. In Render: **New** → **Web Service** → connect the repo.
3. Environment: **Node**.
4. Build command: `npm install`
5. Start command: `npm start`
6. Environment variables (Render's dashboard) — set every key from
   `.env.example` with real values, plus:
   | Key | Value |
   |---|---|
   | `NODE_ENV` | `production` |

7. Deploy. Render gives you a public HTTPS URL automatically.

---

## Adding a new feature

Every feature lives in its own folder under `src/modules/` and exports a
`register(app)` function:

```javascript
// src/modules/reactions/index.js
function register(app) {
  // register routes, start background jobs, etc.
}

module.exports = { register };
```

Then add one line to `src/modules/moduleRegistry.js`:

```javascript
const reactionsModule = require('./reactions');
const modules = [oneDriveSyncModule, reactionsModule];
```

Nothing else needs to change. Routes for the new feature go in their own
file under `src/routes/`, and any Airtable fields it needs can be added to
the relevant table without touching existing code.

---

## Security notes

- PINs are hashed with bcrypt before they ever touch Airtable — the app
  never stores or logs a plain PIN.
- Login attempts are rate-limited per IP address (5 failures locks that IP
  out for 15 minutes) to slow down PIN guessing.
- Session cookies are signed (JWT) and `httpOnly`; they're marked `Secure`
  automatically in production so browsers only send them over HTTPS.
- Airtable and OneDrive credentials live only in environment variables /
  `.env` (which is gitignored), never in source control.
- Photos are streamed live from OneDrive on each view rather than cached,
  so there's no separate copy of your family's photos sitting on a server
  disk somewhere.
