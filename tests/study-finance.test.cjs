const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const root = path.resolve(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");

const db = read("DentalRay.Api/Data/DentalRayDbContext.cs");
const api = read("DentalRay.Api/Controllers/StudyFinanceController.cs");
const sql = read("DentalRay.SetupHelper/Database/DentalRay.Database.Install.sql");
const ui = read("DentalRay.Api/wwwroot/js/study-finance.js");
const html = read("DentalRay.Api/wwwroot/index.html");
const deletion = read("DentalRay.Api/Controllers/RadiologyStudiesController.DeleteStudy.cs");

for (const table of ["tblStudyActions", "tblStudyPayments"]) assert.match(sql, new RegExp(table));
for (const set of ["StudyActions", "StudyPayments"]) assert.match(db, new RegExp(`DbSet<Study(?:Action|Payment)>\\s+${set}`));
assert.match(api, /DiscountAmount>x\.Amount/, "Discount must not exceed action cost.");
assert.match(api, /balanceAmount=gross-discount-received/, "Balance formula is missing.");
assert.match(ui, /اقدامات Study/);
assert.match(ui, /دریافت‌های Study/);
assert.match(
  ui,
  /form\.append\(date,amount,desc,add\);window\.DentalRayJalali\?\.enhanceAll\(date\)/,
  "The payment date input must be attached before the Jalali picker wraps it."
);
assert.match(html, /study-finance\.js\?v=/);
assert.match(deletion, /StudyActions\.RemoveRange/);
assert.match(deletion, /StudyPayments\.RemoveRange/);

console.log("Study finance contract: passed");
