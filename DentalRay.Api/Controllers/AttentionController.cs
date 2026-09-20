using DentalRay.Api.Data;
using DentalRay.Api.Models;
using DentalRay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    /// <summary>
    /// Who needs to be contacted today.
    ///
    /// The clinic has three reasons to reach a patient: a visit is coming up, a
    /// follow-up has come due, or money is still owed. Rather than leaving the user
    /// to hunt for them, this endpoint lists them in one place. It only reports;
    /// nothing is sent automatically, so the clinic stays in control.
    /// </summary>
    [ApiController]
    [Route("api/attention")]
    public class AttentionController : ControllerBase
    {
        private readonly DentalRayDbContext _db;
        private readonly StudyAccessService _access;

        public AttentionController(DentalRayDbContext db, StudyAccessService access)
        {
            _db = db;
            _access = access;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int take = 20)
        {
            take = Math.Clamp(take, 1, 100);
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            // Only studies the caller may see are considered.
            var accessible = _access.ApplyAccess(_db.RadiologyStudies.AsNoTracking(), User);

            // Appointments within the reminder window: today and tomorrow.
            var upcoming = await (
                from appt in _db.Appointments.AsNoTracking()
                where appt.Status == 1
                   && appt.AppointmentDate >= today
                   && appt.AppointmentDate < tomorrow.AddDays(1)
                   && appt.ReminderSentAt == null
                   && accessible.Any(s => s.PatientID == appt.PatientID)
                join patient in _db.Patients.AsNoTracking() on appt.PatientID equals patient.PatientID
                orderby appt.AppointmentDate
                select new
                {
                    Kind = "appointment",
                    PatientID = patient.PatientID,
                    PatientName = patient.FirstName + " " + patient.LastName,
                    Mobile = patient.Mobile,
                    DueDate = appt.AppointmentDate,
                    Detail = "نوبت پیش‌رو",
                    TemplateKey = "appointment-reminder",
                    AppointmentID = (int?)appt.AppointmentID
                }).Take(take).ToListAsync();

            // Waiting studies whose follow-up has come due.
            var dueFollowUps = await (
                from study in accessible
                where study.Status == 3 && study.FollowUpDate != null && study.FollowUpDate <= today
                join patient in _db.Patients.AsNoTracking() on study.PatientID equals patient.PatientID
                orderby study.FollowUpDate
                select new
                {
                    Kind = "followup",
                    PatientID = patient.PatientID,
                    PatientName = patient.FirstName + " " + patient.LastName,
                    Mobile = patient.Mobile,
                    DueDate = study.FollowUpDate,
                    Detail = "پیگیری مطالعه",
                    TemplateKey = "follow-up",
                    AppointmentID = (int?)null
                }).Take(take).ToListAsync();

            // Patients with an outstanding balance, largest first.
            var debtors = await (
                from patient in _db.Patients.AsNoTracking()
                where patient.IsActive
                   && accessible.Any(s => s.PatientID == patient.PatientID)
                let gross = _db.StudyActions.AsNoTracking()
                    .Where(a => accessible.Any(s => s.StudyID == a.StudyID && s.PatientID == patient.PatientID))
                    .Sum(a => (decimal?)a.Amount) ?? 0m
                let discount = _db.StudyActions.AsNoTracking()
                    .Where(a => accessible.Any(s => s.StudyID == a.StudyID && s.PatientID == patient.PatientID))
                    .Sum(a => (decimal?)a.DiscountAmount) ?? 0m
                let received = _db.StudyPayments.AsNoTracking()
                    .Where(p => accessible.Any(s => s.StudyID == p.StudyID && s.PatientID == patient.PatientID))
                    .Sum(p => p.IsRefund ? -(decimal?)p.Amount : (decimal?)p.Amount) ?? 0m
                let balance = gross - discount - received
                where balance > 0
                orderby balance descending
                select new
                {
                    Kind = "balance",
                    PatientID = patient.PatientID,
                    PatientName = patient.FirstName + " " + patient.LastName,
                    Mobile = patient.Mobile,
                    DueDate = (DateTime?)null,
                    Detail = "مانده حساب",
                    TemplateKey = "balance-due",
                    AppointmentID = (int?)null,
                    Balance = balance
                }).Take(take).ToListAsync();

            return Ok(new
            {
                success = true,
                counts = new
                {
                    appointments = upcoming.Count,
                    followUps = dueFollowUps.Count,
                    balances = debtors.Count,
                    total = upcoming.Count + dueFollowUps.Count + debtors.Count
                },
                appointments = upcoming,
                followUps = dueFollowUps,
                balances = debtors
            });
        }
    }
}
