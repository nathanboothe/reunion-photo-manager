const express = require('express');
const airtable = require('../services/airtableService');
const { requireAuth } = require('../middleware/auth');

const router = express.Router();

router.get('/photo/:photoId', requireAuth, async (req, res, next) => {
  try {
    const photo = await airtable.getPhotoById(req.params.photoId);
    if (!photo) return res.status(404).render('error', { message: "That photo couldn't be found." });

    const entries = await airtable.getEntriesForPhoto(photo.id);
    res.render('photo', { photo, entries });
  } catch (err) {
    next(err);
  }
});

router.post('/photo/:photoId', requireAuth, async (req, res, next) => {
  try {
    const photo = await airtable.getPhotoById(req.params.photoId);
    if (!photo) return res.status(404).render('error', { message: "That photo couldn't be found." });

    const text = (req.body.text || '').trim();
    if (text) {
      await airtable.addEntry({
        photoId: photo.id,
        familyMemberId: req.user.id,
        familyMemberName: req.user.name,
        type: req.body.type === 'Story' ? 'Story' : 'NameTag',
        text,
      });
    }

    res.redirect(`/photo/${photo.id}`);
  } catch (err) {
    next(err);
  }
});

module.exports = router;
