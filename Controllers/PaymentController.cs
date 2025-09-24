using Microsoft.AspNetCore.Mvc;
using ServicesyncWebApp.Services;

namespace ServicesyncWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly SquarePaymentService _paymentService;

        public PaymentController(SquarePaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("process")]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequest request)
        {
            try
            {
                var paymentId = await _paymentService.ProcessPaymentAsync(request.SourceId, request.Amount);
                return Ok(new { paymentId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class PaymentRequest
    {
        public string SourceId { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; } = "USD";
    }
}
