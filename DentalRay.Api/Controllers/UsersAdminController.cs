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
                u.UserID,u.StaffID,UserName=s.NationalCode,s.NationalCode,s.FirstName,s.LastName,s.StaffType,u.IsActive,
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

    public sealed class CreateRequest { public int StaffID {get;set;} public string Password {get;set;}=string.Empty; public bool IsActive {get;set;}=true; }
    [HttpPost]
    public async Task<IActionResult> Create(CreateRequest request)
    {
        var staff=await _context.Staff.FirstOrDefaultAsync(x=>x.StaffID==request.StaffID);
        if(staff==null) return BadRequest(new {success=false,message="پرسنل انتخاب‌شده یافت نشد."});
        if(await _context.Users.AnyAsync(x=>x.StaffID==request.StaffID)) return Conflict(new {success=false,message="برای این پرسنل قبلاً حساب کاربری ساخته شده است."});
        if(string.IsNullOrWhiteSpace(request.Password)) return BadRequest(new {success=false,message="رمز عبور الزامی است."});
        var user=new User {StaffID=staff.StaffID,UserName=staff.NationalCode,IsActive=request.IsActive};
        user.PasswordHash=_hasher.HashPassword(user,request.Password);
        _context.Users.Add(user); await _context.SaveChangesAsync();
        return Ok(new {success=true,userID=user.UserID,userName=staff.NationalCode});
    }

    public sealed class UpdateRequest { public bool IsActive {get;set;} public string? NewPassword {get;set;} }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,UpdateRequest request)
    {
        var user=await _context.Users.FirstOrDefaultAsync(x=>x.UserID==id);
        if(user==null) return NotFound(new {success=false,message="کاربر یافت نشد."});
        var staff=await _context.Staff.AsNoTracking().FirstOrDefaultAsync(x=>x.StaffID==user.StaffID);
        if(staff==null) return BadRequest(new {success=false,message="پرسنل مرتبط یافت نشد."});
        user.UserName=staff.NationalCode; user.IsActive=request.IsActive;
        if(!string.IsNullOrWhiteSpace(request.NewPassword)) user.PasswordHash=_hasher.HashPassword(user,request.NewPassword);
        await _context.SaveChangesAsync();
        return Ok(new {success=true});
    }
}
