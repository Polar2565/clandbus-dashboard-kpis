using Microsoft.AspNetCore.Mvc;

namespace ClandbusERPIntegration.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "API running successfully",
                project = "Clandbus ERP Integration Dashboard",
                timestamp = DateTime.UtcNow
            });
        }
    }
}