// Every optional feature (OneDrive sync today; things like reactions, a
// slideshow mode, or a "download original" button later) is a module with
// a register(app) function. server.js calls registerAll() once at startup.
//
// To add a new feature later:
//   1. Create a new folder under src/modules/ (e.g. modules/reactions).
//   2. Export a register(app) function from its index.js.
//   3. Add one line to the modules array below.
// Nothing else in the app needs to change.
const oneDriveSyncModule = require('./oneDriveSync');

const modules = [oneDriveSyncModule];

function registerAll(app) {
  for (const mod of modules) {
    mod.register(app);
  }
}

module.exports = { registerAll };
