// Loads all configuration from environment variables. Locally these come
// from a .env file (via dotenv); on Render they come from the dashboard's
// Environment tab. Nothing sensitive is hardcoded here.
require('dotenv').config();

module.exports = {
  port: process.env.PORT || 3000,
  nodeEnv: process.env.NODE_ENV || 'production',

  // Used to sign the login session cookie (JWT). Any long random string.
  sessionSecret: process.env.SESSION_SECRET || '',

  airtable: {
    apiKey: process.env.AIRTABLE_API_KEY || '',
    baseId: process.env.AIRTABLE_BASE_ID || '',
    albumsTable: process.env.AIRTABLE_ALBUMS_TABLE || 'Albums',
    photosTable: process.env.AIRTABLE_PHOTOS_TABLE || 'Photos',
    entriesTable: process.env.AIRTABLE_ENTRIES_TABLE || 'Entries',
    familyMembersTable: process.env.AIRTABLE_FAMILY_MEMBERS_TABLE || 'FamilyMembers',
    configTable: process.env.AIRTABLE_CONFIG_TABLE || 'AppConfig',
  },

  graph: {
    clientId: process.env.GRAPH_CLIENT_ID || '',
    clientSecret: process.env.GRAPH_CLIENT_SECRET || '',
    tenantId: process.env.GRAPH_TENANT_ID || '',
    refreshToken: process.env.GRAPH_REFRESH_TOKEN || '',
    syncIntervalMinutes: parseInt(process.env.GRAPH_SYNC_INTERVAL_MINUTES || '60', 10),
  },
};
