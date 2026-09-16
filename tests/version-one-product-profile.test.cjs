const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const repositoryRoot = path.resolve(__dirname, "..");
const webRoot = path.join(repositoryRoot, "DentalRay.Api", "wwwroot");
const profilePath = path.join(webRoot, "js", "product-profile.js");
const applicationPath = path.join(webRoot, "js", "app.js");
const stylesheetPath = path.join(webRoot, "css", "site.css");
const indexPath = path.join(webRoot, "index.html");
const patientsControllerPath = path.join(
    repositoryRoot,
    "DentalRay.Api",
    "Controllers",
    "PatientsController.cs"
);

// Evaluate the product profile in a minimal browser-like context. This keeps
// the test independent from the rest of the DOM-heavy application script.
const profileSource = fs.readFileSync(profilePath, "utf8");
const context = vm.createContext({ window: {} });
vm.runInContext(profileSource, context, { filename: profilePath });

const profile = context.window.DentalRayProductProfile;

assert.ok(profile, "DentalRayProductProfile must be defined.");
assert.equal(profile.release, "1.0", "The clinic installer must use the Version 1 profile.");
assert.equal(profile.features.financials, false, "Financial UI must stay disabled in Version 1.");
assert.equal(profile.features.resourceSharing, false, "Sharing UI must stay disabled in Version 1.");
assert.equal(profile.features.imageAttachments, false, "Attachment UI must stay disabled in Version 1.");
assert.equal(Object.isFrozen(profile), true, "The product profile must be immutable at runtime.");
assert.equal(Object.isFrozen(profile.features), true, "Feature flags must be immutable at runtime.");

// Guard against accidentally deleting the application-side feature checks.
const applicationSource = fs.readFileSync(applicationPath, "utf8");
const stylesheetSource = fs.readFileSync(stylesheetPath, "utf8");
const indexSource = fs.readFileSync(indexPath, "utf8");
const patientsControllerSource = fs.readFileSync(patientsControllerPath, "utf8");
for (const featureName of ["financials", "resourceSharing", "imageAttachments"]) {
    assert.match(
        applicationSource,
        new RegExp(`isProductFeatureEnabled\\(\\s*["']${featureName}["']\\s*\\)`),
        `app.js must enforce the ${featureName} feature flag.`
    );
}

assert.match(
    applicationSource,
    /\[\s*["']newStudyVisibility["']\s*,\s*["']imageVisibility["']\s*\]/,
    "Version 1 must hide the Study and image visibility selectors."
);

assert.match(
    applicationSource,
    /function\s+bindInputValidationFeedback\s*\(/,
    "Input forms must expose accessible native validation feedback."
);
for (const formName of ["newPatientForm", "editPatientForm", "newStudyForm", "editStudyForm", "uploadImageForm"]) {
    assert.match(
        applicationSource,
        new RegExp(`\\b${formName}\\b`),
        `${formName} must be covered by input validation feedback.`
    );
}
assert.match(
    stylesheetSource,
    /input\[aria-invalid="true"\]/,
    "Invalid input fields must have a visible error style."
);

// NationalCode accepts Persian and Arabic keyboard digits in the browser,
// while the API normalizes them before validation and persistence.
for (const fieldId of ["newNationalCode", "editNationalCode"]) {
    assert.match(
        indexSource,
        new RegExp(`id=["']${fieldId}["'][\\s\\S]*?pattern=["']\\[0-9۰-۹\\]\\+["']`),
        `${fieldId} must validate numeric Persian/Latin digits in the browser.`
    );
}
assert.match(
    patientsControllerSource,
    /static\s+string\s+NormalizeNationalCode\s*\(/,
    "PatientsController must normalize NationalCode digits."
);
assert.match(
    patientsControllerSource,
    /patient\.NationalCode\s*=\s*NormalizeNationalCode\(/,
    "Patient creation must persist a normalized NationalCode."
);
assert.match(
    patientsControllerSource,
    /string\s+newNationalCode\s*=\s*\n?\s*NormalizeNationalCode\(/,
    "Patient updates must compare a normalized NationalCode."
);
assert.match(
    patientsControllerSource,
    /string\s+normalizedNationalCode\s*=\s*\n?\s*NormalizeNationalCode\(/,
    "Patient lookup must normalize NationalCode input."
);
assert.match(
    patientsControllerSource,
    /string\s+searchText\s*=\s*\n?\s*NormalizeNationalCode\(search\)/,
    "Patient search must normalize numeric keyboard input."
);

// The profile has to load before app.js reads its feature flags.
const profileScriptPosition = indexSource.indexOf("js/product-profile.js");
const applicationScriptPosition = indexSource.indexOf("js/app.js");

assert.notEqual(profileScriptPosition, -1, "index.html must load product-profile.js.");
assert.notEqual(applicationScriptPosition, -1, "index.html must load app.js.");
assert.ok(
    profileScriptPosition < applicationScriptPosition,
    "product-profile.js must load before app.js."
);

console.log("Version 1 product profile checks passed.");
