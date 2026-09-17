using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[SuperAdminOnly]
public class UsersAdminController : ControllerBase
{
    private readonly DentalRayDbContext _context;
    private readonly IPasswordHasher<User> _hasher;
    public UsersAdminController(DentalRayDbContext context, IPasswordHasher<User> hasher)
    { _context=context; _hasher=hasher; }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users=await _context.Users.AsNoTracking().Join(_context.Staff.AsNoTracking(),
            u=>u.StaffID,s=>s.StaffID,(u,s)=>new {
                u.UserID,u.StaffID,UserName=s.NationalCode,s.NationalCode,s.FirstName,s.LastName,s.StaffType,u.IsActive,u.EndDate,
                HasPassword=!string.IsNullOrWhiteSpace(u.PasswordHash)
            }).OrderBy(x=>x.LastName).ThenBy(x=>x.FirstName).ToListAsync();
        return Ok(new { success=true, users });
    }

    [HttpGet("available-staff")]
    public async Task<IActionResult> AvailableStaff()
    {
        var used=await _context.Users.Select(x=>x.StaffID).ToListAsync();
        var staff=await _context.Staff.AsNoTracking().Where(x=>!used.Contains(x.StaffID))
            .OrderBy(x=>x.LastName).ThenBy(x=>x.FirstName)
            .Select(x=>new { x.StaffID,x.NationalCode,x.FirstName,x.LastName,x.StaffType }).ToListAsync();
        return Ok(new { success=true, staff });
    }

    // Returns active clinics and their dentist members for the access editor.
    [HttpGet("access-options")]
    public async Task<IActionResult> AccessOptions()
    {
        var options = await (from cs in _context.ClinicStaff.AsNoTracking()
                             join c in _context.Clinics.AsNoTracking() on cs.ClinicID equals c.ClinicID
                             join s in _context.Staff.AsNoTracking() on cs.StaffID equals s.StaffID
                             where c.IsActive && s.StaffType == 2
                             orderby c.ClinicName, s.LastName, s.FirstName
                             select new { c.ClinicID, c.ClinicName, DentistStaffID=s.StaffID, s.FirstName, s.LastName })
                            .ToListAsync();
        return Ok(new { success=true, options });
    }

    [HttpGet("{id:int}/access")]
    public async Task<IActionResult> GetAccess(int id)
    {
        if (!await _context.Users.AnyAsync(x=>x.UserID==id)) return NotFound(new {success=false,message="کاربر یافت نشد."});
        var access=await _context.UserDentists.AsNoTracking().Where(x=>x.UserID==id)
            .Select(x=>new {x.ClinicID,x.DentistStaffID}).ToListAsync();
        return Ok(new {success=true,access});
    }

    public sealed class AccessItem { public int ClinicID {get;set;} public int DentistStaffID {get;set;} }
    public sealed class AccessRequest { public List<AccessItem> Access {get;set;} = new(); }

    [HttpPut("{id:int}/access")]
    public async Task<IActionResult> SetAccess(int id, AccessRequest request)
    {
        var user=await _context.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.UserID==id);
        if(user==null) return NotFound(new {success=false,message="کاربر یافت نشد."});

        var requested=request.Access.DistinctBy(x=>new {x.ClinicID,x.DentistStaffID}).ToList();
        foreach(var a in requested)
        {
            bool valid=await _context.ClinicStaff.AsNoTracking().AnyAsync(x=>x.ClinicID==a.ClinicID && x.StaffID==a.DentistStaffID)
                && await _context.Clinics.AsNoTracking().AnyAsync(x=>x.ClinicID==a.ClinicID && x.IsActive)
                && await _context.Staff.AsNoTracking().AnyAsync(x=>x.StaffID==a.DentistStaffID && x.StaffType==2);
            if(!valid) return BadRequest(new {success=false,message="یکی از دسترسی‌های مطب/دندانپزشک معتبر نیست."});
        }

        var old=await _context.UserDentists.Where(x=>x.UserID==id).ToListAsync();
        _context.UserDentists.RemoveRange(old);
        _context.UserDentists.AddRange(requested.Select(a=>new UserDentist {UserID=id,ClinicID=a.ClinicID,DentistStaffID=a.DentistStaffID}));
        await _context.SaveChangesAsync();
        return Ok(new {success=true});
    }

    public sealed class CreateRequest { public int StaffID {get;set;} public string Password {get;set;}=string.Empty; public bool IsActive {get;set;}=true; public DateTime? EndDate {get;set;} }
    [HttpPost]
    public async Task<IActionResult> Create(CreateRequest request)
    {
        var staff=await _context.Staff.FirstOrDefaultAsync(x=>x.StaffID==request.StaffID);
        if(staff==null) return BadRequest(new {success=false,message="پرسنل انتخاب‌شده یافت نشد."});
        if(await _context.Users.AnyAsync(x=>x.StaffID==request.StaffID)) return Conflict(new {success=false,message="برای این پرسنل قبلاً حساب کاربری ساخته شده است."});
        if(string.IsNullOrWhiteSpace(request.Password)) return BadRequest(new {success=false,message="رمز عبور الزامی است."});
        var user=new User {StaffID=staff.StaffID,UserName=staff.NationalCode,IsActive=request.IsActive,EndDate=request.EndDate};
        user.PasswordHash=_hasher.HashPassword(user,request.Password);
        _context.Users.Add(user); await _context.SaveChangesAsync();
        return Ok(new {success=true,userID=user.UserID,userName=staff.NationalCode});
    }

    public sealed class UpdateRequest { public bool IsActive {get;set;} public string? NewPassword {get;set;} public DateTime? EndDate {get;set;} }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,UpdateRequest request)
    {
        var user=await _context.Users.FirstOrDefaultAsync(x=>x.UserID==id);
        if(user==null) return NotFound(new {success=false,message="کاربر یافت نشد."});
        var staff=await _context.Staff.AsNoTracking().FirstOrDefaultAsync(x=>x.StaffID==user.StaffID);
        if(staff==null) return BadRequest(new {success=false,message="پرسنل مرتبط یافت نشد."});
        user.UserName=staff.NationalCode; user.IsActive=request.IsActive; user.EndDate=request.EndDate;
        if(!string.IsNullOrWhiteSpace(request.NewPassword)) user.PasswordHash=_hasher.HashPassword(user,request.NewPassword);
        await _context.SaveChangesAsync();
        return Ok(new {success=true});
    }
}
