const express = require('express');
const pinAuth = require('../services/pinAuthService');
const { setSessionCookie, clearSessionCookie } = require('../middleware/auth');

const router = express.Router();

router.get('/login', (req, res) => {
  res.render('login', { error: null, returnUrl: req.query.returnUrl || '/' });
});

router.post('/login', async (req, res) => {
  const clientKey = req.ip;
  const { pin, returnUrl } = req.body;
  const safeReturnUrl = returnUrl && returnUrl.startsWith('/') ? returnUrl : '/';

  if (pinAuth.isLockedOut(clientKey)) {
    return res.render('login', {
      error: 'Too many attempts. Please try again in a few minutes.',
      returnUrl: safeReturnUrl,
    });
  }

  const member = await pinAuth.validatePin(pin || '', clientKey);
  if (!member) {
    return res.render('login', {
      error: "That PIN wasn't recognized. Please try again.",
      returnUrl: safeReturnUrl,
    });
  }

  setSessionCookie(res, member);
  res.redirect(safeReturnUrl);
});

router.post('/logout', (req, res) => {
  clearSessionCookie(res);
  res.redirect('/login');
});

module.exports = router;
