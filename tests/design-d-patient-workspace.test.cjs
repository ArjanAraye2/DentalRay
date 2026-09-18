const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const html = fs.readFileSync(path.join(root, "DentalRay.Api/wwwroot/index.html"), "utf8");
const app = fs.readFileSync(path.join(root, "DentalRay.Api/wwwroot/js/app.js"), "utf8");
const navigation = fs.readFileSync(path.join(root, "DentalRay.Api/wwwroot/js/navigation.js"), "utf8");
const dashboardController = fs.readFileSync(path.join(root, "DentalRay.Api/Controllers/DashboardController.cs"), "utf8");

for (const id of [
    "newStudyButton",
    "recentStudiesSummary",
    "lastStudyDateSummary",
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


assert.match(html, /<th>بیمار<\/th>/, "Design D patient identity column is missing.");
assert.match(html, /<th>مطالعات<\/th>/, "Design D study summary column is missing.");
assert.match(app, /function\s+createPatientIdentityCell\s*\(/, "Patient row photo renderer is missing.");
assert.ok(app.includes('/api/patients/${patient.patientID}/photo'), "Patient row photo endpoint is not wired.");
assert.doesNotMatch(app, /b\.textContent="باز کردن پرونده"/, "The obsolete open-record button must not be rendered.");
assert.match(app, /function\s+deletePatient\s*\(/, "Patient delete action is missing.");
assert.match(app, /method:["']DELETE["']/, "Patient delete must call the DELETE API.");
assert.match(app, /function\s+hydrateStudyCard\s*\(/, "Scrollable Study cards must load their own details.");
assert.doesNotMatch(app, /function\s+selectStudyTab\s*\(/, "Studies must not use tab selection.");
assert.match(app, /sort\(\(a,b\)=>new Date\(b\.studyDate\|\|0\)-new Date\(a\.studyDate\|\|0\)\)/, "Studies must be sorted newest-first.");
assert.match(app, /function\s+renderRecentStudiesSummary\s*\(/, "Design D recent Study summary is missing.");
assert.match(app, /radiologyimages\/study\/\$\{study\.studyID\}/, "Selected Study images must load inline.");

const controller = fs.readFileSync(path.join(root, "DentalRay.Api/Controllers/PatientsController.cs"), "utf8");
assert.match(controller, /\[HttpDelete\("\{patientID:int\}"\)\]/, "Patient DELETE endpoint is missing.");
assert.match(controller, /RadiologyStudies[\s\S]*AnyAsync\(s => s\.PatientID == patientID\)/, "Backend must block deleting patients with Studies.");
assert.match(html, /\/js\/navigation\.js/, "Shell navigation script is not loaded.");
assert.match(html, /\/js\/app\.js\?v=[^"']+/, "Main application script must be cache-busted after UI contract changes.");
assert.match(html, /\/css\/site\.css\?v=[^"']+/, "Main stylesheet must be cache-busted after layout changes.");
assert.match(navigation, /settingsAdminActions/, "Administrative actions must be moved to Settings.");
assert.match(navigation, /data-nav/, "Right sidebar navigation is not wired.");
assert.doesNotMatch(html, /data-nav=["']studies["']/, "Studies must remain inside the Patient workspace, not the main menu.");
assert.doesNotMatch(html, /data-nav=["']images["']/, "Images must remain inside the Patient workspace, not the main menu.");
assert.doesNotMatch(navigation, /dashboard-shortcuts/, "Dashboard must not contain operational shortcuts.");
assert.match(navigation, /dashboardPatientsToday/, "Today's patient metric is missing.");
assert.match(navigation, /dashboardRecentStudies/, "Recent Studies panel is missing.");
assert.match(dashboardController, /patientsToday/, "Dashboard API must calculate today's distinct patients.");
assert.match(dashboardController, /recentImages/, "Dashboard API must return recent images.");
