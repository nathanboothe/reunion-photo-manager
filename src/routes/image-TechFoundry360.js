const express = require('express');
const { Readable } = require('stream');
const airtable = require('../services/airtableService');
const graph = require('../modules/oneDriveSync/graphApiClient');
const { requireAuth } = require('../middleware/auth');

const router = express.Router();

// Streams photo bytes from OneDrive on demand rather than caching Graph
// API's short-lived download URLs - this endpoint always fetches fresh, so
// links in the gallery never go stale.
router.get('/image/:photoId', requireAuth, async (req, res, next) => {
  try {
    const photo = await airtable.getPhotoById(req.params.photoId);
    if (!photo) return res.status(404).end();

    const thumbnail = req.query.thumb === 'true';
    const { body, contentType } = await graph.getImageStream(photo.driveId, photo.oneDriveItemId, thumbnail);

    res.setHeader('Content-Type', contentType);
    Readable.fromWeb(body).pipe(res);
  } catch (err) {
    next(err);
  }
});

module.exports = router;
