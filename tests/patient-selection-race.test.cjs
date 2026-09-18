const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const root = path.resolve(__dirname, "..");
const app = fs.readFileSync(path.join(root, "DentalRay.Api/wwwroot/js/app.js"), "utf8");

assert.match(app, /patientOpenRequestVersion/);
assert.match(app, /patientOpenAbortController\?\.abort\(\)/);
assert.match(app, /signal:request\.signal/);
assert.match(app, /isCurrentPatientOpenRequest\(request\.version,id\)/);
assert.match(app, /err\?\.name==="AbortError"/);
assert.match(app, /showPatientLoadingState\(\)/);

console.log("Patient selection stale-response protection: passed");
