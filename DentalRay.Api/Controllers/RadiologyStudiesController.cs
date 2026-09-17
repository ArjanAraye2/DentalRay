using DentalRay.Api.Data;
using DentalRay.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RadiologyStudiesController : ControllerBase
    {
        private readonly DentalRayDbContext _context;
        public RadiologyStudiesController(DentalRayDbContext context) => _context = context;

        [HttpPost]
        public async Task<IActionResult> CreateStudy(RadiologyStudy study)
        {
            try
            {
                if (study.PatientID <= 0) return BadRequest(new { success=false, message="PatientID must be greater than zero." });
                if (!await _context.Patients.AnyAsync(p=>p.PatientID==study.PatientID)) return NotFound(new { success=false, message="Patient not found." });
                if (string.IsNullOrWhiteSpace(study.StudyType)) return BadRequest(new { success=false, message="StudyType is required." });
                var teeth = NormalizeTeeth(study.ToothNumbers);
                if (teeth == null) return BadRequest(new { success=false, message="One or more FDI tooth numbers are invalid." });
                study.StudyType=study.StudyType.Trim(); study.BodyPart=NormalizeOptionalText(study.BodyPart); study.Description=NormalizeOptionalText(study.Description); study.Report=NormalizeOptionalText(study.Report);
                if(study.StudyType.Length>50) return BadRequest(new {success=false,message="StudyType cannot be longer than 50 characters."});
                if(study.BodyPart?.Length>100) return BadRequest(new {success=false,message="BodyPart cannot be longer than 100 characters."});
                if(study.Description?.Length>1000) return BadRequest(new {success=false,message="Description cannot be longer than 1000 characters."});
                if(study.StudyDate==default) study.StudyDate=DateTime.Now;
                study.StudyID=0; study.CreatedDate=DateTime.Now; study.ModifiedDate=null;
                await using var transaction=await _context.Database.BeginTransactionAsync();
                _context.RadiologyStudies.Add(study); await _context.SaveChangesAsync();
                foreach(var tooth in teeth) _context.RadiologyStudyTeeth.Add(new RadiologyStudyTooth{StudyID=study.StudyID,ToothNumber=(byte)tooth,CreatedDate=DateTime.Now});
                await _context.SaveChangesAsync(); await transaction.CommitAsync();
                return Ok(new { success=true, study, toothNumbers=teeth });
            }
            catch(Exception ex){return StatusCode(500,new{success=false,message="Study creation failed.",error=ex.Message});}
        }

        [HttpGet("{studyID:int}")]
        public async Task<IActionResult> GetStudy(int studyID)
        {
            if(studyID<=0)return BadRequest(new{success=false,message="StudyID must be greater than zero."});
            var study=await _context.RadiologyStudies.AsNoTracking().FirstOrDefaultAsync(s=>s.StudyID==studyID);
            if(study==null)return NotFound(new{success=false,message="Study not found."});
            var teeth=await _context.RadiologyStudyTeeth.AsNoTracking().Where(x=>x.StudyID==studyID).OrderBy(x=>x.ToothNumber).Select(x=>(int)x.ToothNumber).ToListAsync();
            return Ok(new{success=true,study,toothNumbers=teeth});
        }

        [HttpGet("patient/{patientID:int}")]
        public async Task<IActionResult> GetPatientStudies(int patientID)
        {
            if(patientID<=0)return BadRequest(new{success=false,message="PatientID must be greater than zero."});
            if(!await _context.Patients.AsNoTracking().AnyAsync(p=>p.PatientID==patientID))return NotFound(new{success=false,message="Patient not found."});
            var studies=await _context.RadiologyStudies.AsNoTracking().Where(s=>s.PatientID==patientID).OrderByDescending(s=>s.StudyDate).ThenByDescending(s=>s.StudyID).ToListAsync();
            var ids=studies.Select(s=>s.StudyID).ToList();
            var toothRows=await _context.RadiologyStudyTeeth.AsNoTracking().Where(x=>ids.Contains(x.StudyID)).ToListAsync();
            var result=studies.Select(s=>new{s.StudyID,s.PatientID,s.StudyDate,s.StudyType,s.BodyPart,s.Description,s.Report,s.CreatedDate,s.ModifiedDate,ToothNumbers=toothRows.Where(t=>t.StudyID==s.StudyID).Select(t=>(int)t.ToothNumber).OrderBy(n=>n).ToArray()}).ToList();
            return Ok(new{success=true,patientID,count=result.Count,studies=result});
        }

        [HttpPut("{studyID:int}")]
        public async Task<IActionResult> UpdateStudy(int studyID,UpdateRadiologyStudyRequest request)
        {
            try
            {
                if(studyID<=0)return BadRequest(new{success=false,message="StudyID must be greater than zero."});
                if(request==null)return BadRequest(new{success=false,message="Study information is required."});
                if(string.IsNullOrWhiteSpace(request.StudyType))return BadRequest(new{success=false,message="StudyType is required."});
                var teeth=NormalizeTeeth(request.ToothNumbers); if(teeth==null)return BadRequest(new{success=false,message="One or more FDI tooth numbers are invalid."});
                var study=await _context.RadiologyStudies.FirstOrDefaultAsync(s=>s.StudyID==studyID); if(study==null)return NotFound(new{success=false,message="Study not found."});
                var type=request.StudyType.Trim(); var body=NormalizeOptionalText(request.BodyPart); var desc=NormalizeOptionalText(request.Description); var report=NormalizeOptionalText(request.Report);
                if(type.Length>50)return BadRequest(new{success=false,message="StudyType cannot be longer than 50 characters."}); if(body?.Length>100)return BadRequest(new{success=false,message="BodyPart cannot be longer than 100 characters."}); if(desc?.Length>1000)return BadRequest(new{success=false,message="Description cannot be longer than 1000 characters."}); if(request.StudyDate==default)return BadRequest(new{success=false,message="StudyDate is required."});
                await using var transaction=await _context.Database.BeginTransactionAsync();
                study.StudyDate=request.StudyDate;study.StudyType=type;study.BodyPart=body;study.Description=desc;study.Report=report;study.ModifiedDate=DateTime.Now;
                var old=await _context.RadiologyStudyTeeth.Where(x=>x.StudyID==studyID).ToListAsync(); _context.RadiologyStudyTeeth.RemoveRange(old);
                foreach(var tooth in teeth)_context.RadiologyStudyTeeth.Add(new RadiologyStudyTooth{StudyID=studyID,ToothNumber=(byte)tooth,CreatedDate=DateTime.Now});
                await _context.SaveChangesAsync();await transaction.CommitAsync();
                return Ok(new{success=true,study,toothNumbers=teeth,message="Study updated successfully."});
            }
            catch(Exception ex){return StatusCode(500,new{success=false,message="Study update failed.",error=ex.Message});}
        }

        private static List<int>? NormalizeTeeth(IEnumerable<int>? values){var result=(values??Array.Empty<int>()).Distinct().OrderBy(x=>x).ToList();return result.All(IsValidFdi)?result:null;}
        private static bool IsValidFdi(int n)=>(n>=11&&n<=18)||(n>=21&&n<=28)||(n>=31&&n<=38)||(n>=41&&n<=48)||(n>=51&&n<=55)||(n>=61&&n<=65)||(n>=71&&n<=75)||(n>=81&&n<=85);
        private static string? NormalizeOptionalText(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    }
}