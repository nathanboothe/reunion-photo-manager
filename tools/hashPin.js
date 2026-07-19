// Run with: node tools/hashPin.js
// Enter a PIN for a family member, get back a hash to paste into the
// PinHash field of that person's row in the FamilyMembers Airtable table.
// The plain PIN is never written anywhere - only the hash leaves this tool.
const readline = require('readline');
const bcrypt = require('bcryptjs');

const rl = readline.createInterface({ input: process.stdin, output: process.stdout });

rl.question('Enter the PIN to hash (6+ digits recommended): ', (pin) => {
  if (!pin || !pin.trim()) {
    console.log('No PIN entered - nothing to do.');
    rl.close();
    return;
  }

  if (pin.trim().length < 6) {
    console.log('Warning: PINs under 6 digits are easier to guess. Consider using a longer one.');
  }

  const hash = bcrypt.hashSync(pin.trim(), 10);

  console.log('');
  console.log('Paste this into the PinHash field for this person:');
  console.log(hash);

  rl.close();
});
