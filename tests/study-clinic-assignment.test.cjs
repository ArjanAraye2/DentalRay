const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

const repositoryRoot = path.resolve(__dirname, "..");
const indexSource = fs.readFileSync(
    path.join(repositoryRoot, "DentalRay.Api", "wwwroot", "index.html"),
    "utf8"
);
const applicationSource = fs.readFileSync(
    path.join(repositoryRoot, "DentalRay.Api", "wwwroot", "js", "app.js"),
    "utf8"
);
const controllerSource = fs.readFileSync(
    path.join(repositoryRoot, "DentalRay.Api", "Controllers", "RadiologyStudiesController.cs"),
    "utf8"
);

// Both data-entry paths must require a clinic and dentist before submission.
for (const fieldID of [
    "newStudyOrganization",
    "newStudyDentist",
    "editStudyOrganization",
    "editStudyDentist"
]) {
    assert.match(
        indexSource,
        new RegExp(`<select\\s+id=["']${fieldID}["'][^>]*\\brequired\\b`),
        `${fieldID} must be required in the Study form.`
    );
}

for (const message of [
    "مطب یا مرکز را انتخاب کنید.",
    "دندانپزشک را انتخاب کنید."
]) {
    assert.ok(
        applicationSource.split(message).length >= 3,
        `Create and edit flows must both validate: ${message}`
    );
}

// The API owns the same invariant and shares one implementation between POST
// and PUT. This protects callers other than the browser from incomplete data.
assert.ok(
    controllerSource.split("ValidateStudyAssignmentAsync(").length >= 4,
    "Create, update and the helper definition must share assignment validation."
);
for (const backendMessage of [
    "Organization is required.",
    "The organization has no active dentist.",
    "Dentist selection is required for organizations with multiple dentists.",
    "Selected dentist does not belong to this organization."
]) {
    assert.match(controllerSource, new RegExp(backendMessage.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")));
}

console.log("Study clinic/dentist assignment checks passed.");
