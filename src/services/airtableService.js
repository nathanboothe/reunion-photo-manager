// Thin wrapper around the Airtable REST API. Every other module (routes,
// the sync service) goes through this rather than calling fetch() directly,
// so Airtable's field names only need to be known in one place.
const config = require('../config');

const BASE_URL = `https://api.airtable.com/v0/${config.airtable.baseId}`;

function headers() {
  return {
    Authorization: `Bearer ${config.airtable.apiKey}`,
    'Content-Type': 'application/json',
  };
}

async function listRecords(table, filterFormula) {
  const all = [];
  let offset;

  do {
    const params = new URLSearchParams({ pageSize: '100' });
    if (filterFormula) params.set('filterByFormula', filterFormula);
    if (offset) params.set('offset', offset);

    const res = await fetch(`${BASE_URL}/${encodeURIComponent(table)}?${params}`, {
      headers: headers(),
    });
    if (!res.ok) {
      throw new Error(`Airtable list ${table} failed: ${res.status} ${await res.text()}`);
    }
    const body = await res.json();
    all.push(...(body.records || []));
    offset = body.offset;
  } while (offset);

  return all;
}

async function getRecord(table, id) {
  const res = await fetch(`${BASE_URL}/${encodeURIComponent(table)}/${id}`, {
    headers: headers(),
  });
  if (res.status === 404) return null;
  if (!res.ok) {
    throw new Error(`Airtable get ${table}/${id} failed: ${res.status} ${await res.text()}`);
  }
  return res.json();
}

async function createRecord(table, fields) {
  const res = await fetch(`${BASE_URL}/${encodeURIComponent(table)}`, {
    method: 'POST',
    headers: headers(),
    body: JSON.stringify({ fields }),
  });
  if (!res.ok) {
    throw new Error(`Airtable create ${table} failed: ${res.status} ${await res.text()}`);
  }
  return res.json();
}

async function updateRecord(table, id, fields) {
  const res = await fetch(`${BASE_URL}/${encodeURIComponent(table)}/${id}`, {
    method: 'PATCH',
    headers: headers(),
    body: JSON.stringify({ fields }),
  });
  if (!res.ok) {
    throw new Error(`Airtable update ${table}/${id} failed: ${res.status} ${await res.text()}`);
  }
  return res.json();
}

// ---------- Field mapping helpers ----------

const firstLinked = (val) => (Array.isArray(val) && val.length > 0 ? val[0] : '');

function mapAlbum(r) {
  return {
    id: r.id,
    name: r.fields.Name || '',
    driveId: r.fields.DriveId || '',
    oneDriveFolderId: r.fields.OneDriveFolderId || '',
    active: !!r.fields.Active,
  };
}

function mapPhoto(r) {
  return {
    id: r.id,
    albumId: firstLinked(r.fields.Album),
    driveId: r.fields.DriveId || '',
    oneDriveItemId: r.fields.OneDriveItemId || '',
    fileName: r.fields.FileName || '',
    dateTaken: r.fields.DateTaken || null,
    lastSynced: r.fields.LastSynced || null,
  };
}

function mapEntry(r) {
  return {
    id: r.id,
    photoId: firstLinked(r.fields.Photo),
    familyMemberId: firstLinked(r.fields.FamilyMember),
    familyMemberName: r.fields.FamilyMemberName || '',
    type: r.fields.Type === 'Story' ? 'Story' : 'NameTag',
    text: r.fields.Text || '',
    createdAt: r.fields.CreatedAt || null,
  };
}

function mapFamilyMember(r) {
  return {
    id: r.id,
    name: r.fields.Name || '',
    pinHash: r.fields.PinHash || '',
    active: !!r.fields.Active,
  };
}

// ---------- Albums ----------

async function getActiveAlbums() {
  const records = await listRecords(config.airtable.albumsTable, '{Active} = TRUE()');
  return records.map(mapAlbum);
}

// ---------- Photos ----------

async function getPhotosByAlbum(albumId) {
  const records = await listRecords(config.airtable.photosTable);
  return records
    .filter((r) => Array.isArray(r.fields.Album) && r.fields.Album.includes(albumId))
    .map(mapPhoto);
}

async function getPhotoById(photoId) {
  const record = await getRecord(config.airtable.photosTable, photoId);
  return record ? mapPhoto(record) : null;
}

async function findPhotoByOneDriveItemId(oneDriveItemId) {
  const formula = `{OneDriveItemId} = '${oneDriveItemId}'`;
  const records = await listRecords(config.airtable.photosTable, formula);
  return records.length ? mapPhoto(records[0]) : null;
}

async function upsertPhoto(photo) {
  const existing = await findPhotoByOneDriveItemId(photo.oneDriveItemId);
  const fields = {
    Album: [photo.albumId],
    DriveId: photo.driveId,
    OneDriveItemId: photo.oneDriveItemId,
    FileName: photo.fileName,
    DateTaken: photo.dateTaken || undefined,
    LastSynced: new Date().toISOString(),
  };

  if (existing) {
    await updateRecord(config.airtable.photosTable, existing.id, fields);
  } else {
    await createRecord(config.airtable.photosTable, fields);
  }
}

// ---------- Entries ----------

async function getEntriesForPhoto(photoId) {
  const records = await listRecords(config.airtable.entriesTable);
  return records
    .filter((r) => Array.isArray(r.fields.Photo) && r.fields.Photo.includes(photoId))
    .map(mapEntry)
    .sort((a, b) => new Date(a.createdAt) - new Date(b.createdAt));
}

async function addEntry(entry) {
  const fields = {
    Photo: [entry.photoId],
    FamilyMember: [entry.familyMemberId],
    FamilyMemberName: entry.familyMemberName,
    Type: entry.type === 'Story' ? 'Story' : 'Name tag',
    Text: entry.text,
    CreatedAt: new Date().toISOString(),
  };
  await createRecord(config.airtable.entriesTable, fields);
}

// ---------- Family members ----------

async function getActiveFamilyMembers() {
  const records = await listRecords(config.airtable.familyMembersTable, '{Active} = TRUE()');
  return records.map(mapFamilyMember);
}

// ---------- Runtime config (rotating OneDrive refresh token) ----------

async function getConfigValue(key) {
  const formula = `{Key} = '${key}'`;
  const records = await listRecords(config.airtable.configTable, formula);
  return records.length ? records[0].fields.Value : null;
}

async function setConfigValue(key, value) {
  const formula = `{Key} = '${key}'`;
  const records = await listRecords(config.airtable.configTable, formula);
  const fields = { Key: key, Value: value };

  if (records.length) {
    await updateRecord(config.airtable.configTable, records[0].id, fields);
  } else {
    await createRecord(config.airtable.configTable, fields);
  }
}

module.exports = {
  getActiveAlbums,
  getPhotosByAlbum,
  getPhotoById,
  findPhotoByOneDriveItemId,
  upsertPhoto,
  getEntriesForPhoto,
  addEntry,
  getActiveFamilyMembers,
  getConfigValue,
  setConfigValue,
};
