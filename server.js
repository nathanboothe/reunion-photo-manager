const express = require('express');
const cookieParser = require('cookie-parser');
const path = require('path');

const config = require('./src/config');
const { attachUser } = require('./src/middleware/auth');
const moduleRegistry = require('./src/modules/moduleRegistry');

const loginRoutes = require('./src/routes/login');
const galleryRoutes = require('./src/routes/index');
const photoRoutes = require('./src/routes/photo');
const imageRoutes = require('./src/routes/image');

const app = express();

// Render sits in front of the app as a reverse proxy; this lets req.ip and
// friends reflect the real client rather than Render's internal address.
app.set('trust proxy', 1);

app.set('view engine', 'ejs');
app.set('views', path.join(__dirname, 'views'));

app.use(express.urlencoded({ extended: false }));
app.use(cookieParser());
app.use(express.static(path.join(__dirname, 'public')));
app.use(attachUser);

app.use(loginRoutes);
app.use(galleryRoutes);
app.use(photoRoutes);
app.use(imageRoutes);

// Fallback error handler - keeps a stray exception from leaking a stack
// trace to a family member's browser.
app.use((err, req, res, next) => {
  console.error(err);
  res.status(500).render('error', { message: 'Something went wrong. Please try again.' });
});

if (!config.sessionSecret) {
  console.warn(
    'WARNING: SESSION_SECRET is not set. Set it to a long random string before deploying - ' +
      'without it, login sessions cannot be securely signed.'
  );
}

moduleRegistry.registerAll(app);

app.listen(config.port, () => {
  console.log(`Reunion Photos listening on http://localhost:${config.port}`);
});
