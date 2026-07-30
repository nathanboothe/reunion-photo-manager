const express = require('express');
const airtable = require('../services/airtableService');
const { requireAuth } = require('../middleware/auth');

console.log('[DEBUG] routes/index.js loaded');

const router = express.Router();

router.get('/', requireAuth, async (req, res, next) => {
  try {
    const albums = await airtable.getActiveAlbums();
    console.log('[DEBUG] albums returned:', albums.map(a => ({ id: a.id, name: a.name })));

    const photosByAlbum = {};

    for (const album of albums) {
      photosByAlbum[album.id] = await airtable.getPhotosByAlbum(album.id);
      console.log('[DEBUG] photos for album', album.id, ':', photosByAlbum[album.id].length);
    }

    res.render('index', { albums, photosByAlbum });
  } catch (err) {
    console.log('[DEBUG] error in gallery route:', err);
    next(err);
  }
});

module.exports = router;
