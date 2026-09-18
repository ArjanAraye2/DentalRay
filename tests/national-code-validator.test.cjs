const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const source = fs.readFileSync(
    path.resolve(__dirname, "../DentalRay.Api/Services/IranianNationalCodeValidator.cs"),
    "utf8"
);

assert.doesNotMatch(
    source,
    /nationalCode\.All\s*\(/,
    "Repeated digits must not be rejected before check-digit validation."
);
assert.match(source, /nationalCode\.Length\s*!=\s*10/, "National Code must remain exactly 10 digits.");
assert.match(source, /sum\s*%\s*11/, "National Code check-digit validation must remain enabled.");

console.log("National Code repeated-digit policy contract: passed");
