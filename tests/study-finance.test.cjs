const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const root = path.resolve(__dirname, "..");
const read = file => fs.readFileSync(path.join(root, file), "utf8");

const db = read("ReSiRai.Api/Data/ReSiRaiDbContext.cs");
const api = read("ReSiRai.Api/Controllers/StudyFinanceController.cs");
const sql = read("ReSiRai.SetupHelper/Database/ReSiRai.Database.Install.sql");
const ui = read("ReSiRai.Api/wwwroot/js/study-finance.js");
const html = read("ReSiRai.Api/wwwroot/index.html");
const deletion = read("ReSiRai.Api/Controllers/RadiologyStudiesController.DeleteStudy.cs");

for (const table of ["tblStudyActions", "tblStudyPayments"]) assert.match(sql, new RegExp(table));
for (const set of ["StudyActions", "StudyPayments"]) assert.match(db, new RegExp(`DbSet<Study(?:Action|Payment)>\\s+${set}`));
assert.match(api, /DiscountAmount>x\.Amount/, "Discount must not exceed action cost.");
assert.match(api, /balanceAmount=gross-discount-received/, "Balance formula is missing.");
assert.match(api, /PaymentMethod is null or <1 or >3/, "Payment method validation is missing.");
assert.match(sql, /PaymentMethod TINYINT NULL/);
assert.match(ui, /اقدامات Study/);
assert.match(ui, /دریافت‌های Study/);
for (const method of ["پوز", "کارت به کارت", "نقدی"]) assert.match(ui, new RegExp(method));
assert.match(
  ui,
  /form\.append\(date,method,amount,desc,add\);window\.ReSiRaiJalali\?\.enhanceAll\(date\)/,
  "The payment date input must be attached before the Jalali picker wraps it."
);
assert.match(html, /study-finance\.js\?v=/);
assert.match(deletion, /StudyActions\.RemoveRange/);
assert.match(deletion, /StudyPayments\.RemoveRange/);

console.log("Study finance contract: passed");
