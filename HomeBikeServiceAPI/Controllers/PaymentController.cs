using HomeBikeServiceAPI.Services;
using HomeBikeServiceAPI.Data;
using HomeBikeServiceAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace HomeBikeServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;
        private readonly KhaltiPaymentService _khaltiPaymentService;

        public PaymentController(HttpClient httpClient, AppDbContext context, KhaltiPaymentService khaltiPaymentService)
        {
            _httpClient = httpClient;
            _context = context;
            _khaltiPaymentService = khaltiPaymentService;
        }


        [HttpPost("pay/{userId}")]
        public async Task<IActionResult> MakePayment(int userId, [FromBody] PaymentRequestDto request)
        {
            // Step 1: Check if user exists
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found." });
            }

            // Step 2: Fetch total sum from TotalSumController
            var totalSumResponse = await _httpClient.GetAsync($"https://api-rj9q.onrender.com/api/TotalSum/{userId}");
            if (!totalSumResponse.IsSuccessStatusCode)
            {
                return BadRequest(new { success = false, message = "Failed to retrieve total sum." });
            }

            var totalSumData = await totalSumResponse.Content.ReadAsStringAsync();
            var totalSum = JsonConvert.DeserializeObject<TotalSumResponseDto>(totalSumData)?.TotalAmount ?? 0;

            if (totalSum <= 0)
            {
                return BadRequest(new { success = false, message = "No pending payments found." });
            }

            // Step 3: Get unpaid cart items
            var cartItems = await _context.Carts.Where(c => c.UserId == userId && !c.IsPaymentDone).ToListAsync();

            // Step 4: Call Khalti API for payment
            string orderId = Guid.NewGuid().ToString();
            string orderName = $"Order for User {userId}";
            string returnUrl = request.ReturnUrl;

            var khaltiResponse = await _khaltiPaymentService.InitiatePaymentAsync(orderId, orderName, (int)(totalSum * 100), returnUrl);

            if (!string.IsNullOrEmpty(khaltiResponse))
            {
                // Step 5: Parse Khalti Response
                var paymentData = JsonConvert.DeserializeObject<KhaltiPaymentResponseDto>(khaltiResponse);

                // Step 6: Store Payment Record
                var paymentRecord = new Payment
                {
                    TransactionId = paymentData.TransactionId,
                    Pidx = paymentData.Pidx,
                    Amount = totalSum,
                    DataFromVerificationReq = khaltiResponse,
                    ApiQueryFromUser = JsonConvert.SerializeObject(request),
                    PaymentGateway = PaymentGatewayType.Khalti,
                    Status = PaymentStatus.Success
                };

                _context.Payments.Add(paymentRecord);

                // Step 7: Update IsPaymentDone for carts
                foreach (var cart in cartItems)
                {
                    cart.IsPaymentDone = true;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Payment successful, cart updated, and payment recorded.",
                    paymentData
                });
            }
            else
            {
                return BadRequest(new { success = false, message = "Payment failed." });
            }
        }


        public class TotalSumResponseDto
        {
            public decimal TotalAmount { get; set; }
        }


        public class KhaltiPaymentResponseDto
        {
            public string? TransactionId { get; set; }
            public string? Pidx { get; set; }
        }


        public class PaymentRequestDto
        {
            public string ReturnUrl { get; set; }
        }
    }
}
