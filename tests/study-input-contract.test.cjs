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

// Create and edit must both require a complete Jalali date and time. The HTML
// pattern accepts Latin, Persian and Arabic keyboard digits.
for (const fieldID of ["newStudyDate", "editStudyDate"]) {
    const fieldPattern = new RegExp(
        `<input\\s+id=["']${fieldID}["'][\\s\\S]*?pattern=["'][^"']+:[^"']+["'][\\s\\S]*?required`
    );
    assert.match(indexSource, fieldPattern, `${fieldID} must require date and time.`);
}

assert.match(
    applicationSource,
    /\? \/\^\(\\d\{4\}\)\\\/\(\\d\{1,2\}\)\\\/\(\\d\{1,2\}\)\\s\+\(\\d\{1,2\}\):\(\\d\{2\}\)\$\//,
    "The JavaScript parser must require a time when includeTime is true."
);

assert.equal(
    controllerSource.includes("study.StudyDate =\n                        DateTime.Now"),
    false,
    "The API must not silently replace a missing Study date with the current time."
);
assert.ok(
    controllerSource.split('"StudyDate is required."').length >= 3,
    "Both Create and Update Study endpoints must reject a missing date."
);

for (const message of [
    "Organization is required.",
    "The organization has no active dentist.",
    "StudyType cannot be longer than 50 characters.",
    "BodyPart cannot be longer than 100 characters.",
    "Description cannot be longer than 1000 characters."
]) {
    assert.match(
        applicationSource,
        new RegExp(message.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")),
        `The frontend must translate API validation message: ${message}`
    );
}

console.log("Study input contract checks passed.");
