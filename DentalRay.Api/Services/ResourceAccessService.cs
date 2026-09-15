using System.Security.Claims;
using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Services
{
    public sealed class ResourceAccessService
    {
        public const byte StudyResourceType = 1;
        public const byte ImageResourceType = 2;
        public const byte PrivateVisibility = 0;
        public const byte PublicVisibility = 1;

        private readonly DentalRayDbContext _context;

        public ResourceAccessService(DentalRayDbContext context)
        {
            _context = context;
        }

        public int GetCurrentUserID(ClaimsPrincipal user)
        {
            return int.TryParse(
                user.FindFirstValue(ClaimTypes.NameIdentifier),
                out int userID)
                    ? userID
                    : 0;
        }

        public IQueryable<RadiologyStudy> ReadableStudies(int userID)
        {
            return _context.RadiologyStudies.Where(study =>
                study.OwnerUserID == userID ||
                study.Visibility == PublicVisibility ||
                _context.ResourceAccessGrants.Any(grant =>
                    grant.ResourceType == StudyResourceType &&
                    grant.ResourceID == study.StudyID &&
                    grant.RecipientUserID == userID));
        }

        public IQueryable<RadiologyImage> ReadableImages(int userID)
        {
            return _context.RadiologyImages.Where(image =>
                image.OwnerUserID == userID ||
                image.Visibility == PublicVisibility ||
                _context.ResourceAccessGrants.Any(grant =>
                    grant.ResourceType == ImageResourceType &&
                    grant.ResourceID == image.ImageID &&
                    grant.RecipientUserID == userID));
        }

        public IQueryable<Patient> ReadablePatients(int userID)
        {
            var patientIDs = ReadableStudies(userID)
                .Select(study => study.PatientID);

            return _context.Patients.Where(patient =>
                patient.OwnerUserID == userID ||
                patientIDs.Contains(patient.PatientID));
        }

        public Task<bool> CanReadStudyAsync(int studyID, int userID) =>
            ReadableStudies(userID).AnyAsync(study => study.StudyID == studyID);

        public Task<bool> CanReadImageAsync(long imageID, int userID) =>
            ReadableImages(userID).AnyAsync(image => image.ImageID == imageID);

        public Task<bool> CanReadPatientAsync(int patientID, int userID) =>
            ReadablePatients(userID).AnyAsync(patient => patient.PatientID == patientID);

        public Task<bool> CanManageStudyAsync(int studyID, int userID) =>
            _context.RadiologyStudies.AnyAsync(study =>
                study.StudyID == studyID &&
                study.OwnerUserID == userID);

        public Task<bool> CanManageImageAsync(long imageID, int userID) =>
            _context.RadiologyImages.AnyAsync(image =>
                image.ImageID == imageID &&
                image.OwnerUserID == userID);

        public Task<bool> CanManagePatientAsync(int patientID, int userID) =>
            _context.Patients.AnyAsync(patient =>
                patient.PatientID == patientID &&
                patient.OwnerUserID == userID);
    }
}
