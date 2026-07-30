const bcrypt = require('bcryptjs');
const airtable = require('./airtableService');

// Simple in-memory lockout tracking, keyed by caller IP. Resets on restart,
// which is fine for a reunion-scale app - the goal is slowing down casual
// PIN guessing, not defending a high-value target.
const attempts = new Map();
const MAX_FAILURES_BEFORE_LOCKOUT = 5;
const LOCKOUT_MS = 15 * 60 * 1000;

function isLockedOut(clientKey) {
  const entry = attempts.get(clientKey);
  if (!entry) return false;
  return entry.failures >= MAX_FAILURES_BEFORE_LOCKOUT && Date.now() < entry.lockedUntil;
}

function recordFailure(clientKey) {
  const current = attempts.get(clientKey) || { failures: 0, lockedUntil: 0 };
  const failures = current.failures + 1;
  const lockedUntil = failures >= MAX_FAILURES_BEFORE_LOCKOUT ? Date.now() + LOCKOUT_MS : 0;
  attempts.set(clientKey, { failures, lockedUntil });
}

async function validatePin(pin, clientKey) {
  if (isLockedOut(clientKey)) {
    return null;
  }

  const members = await airtable.getActiveFamilyMembers();

  for (const member of members) {
    if (member.pinHash && bcrypt.compareSync(pin, member.pinHash)) {
      attempts.delete(clientKey);
      return member;
    }
  }

  recordFailure(clientKey);
  return null;
}

module.exports = { validatePin, isLockedOut };
