// Talks to Microsoft Graph for a work/school OneDrive account. Handles the
// token refresh dance, including the fact that refresh tokens rotate on
// every use - each refresh call returns a brand new refresh token, and the
// old one stops working. We persist the latest one in Airtable so this
// survives app restarts and redeploys.
const config = require('../../config');
const airtable = require('../../services/airtableService');

const GRAPH_BASE = 'https://graph.microsoft.com/v1.0';
const CONFIG_KEY_REFRESH_TOKEN = 'OneDriveRefreshToken';

let accessToken = null;
let accessTokenExpiresAt = 0;

async function getAccessToken() {
  if (accessToken && Date.now() < accessTokenExpiresAt) {
    return accessToken;
  }

  const tokenEndpoint = `https://login.microsoftonline.com/${config.graph.tenantId}/oauth2/v2.0/token`;

  // Prefer the most recently persisted refresh token (it may have rotated
  // since the app started); fall back to the configured seed value the
  // very first time the app runs.
  const storedToken = await airtable.getConfigValue(CONFIG_KEY_REFRESH_TOKEN);
  const refreshToken = storedToken || config.graph.refreshToken;

  const form = new URLSearchParams({
    client_id: config.graph.clientId,
    client_secret: config.graph.clientSecret,
    grant_type: 'refresh_token',
    refresh_token: refreshToken,
    scope: 'Files.Read offline_access',
  });

  const res = await fetch(tokenEndpoint, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: form,
  });

  const body = await res.json();

  if (!res.ok) {
    console.error('OneDrive token refresh failed:', body);
    throw new Error('Failed to refresh Microsoft Graph access token. See logs for details.');
  }

  accessToken = body.access_token;
  accessTokenExpiresAt = Date.now() + (body.expires_in - 60) * 1000; // refresh a minute early

  // Persist the new refresh token - it rotates on every use and invalidates
  // the previous one.
  if (body.refresh_token) {
    await airtable.setConfigValue(CONFIG_KEY_REFRESH_TOKEN, body.refresh_token);
  }

  return accessToken;
}

async function listFolderItems(driveId, folderId) {
  const token = await getAccessToken();
  const url = `${GRAPH_BASE}/drives/${driveId}/items/${folderId}/children?$select=id,name,file,photo,image,createdDateTime`;

  const res = await fetch(url, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) {
    throw new Error(`Graph list folder items failed: ${res.status} ${await res.text()}`);
  }

  const body = await res.json();
  return (body.value || []).filter((item) => !!item.file); // skip subfolders
}

// Streams the actual image bytes for a photo. Used by the /image/:photoId
// proxy route so the browser never needs a direct (short-lived) OneDrive
// download URL - it always asks our server, which fetches fresh each time.
async function getImageStream(driveId, itemId, thumbnail) {
  const token = await getAccessToken();
  const url = thumbnail
    ? `${GRAPH_BASE}/drives/${driveId}/items/${itemId}/thumbnails/0/large/content`
    : `${GRAPH_BASE}/drives/${driveId}/items/${itemId}/content`;

  const res = await fetch(url, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!res.ok) {
    throw new Error(`Graph get image failed: ${res.status} ${await res.text()}`);
  }

  return {
    body: res.body, // a web ReadableStream, pipeable straight into the Express response
    contentType: res.headers.get('content-type') || 'image/jpeg',
  };
}

module.exports = { listFolderItems, getImageStream };
