const syncService = require('./syncService');

function register(app) {
  syncService.start();
}

module.exports = { register };
