const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const root = path.resolve(__dirname, "..");
const html = fs.readFileSync(path.join(root, "DentalRay.Api/wwwroot/index.html"), "utf8");
const app = fs.readFileSync(path.join(root, "DentalRay.Api/wwwroot/js/app.js"), "utf8");
const navigation = fs.readFileSync(path.join(root, "DentalRay.Api/wwwroot/js/navigation.js"), "utf8");
const dashboardController = fs.readFileSync(path.join(root, "DentalRay.Api/Controllers/DashboardController.cs"), "utf8");
const studiesController = fs.readFileSync(path.join(root, "DentalRay.Api/Controllers/RadiologyStudiesController.cs"), "utf8");

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
assert.match(html, /id=["']studyDetailsSaveButton["'] type=["']button["']/, "Study save must use an explicit non-submitting button.");
assert.match(html, /onclick=["'][^"']*DentalRaySaveStudyDetails/, "Study Save must invoke its handler directly.");
assert.match(app, /studyDetailsSaveInProgress/, "Study save must guard against duplicate submissions.");
assert.match(app, /window\.DentalRaySaveStudyDetails=event=>/, "Study Save must expose a direct, cache-diagnostic handler.");
assert.match(html, /onclick=["'][^"']*DentalRayOpenNewStudy/, "New Study must invoke its handler directly.");
assert.match(app, /window\.DentalRayOpenNewStudy=event=>/, "New Study must expose a direct, cache-diagnostic handler.");
assert.match(app, /\(غیرفعال\)/, "The current inactive Study type must remain selectable while editing.");
assert.match(app, /Study saved, but patient workspace refresh failed/, "A refresh failure must not be reported as a failed Study save.");
assert.match(app, /function\s+renderRecentStudiesSummary\s*\(/, "Design D recent Study summary is missing.");
assert.match(html, /class=["']patient-identity-hero["']/, "Patient identity must use the compact unified hero layout.");
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
assert.match(navigation, /dashboardLocalIp/, "Dashboard local network information is missing.");
assert.match(navigation, /dashboardPublicIp/, "Dashboard static/public IP information is missing.");
assert.match(navigation, /dashboardLanLinks/, "Dashboard LAN access links are missing.");
assert.match(navigation, /networkAccessSettings/, "Dashboard network settings must open a dedicated Settings section.");
assert.match(navigation, /data-settings-focus=\"network\"/, "Dashboard must provide a direct Network Settings action.");
assert.match(navigation, /network\.serverNameUrl/, "The server-name URL must be rendered with the network links.");
assert.match(navigation, /navigator\.clipboard\.writeText/, "Dashboard network links must be copyable.");
assert.match(navigation, /navigate\(["']dashboard["']\);\s*\}\)\(\);/, "Dashboard must be the default landing page.");
assert.match(html, /sidebar-link active["'] data-nav=["']dashboard["']/, "Dashboard must be active in the initial navigation markup.");
assert.match(dashboardController, /patientsToday/, "Dashboard API must calculate today's distinct patients.");
assert.match(dashboardController, /recentImages/, "Dashboard API must return recent images.");
assert.match(dashboardController, /Dns\.GetHostAddresses/, "Dashboard API must discover local IPv4 addresses.");
assert.match(dashboardController, /RemoteAccess:PublicHost/, "Dashboard API must read the configured public or static host.");
assert.match(dashboardController, /localUrls/, "Dashboard API must return LAN access URLs.");
assert.match(dashboardController, /serverNameUrl/, "Dashboard API must return an access URL based on the server name.");
assert.match(studiesController, /t\.IsActive\|\|t\.StudyTypeID==study\.StudyTypeID/, "An existing Study must retain its current inactive type during unrelated edits.");
