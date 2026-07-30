const jwt = require('jsonwebtoken');
const config = require('../config');

const COOKIE_NAME = 'reunion_auth';

function signSession(member) {
  return jwt.sign({ sub: member.id, name: member.name }, config.sessionSecret, {
    expiresIn: '30d',
  });
}

// Runs on every request. Decodes the session cookie if present and attaches
// req.user - but never blocks the request. Use requireAuth (below) on
// routes that actually need to enforce login.
function attachUser(req, res, next) {
  const token = req.cookies[COOKIE_NAME];
  if (token) {
    try {
      const payload = jwt.verify(token, config.sessionSecret);
      req.user = { id: payload.sub, name: payload.name };
    } catch {
      req.user = null;
    }
  } else {
    req.user = null;
  }
  res.locals.user = req.user;
  next();
}

function requireAuth(req, res, next) {
  if (!req.user) {
    const returnUrl = encodeURIComponent(req.originalUrl);
    return res.redirect(`/login?returnUrl=${returnUrl}`);
  }
  next();
}

function setSessionCookie(res, member) {
  const token = signSession(member);
  res.cookie(COOKIE_NAME, token, {
    httpOnly: true,
    // The browser enforces this based on the URL scheme *it* used to reach
    // the site - so on Render (https://yourapp.onrender.com) this is
    // correctly sent even though Render forwards plain http internally.
    // Locally (NODE_ENV isn't 'production'), this stays false so the
    // cookie also works over plain http://localhost during development.
    secure: config.nodeEnv === 'production',
    sameSite: 'lax',
    maxAge: 30 * 24 * 60 * 60 * 1000, // 30 days
  });
}

function clearSessionCookie(res) {
  res.clearCookie(COOKIE_NAME);
}

module.exports = { attachUser, requireAuth, setSessionCookie, clearSessionCookie };
