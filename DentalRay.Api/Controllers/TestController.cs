using DentalRay.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalRay.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly DentalRayDbContext _context;

        public TestController(DentalRayDbContext context)
        {
            _context = context;
        }

        [HttpGet("database")]
        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                bool connected = await _context.Database.CanConnectAsync();

                if (connected)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Database connection successful."
                    });
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "Could not connect to database."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }
}