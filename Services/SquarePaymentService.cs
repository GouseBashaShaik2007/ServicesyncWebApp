using System.Net.Http.Headers;
using System.Text.Json;

namespace ServicesyncWebApp.Services
{
    public class SquarePaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly string _accessToken;
        private readonly string _baseUrl;

        public SquarePaymentService(IConfiguration config)
        {
            _accessToken = config["Square:AccessToken"];
            var environment = config["Square:Environment"];
            _baseUrl = environment == "Sandbox" ? "https://connect.squareupsandbox.com" : "https://connect.squareup.com";

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        public async Task<string> ProcessPaymentAsync(string token, long amountInCents)
        {
            var requestBody = new
            {
                source_id = token,
                idempotency_key = Guid.NewGuid().ToString(),
                amount_money = new
                {
                    amount = amountInCents,
                    currency = "USD"
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/v2/payments", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Square API error: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var paymentResponse = JsonSerializer.Deserialize<PaymentResponse>(responseContent);

            return paymentResponse.payment.id;
        }

        private class PaymentResponse
        {
            public Payment payment { get; set; }
        }

        private class Payment
        {
            public string id { get; set; }
        }
    }
}
