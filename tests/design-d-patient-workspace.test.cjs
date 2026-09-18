const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const html = fs.readFileSync(path.join(root, "DentalRay.Api/wwwroot/index.html"), "utf8");
const app = fs.readFileSync(path.join(root, "DentalRay.Api/wwwroot/js/app.js"), "utf8");

for (const id of [
    "newStudyButton",
    "editPatientButton",
    "printPatientButton",
    "deactivatePatientButton",
    "detailPatientCode",
    "detailNationalCode",
    "detailMobile",
    "detailBirthDate",
    "detailGender",
    "detailAddress"
]) {
    assert.match(html, new RegExp(`id=["']${id}["']`), `Missing Design D patient control: ${id}`);
    assert.match(app, new RegExp(`\\b${id}\\b`), `Control is not wired in app.js: ${id}`);
}

assert.ok(
    app.includes('fetch(`/api/patients/${selectedPatientID}/${action}`,{method:"PUT"})'),
    "Active/inactive action must match the API PUT contract."
);
assert.match(app, /function\s+printPatientInformation\s*\(/, "Patient print action is missing.");
assert.match(app, /P-\$\{String\(id\)\.padStart\(3,["']0["']\)\}/, "Display patient code must use the Design D P-001 format.");

console.log("Design D patient workspace contract: passed");
