using Microsoft.AspNetCore.Mvc;

namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        // Simple Test API
        [HttpGet("ping")]
        public IActionResult TestApi()
        {
            return Ok(new { message = "API is working successfully!" });
        }
    }
}
