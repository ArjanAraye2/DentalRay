using System.Security.Claims;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Services
{
    // Central authorization rules for Radiology Studies.
    // Keeping these rules in one service prevents individual controllers from
    // accidentally implementing different Dentist/Employee access behavior.
    public class StudyAccessService
    {
        private readonly DentalRayDbContext _db;
        public StudyAccessService(DentalRayDbContext db) => _db = db;

        public IQueryable<RadiologyStudy> ApplyAccess(IQueryable<RadiologyStudy> studies, ClaimsPrincipal principal)
        {
            if (IsSuperAdmin(principal)) return studies;

            int userID = ReadInt(principal, "UserID");
            int staffID = ReadInt(principal, "StaffID");
            int staffType = ReadInt(principal, "StaffType");

            // Dentist: only Studies explicitly owned by that Dentist.
            if (staffType == 2)
                return studies.Where(s => s.DentistStaffID == staffID);

            // Employee: only Studies whose Clinic + Dentist pair is assigned to
            // this User in tblUserDentists. Legacy Studies with NULL ownership are
            // intentionally not visible to ordinary users.
            if (staffType == 1)
                return studies.Where(s =>
                    s.ClinicID.HasValue && s.DentistStaffID.HasValue &&
                    _db.UserDentists.Any(a =>
                        a.UserID == userID &&
                        a.ClinicID == s.ClinicID.Value &&
                        a.DentistStaffID == s.DentistStaffID.Value));

            // Unknown/invalid staff type receives no Study access.
            return studies.Where(_ => false);
        }

        public async Task<bool> CanAccessStudyAsync(int studyID, ClaimsPrincipal principal)
        {
            return await ApplyAccess(_db.RadiologyStudies.AsNoTracking(), principal)
                .AnyAsync(s => s.StudyID == studyID);
        }

        public static bool IsSuperAdmin(ClaimsPrincipal principal) =>
            string.Equals(principal.FindFirstValue("IsSuperAdmin"), "true", StringComparison.OrdinalIgnoreCase);

        private static int ReadInt(ClaimsPrincipal principal, string claimName) =>
            int.TryParse(principal.FindFirstValue(claimName), out int value) ? value : 0;
    }
}
