// Run with: dotnet run
// Enter a PIN for a family member, get back a hash to paste into the
// PinHash field of that person's row in the FamilyMembers Airtable table.
// The plain PIN is never written anywhere - only the hash leaves this tool.

Console.Write("Enter the PIN to hash (6+ digits recommended): ");
var pin = Console.ReadLine() ?? "";

if (string.IsNullOrWhiteSpace(pin))
{
    Console.WriteLine("No PIN entered - nothing to do.");
    return;
}

if (pin.Length < 6)
{
    Console.WriteLine("Warning: PINs under 6 digits are easier to guess. Consider using a longer one.");
}

var hash = BCrypt.Net.BCrypt.HashPassword(pin);

Console.WriteLine();
Console.WriteLine("Paste this into the PinHash field for this person:");
Console.WriteLine(hash);
