using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/access")]
    [Authorize]
    public sealed class ResourceAccessController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        private readonly ResourceAccessService _access;

        public ResourceAccessController(
            DentalRayDbContext context,
            ResourceAccessService access)
        {
            _context = context;
            _access = access;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetShareRecipients()
        {
            int currentUserID = _access.GetCurrentUserID(User);
            var users = await _context.Users
                .AsNoTracking()
                .Where(user => user.IsActive && user.UserID != currentUserID)
                .OrderBy(user => user.DisplayName)
                .Select(user => new
                {
                    user.UserID,
                    user.UserName,
                    user.DisplayName,
                    user.Role
                })
                .ToListAsync();

            return Ok(new { success = true, users });
        }

        [HttpGet("grants")]
        public async Task<IActionResult> GetGrants(
            [FromQuery] byte resourceType,
            [FromQuery] long resourceID)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (!IsValidResource(resourceType, resourceID))
                return BadRequest(new { success = false, message = "Invalid resource." });

            if (!await CanReadResource(resourceType, resourceID, currentUserID))
                return NotFound(new { success = false, message = "Resource not found." });

            var grants = await _context.ResourceAccessGrants
                .AsNoTracking()
                .Where(grant =>
                    grant.ResourceType == resourceType &&
                    grant.ResourceID == resourceID)
                .Join(
                    _context.Users.AsNoTracking(),
                    grant => grant.RecipientUserID,
                    recipient => recipient.UserID,
                    (grant, recipient) => new
                    {
                        grant.ResourceAccessGrantID,
                        grant.OwnerUserID,
                        grant.GrantedByUserID,
                        GrantedBy = _context.Users
                            .Where(user => user.UserID == grant.GrantedByUserID)
                            .Select(user => user.DisplayName)
                            .FirstOrDefault(),
                        RecipientUserID = recipient.UserID,
                        RecipientName = recipient.DisplayName,
                        grant.CreatedDate
                    })
                .OrderBy(grant => grant.CreatedDate)
                .ToListAsync();

            return Ok(new { success = true, grants });
        }

        [HttpPost("grants")]
        public async Task<IActionResult> CreateGrant(
            CreateResourceGrantRequest request)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (!IsValidResource(request.ResourceType, request.ResourceID) ||
                request.RecipientUserID <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid resource or recipient." });
            }

            if (!await CanReadResource(request.ResourceType, request.ResourceID, currentUserID))
                return NotFound(new { success = false, message = "Resource not found." });

            var recipient = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user =>
                    user.UserID == request.RecipientUserID &&
                    user.IsActive);

            if (recipient == null)
                return BadRequest(new { success = false, message = "Active recipient user not found." });

            if (recipient.UserID == currentUserID)
                return BadRequest(new { success = false, message = "You already have access to this resource." });

            int? ownerUserID = await GetOwnerUserID(request.ResourceType, request.ResourceID);
            if (!ownerUserID.HasValue)
                return Conflict(new { success = false, message = "Resource owner is not assigned." });

            if (recipient.UserID == ownerUserID.Value)
                return BadRequest(new { success = false, message = "The owner already has access to this resource." });

            bool duplicate = await _context.ResourceAccessGrants.AnyAsync(grant =>
                grant.ResourceType == request.ResourceType &&
                grant.ResourceID == request.ResourceID &&
                grant.GrantedByUserID == currentUserID &&
                grant.RecipientUserID == recipient.UserID);

            if (duplicate)
                return Conflict(new { success = false, message = "This permanent grant was already recorded." });

            var grant = new ResourceAccessGrant
            {
                ResourceType = request.ResourceType,
                ResourceID = request.ResourceID,
                OwnerUserID = ownerUserID.Value,
                GrantedByUserID = currentUserID,
                RecipientUserID = recipient.UserID,
                CreatedDate = DateTime.Now
            };

            _context.ResourceAccessGrants.Add(grant);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                permanent = true,
                grant
            });
        }

        [HttpPut("studies/{studyID:int}/visibility")]
        public async Task<IActionResult> SetStudyVisibility(
            int studyID,
            ResourceVisibilityRequest request)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (request.Visibility > ResourceAccessService.PublicVisibility)
                return BadRequest(new { success = false, message = "Invalid visibility." });

            var study = await _context.RadiologyStudies
                .FirstOrDefaultAsync(item => item.StudyID == studyID);

            if (study == null || study.OwnerUserID != currentUserID)
                return NotFound(new { success = false, message = "Study not found." });

            study.Visibility = request.Visibility;
            study.ModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, study.StudyID, study.Visibility });
        }

        [HttpPut("images/{imageID:long}/visibility")]
        public async Task<IActionResult> SetImageVisibility(
            long imageID,
            ResourceVisibilityRequest request)
        {
            int currentUserID = _access.GetCurrentUserID(User);
            if (request.Visibility > ResourceAccessService.PublicVisibility)
                return BadRequest(new { success = false, message = "Invalid visibility." });

            var image = await _context.RadiologyImages
                .FirstOrDefaultAsync(item => item.ImageID == imageID);

            if (image == null || image.OwnerUserID != currentUserID)
                return NotFound(new { success = false, message = "Image not found." });

            image.Visibility = request.Visibility;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, image.ImageID, image.Visibility });
        }

        private Task<bool> CanReadResource(
            byte resourceType,
            long resourceID,
            int userID)
        {
            return resourceType switch
            {
                ResourceAccessService.StudyResourceType when resourceID <= int.MaxValue =>
                    _access.CanReadStudyAsync((int)resourceID, userID),
                ResourceAccessService.ImageResourceType =>
                    _access.CanReadImageAsync(resourceID, userID),
                _ => Task.FromResult(false)
            };
        }

        private Task<int?> GetOwnerUserID(byte resourceType, long resourceID)
        {
            return resourceType switch
            {
                ResourceAccessService.StudyResourceType when resourceID <= int.MaxValue =>
                    _context.RadiologyStudies
                        .Where(study => study.StudyID == (int)resourceID)
                        .Select(study => study.OwnerUserID)
                        .FirstOrDefaultAsync(),
                ResourceAccessService.ImageResourceType =>
                    _context.RadiologyImages
                        .Where(image => image.ImageID == resourceID)
                        .Select(image => image.OwnerUserID)
                        .FirstOrDefaultAsync(),
                _ => Task.FromResult<int?>(null)
            };
        }

        private static bool IsValidResource(byte resourceType, long resourceID) =>
            resourceID > 0 &&
            (resourceType == ResourceAccessService.ImageResourceType ||
             resourceType == ResourceAccessService.StudyResourceType && resourceID <= int.MaxValue);
    }
}
