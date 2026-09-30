/* =========================================================
   ReSiRai - rename every remaining legacy object name
   (Dental / Dentist) to the neutral doctor / specialty names.

   tblDentalSpecialties  -> tblSpecialties
   tblUserDentists       -> tblUserDoctors
   *.DentistStaffID      -> *.DoctorStaffID
   constraint names updated to match.

   Safe to run more than once.
   ========================================================= */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.tblDentalSpecialties') IS NOT NULL AND OBJECT_ID('dbo.tblSpecialties') IS NULL
    EXEC sp_rename 'dbo.tblDentalSpecialties', 'tblSpecialties';
GO
IF OBJECT_ID('dbo.tblUserDentists') IS NOT NULL AND OBJECT_ID('dbo.tblUserDoctors') IS NULL
    EXEC sp_rename 'dbo.tblUserDentists', 'tblUserDoctors';
GO
IF COL_LENGTH('dbo.tblRadiologyStudies', 'DentistStaffID') IS NOT NULL
    EXEC sp_rename 'dbo.tblRadiologyStudies.DentistStaffID', 'DoctorStaffID', 'COLUMN';
IF COL_LENGTH('dbo.tblAppointments', 'DentistStaffID') IS NOT NULL
    EXEC sp_rename 'dbo.tblAppointments.DentistStaffID', 'DoctorStaffID', 'COLUMN';
IF COL_LENGTH('dbo.tblUserDoctors', 'DentistStaffID') IS NOT NULL
    EXEC sp_rename 'dbo.tblUserDoctors.DentistStaffID', 'DoctorStaffID', 'COLUMN';
GO

/* --- constraint names keep history readable --- */
IF OBJECT_ID('dbo.PK_tblSpecialties') IS NULL AND OBJECT_ID('dbo.PK_tblDentalSpecialties') IS NOT NULL
    EXEC sp_rename 'dbo.PK_tblDentalSpecialties', 'PK_tblSpecialties';
IF OBJECT_ID('dbo.UQ_tblSpecialties_SpecialtyName') IS NULL AND OBJECT_ID('dbo.UQ_tblDentalSpecialties_SpecialtyName') IS NOT NULL
    EXEC sp_rename 'dbo.UQ_tblDentalSpecialties_SpecialtyName', 'UQ_tblSpecialties_SpecialtyName';
IF OBJECT_ID('dbo.DF_tblSpecialties_IsActive') IS NULL AND OBJECT_ID('dbo.DF_tblDentalSpecialties_IsActive') IS NOT NULL
    EXEC sp_rename 'dbo.DF_tblDentalSpecialties_IsActive', 'DF_tblSpecialties_IsActive';
IF OBJECT_ID('dbo.PK_tblUserDoctors') IS NULL AND OBJECT_ID('dbo.PK_tblUserDentists') IS NOT NULL
    EXEC sp_rename 'dbo.PK_tblUserDentists', 'PK_tblUserDoctors';
IF OBJECT_ID('dbo.FK_tblUserDoctors_Clinic') IS NULL AND OBJECT_ID('dbo.FK_tblUserDentists_Clinic') IS NOT NULL
    EXEC sp_rename 'dbo.FK_tblUserDentists_Clinic', 'FK_tblUserDoctors_Clinic';
IF OBJECT_ID('dbo.FK_tblUserDoctors_Doctor') IS NULL AND OBJECT_ID('dbo.FK_tblUserDentists_Dentist') IS NOT NULL
    EXEC sp_rename 'dbo.FK_tblUserDentists_Dentist', 'FK_tblUserDoctors_Doctor';
IF OBJECT_ID('dbo.FK_tblUserDoctors_User') IS NULL AND OBJECT_ID('dbo.FK_tblUserDentists_User') IS NOT NULL
    EXEC sp_rename 'dbo.FK_tblUserDentists_User', 'FK_tblUserDoctors_User';
IF OBJECT_ID('dbo.FK_tblRadiologyStudies_Doctor') IS NULL AND OBJECT_ID('dbo.FK_tblRadiologyStudies_Dentist') IS NOT NULL
    EXEC sp_rename 'dbo.FK_tblRadiologyStudies_Dentist', 'FK_tblRadiologyStudies_Doctor';
IF OBJECT_ID('dbo.FK_tblStaff_Specialty') IS NULL AND OBJECT_ID('dbo.FK_tblStaff_DentalSpecialty') IS NOT NULL
    EXEC sp_rename 'dbo.FK_tblStaff_DentalSpecialty', 'FK_tblStaff_Specialty';
GO
