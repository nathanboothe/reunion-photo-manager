# Reunion Photos

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
  `Modules/` — see "Adding a new feature" below.

---

## 1. Set up Airtable

Create a base (call it whatever you like) with these four tables and exact
field names:

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

(`AppConfig` is used internally by the app to persist the OneDrive refresh
token, which rotates automatically — you don't need to touch this table.)

Get your Airtable API key from your Airtable account's developer settings,
and your Base ID from the base's API documentation page (Help → API
documentation). You'll need both in step 4.

### Adding family members

For each person, add a row to `FamilyMembers` with their name and `Active`
checked. For `PinHash`, don't type their PIN directly — run the hashing
tool included in this repo:

```
cd tools/HashPin
dotnet run
```

It'll ask for the PIN and print a hash. Paste that hash into `PinHash`. The
plain PIN is never stored anywhere — only you and that family member know
it, unless you write it down elsewhere.

### Adding albums

For each OneDrive folder you want to show, add a row to `Albums` with
`Active` checked. You'll fill in `DriveId` and `OneDriveFolderId` in step 3
below, once you can query the Graph API.

---

## 2. Register an app with Microsoft (for OneDrive access)

Since you're using a work/business account, this app registration happens
in your organization's Microsoft 365 tenant rather than against a personal
Microsoft account.

1. Go to [entra.microsoft.com](https://entra.microsoft.com) (or
   portal.azure.com → Microsoft Entra ID) and sign in with your business
   account.
2. **App registrations** → **New registration**.
   - Name: `Reunion Photos` (anything works).
   - Supported account types: **Accounts in this organizational directory
     only (Single tenant)** — this is the right choice unless you
     specifically need people from other organizations to sign in, which
     you don't here.
   - Redirect URI: platform **Web**, value `https://oauth.pstmn.io/v1/callback`
     if you have Postman installed, or `http://localhost:5000/signin` if
     you'd rather capture the code manually. Either is only used once,
     during setup.
3. After registering, note the **Application (client) ID** and the
   **Directory (tenant) ID** shown on the overview page.
4. **Certificates & secrets** → **New client secret** → copy the value
   immediately (it's only shown once).
5. **API permissions** → **Add a permission** → **Microsoft Graph** →
   **Delegated permissions** → add `Files.Read` and `offline_access`.
   `Files.Read` and `offline_access` don't require tenant-admin consent in
   most organizations, but if your account isn't a Global Administrator
   and you see a warning that admin approval is needed, you (or whoever
   manages your tenant) can click **Grant admin consent** on this same
   page — a one-time action.

### Get your first refresh token

This is a one-time manual step. In a browser, go to (replace `{tenantId}`,
`{clientId}`, and `{redirectUri}` with your values, URL-encoded):

```
https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize
  ?client_id={clientId}
  &response_type=code
  &redirect_uri={redirectUri}
  &response_mode=query
  &scope=Files.Read%20offline_access
```

Sign in with your business account, approve the consent screen, and you'll
be redirected with `?code=...` in the URL. Copy that code, then exchange it
for tokens:

```bash
curl -X POST https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token \
  -d "client_id={clientId}" \
  -d "client_secret={clientSecret}" \
  -d "grant_type=authorization_code" \
  -d "code={code}" \
  -d "redirect_uri={redirectUri}" \
  -d "scope=Files.Read offline_access"
```

The response includes a `refresh_token` — that's your `Graph:RefreshToken`
seed value for step 4. After the app's first sync, it'll automatically
rotate and persist newer tokens into the `AppConfig` Airtable table, so you
only need to do this once.

### Find your DriveId and folder IDs

With the access token from the response above:

```bash
# Get your DriveId
curl https://graph.microsoft.com/v1.0/me/drive -H "Authorization: Bearer {access_token}"

# List folders at the root to find a folder's id
curl https://graph.microsoft.com/v1.0/me/drive/root/children -H "Authorization: Bearer {access_token}"
```

Put the resulting `id` (drive) and folder `id` values into the matching
`Albums` row in Airtable.

---

## 3. Run locally

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

Set your secrets locally rather than editing `appsettings.json` directly
(keeps them out of source control):

```bash
cd src/ReunionPhotos.Web
dotnet user-secrets init
dotnet user-secrets set "Airtable:ApiKey" "your-airtable-key"
dotnet user-secrets set "Airtable:BaseId" "your-base-id"
dotnet user-secrets set "Graph:ClientId" "your-client-id"
dotnet user-secrets set "Graph:ClientSecret" "your-client-secret"
dotnet user-secrets set "Graph:TenantId" "your-tenant-id"
dotnet user-secrets set "Graph:RefreshToken" "your-refresh-token"

dotnet run
```

Then open the URL shown in the console.

---

## 4. Deploy to Render

1. Push this repo to GitHub.
2. In Render, **New** → **Web Service** → connect the repo.
3. Root directory: `src/ReunionPhotos.Web`
4. Build command: `dotnet publish -c Release -o out`
5. Start command: `dotnet out/ReunionPhotos.Web.dll`
6. Environment variables (Render's dashboard, not appsettings.json) — note
   ASP.NET Core reads nested config from environment variables using a
   double underscore:

   | Key | Value |
   |---|---|
   | `Airtable__ApiKey` | your Airtable API key |
   | `Airtable__BaseId` | your Airtable base id |
   | `Graph__ClientId` | your Azure app client id |
   | `Graph__ClientSecret` | your Azure app client secret |
   | `Graph__TenantId` | your Azure AD tenant id |
   | `Graph__RefreshToken` | the refresh token from step 2 |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |

7. Deploy. Render gives you a public HTTPS URL automatically.

---

## Adding a new feature

Every feature lives in its own folder under `Modules/` and implements
`IFeatureModule`:

```csharp
public class ReactionsModule : IFeatureModule
{
    public string Name => "Reactions";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // register any services this feature needs
    }
}
```

Then add one line to `Modules/ModuleRegistry.cs`:

```csharp
new ReactionsModule(),
```

Nothing else needs to change. Razor Pages for the new feature go in their
own subfolder under `Pages/`, and any Airtable fields it needs can be added
to the relevant table without touching existing code.

---

## Security notes

- PINs are hashed with bcrypt before they ever touch Airtable — the app
  never stores or logs a plain PIN.
- Login attempts are rate-limited per IP address (5 failures locks that IP
  out for 15 minutes) to slow down PIN guessing.
- The Airtable API key and OneDrive credentials live only in environment
  variables / user secrets, never in source control.
- Photos are streamed live from OneDrive on each view rather than cached,
  so there's no separate copy of your family's photos sitting on a server
  disk somewhere.
