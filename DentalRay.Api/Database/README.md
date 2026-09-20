# Dentix database migrations

Each `.sql` file in this folder is a migration. They are written to be safe to run
more than once.

## Running a migration

Use `sqlcmd` with an explicit UTF-8 code page:

```
sqlcmd -S <server> -U <user> -P <password> -d Dentix -f 65001 -i <file>.sql
```

### Why `-f 65001` matters

The files are UTF-8 without a BOM. Without `-f 65001`, `sqlcmd` reads them as ANSI
and every Persian string arrives as mojibake. For example a migration that inserts
`آماده شدن عکس` stored `طھظ…ط§ط³...` instead, and a lookup that matched a
specialty by name silently inserted nothing.

`ALTER TABLE` statements are unaffected, so the problem only shows up in migrations
that insert or compare Persian text.

## Migrations in this folder

| File | Purpose |
|---|---|
| `20260920_AddStudyPaymentMethod.sql` | `tblStudyPayments.PaymentMethod` |
| `20260921_AddStudyStatus.sql` | Study `Status`, `FollowUpDate`, `FollowUpNote` |
| `20260921_AddWaitStages.sql` | `tblWaitStages` lookup + `Study.WaitStageID` |
| `20260921_AddPosSettings.sql` | `tblPosSettings` |
| `20260921_AddRefundAndPosDispatch.sql` | refunds + POS dispatch tracking |
| `20260921_AddMessagingAndAppointments.sql` | `tblPatientMessages`, `tblAppointments` |
| `20260921_RenameDatabaseToDentix.sql` | rename the database in place |

The Windows installer runs the full schema script through `DentalRay.SetupHelper`,
which reads the file with .NET (`File.ReadAllText`), so it always uses UTF-8 and is
not affected.
