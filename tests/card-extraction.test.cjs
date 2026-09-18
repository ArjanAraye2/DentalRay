const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const root = path.resolve(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");

const api = read("DentalRay.Api/Controllers/AiCardExtractionController.cs");
const app = read("DentalRay.Api/wwwroot/js/app.js");
const ui = read("DentalRay.Api/wwwroot/js/card-extraction.js");
const program = read("DentalRay.Api/Program.cs");
const radiologyApi = read("DentalRay.Api/Controllers/AiRadiologyImageAnalysisController.cs");
const imageTypeSql = read("DentalRay.Api/Database/20260919_AddArchiveCardImageType.sql");

assert.match(api, /Route\("api\/ai\/images"\)/);
assert.match(api, /Patient identity and contact fields are already known and must be ignored/);
assert.doesNotMatch(api, /"patient":\{/);
assert.match(api, /store = false/);
assert.match(app, /استخراج اطلاعات از کارت/);
assert.match(app, /تحلیل رادیولوژی/);
assert.match(ui, /اطلاعات پیشنهادی مطالعه/);
assert.doesNotMatch(ui, /اطلاعات پیشنهادی بیمار/);
assert.match(ui, /analyze-radiology/);
assert.match(radiologyApi, /problemTeeth/);
assert.match(imageTypeSql, /کارت بایگانی/);
assert.match(program, /card-extraction\.js/);

console.log("Study card extraction contract: passed");
