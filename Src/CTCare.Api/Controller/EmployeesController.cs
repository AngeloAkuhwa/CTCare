using Microsoft.AspNetCore.Mvc;

namespace CTCare.Api.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeesController: BaseApiController
    {
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            var response = new
            {
                Message = "Employees API is alive!",
                ServerTime = DateTime.UtcNow
            };

            return Ok(response);
        }
    }
}
