using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HomeBikeServiceAPI.Services
{
    public class KhaltiPaymentService
    {
        private readonly HttpClient _httpClient;
        private const string KhaltiApiUrl = "https://khalti.com/api/v2/epayment/initiate/";
        private const string SecretKey = "732170ed666f41ec82f072d45e4c43d9"; // Replace with actual key

        public KhaltiPaymentService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> InitiatePaymentAsync(string orderId, string orderName, int amount, string returnUrl)
        {
            var payload = new
            {
                return_url = returnUrl,
                website_url = "https://dev.khalti.com/api/v2/",
                amount = amount,  // Amount in paisa (1000 = 10 NPR)
                purchase_order_id = orderId,
                purchase_order_name = orderName,
                customer_info = new
                {
                    name = "Customer Name",
                    email = "customer@example.com",
                    phone = "9800000000"
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Key {SecretKey}");

            var response = await _httpClient.PostAsync(KhaltiApiUrl, content);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
