using Microsoft.AspNetCore.Mvc;
using ServicesyncWebApp.Models;
using ServicesyncWebApp.Services;

namespace ServicesyncWebApp.Controllers
{
    [ApiController]
    [Route("api/email")]
    public class EmailController : ControllerBase
    {
        [HttpPost("send")]  // 👈 must be POST
        public async Task<IActionResult> Send([FromBody] EmailRequest req, [FromServices] IEmailSender sender)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.To)
                || string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Html))
                return BadRequest("To, Subject, and Html are required.");

            await sender.SendAsync(req.To, req.Subject, req.Html, req.Text);
            return Ok(new { ok = true });
        }

        [HttpGet("ping")]
        public IActionResult Ping() => Ok("email-ok");
    }
}
