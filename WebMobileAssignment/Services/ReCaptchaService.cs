using System.Text.Json.Serialization;

namespace WebMobileAssignment.Services
{
    public class ReCaptchaService
    {
  private readonly HttpClient _httpClient;
     private readonly IConfiguration _configuration;
        private readonly string _secretKey;

        public ReCaptchaService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
 _configuration = configuration;
_secretKey = configuration["ReCaptcha:SecretKey"] ?? string.Empty;
        }

        /// <summary>
      /// Verifies the reCAPTCHA v2 token with Google's API
        /// </summary>
        /// <param name="token">The reCAPTCHA token from the client</param>
        /// <returns>True if verification passes, false otherwise</returns>
        public async Task<bool> VerifyTokenAsync(string token)
        {
       Console.WriteLine($"?? [ReCaptcha v2] Starting verification...");
            Console.WriteLine($"?? [ReCaptcha v2] Token length: {token?.Length ?? 0}");

            if (string.IsNullOrWhiteSpace(token))
            {
    Console.WriteLine($"? [ReCaptcha v2] Token is empty!");
         return false;
          }

            if (string.IsNullOrWhiteSpace(_secretKey))
      {
    Console.WriteLine($"? [ReCaptcha v2] Secret key not configured!");
     throw new InvalidOperationException("ReCaptcha SecretKey is not configured in appsettings.json");
         }

            try
   {
      Console.WriteLine($"?? [ReCaptcha v2] Sending verification request to Google...");
 var response = await _httpClient.PostAsync(
             $"https://www.google.com/recaptcha/api/siteverify?secret={_secretKey}&response={token}",
     null);

  Console.WriteLine($"?? [ReCaptcha v2] Response status: {response.StatusCode}");

          if (!response.IsSuccessStatusCode)
        {
   Console.WriteLine($"? [ReCaptcha v2] HTTP request failed with status: {response.StatusCode}");
     return false;
 }

     var jsonResponse = await response.Content.ReadAsStringAsync();
           Console.WriteLine($"?? [ReCaptcha v2] Response JSON: {jsonResponse}");

        var result = System.Text.Json.JsonSerializer.Deserialize<ReCaptchaV2Response>(jsonResponse);

      if (result == null)
      {
     Console.WriteLine($"? [ReCaptcha v2] Failed to deserialize response");
      return false;
    }

          Console.WriteLine($"?? [ReCaptcha v2] Success: {result.Success}");
   Console.WriteLine($"?? [ReCaptcha v2] Hostname: {result.Hostname}");

                if (result.ErrorCodes != null && result.ErrorCodes.Length > 0)
        {
        Console.WriteLine($"?? [ReCaptcha v2] Error codes: {string.Join(", ", result.ErrorCodes)}");
                }

   if (result.Success)
       {
 Console.WriteLine($"? [ReCaptcha v2] Verification PASSED!");
     }
        else
     {
    Console.WriteLine($"? [ReCaptcha v2] Verification FAILED!");
      }

        return result.Success;
          }
          catch (Exception ex)
        {
    Console.WriteLine($"? [ReCaptcha v2] Exception during verification: {ex.Message}");
    Console.WriteLine($"? [ReCaptcha v2] Stack trace: {ex.StackTrace}");
              return false;
   }
        }

  /// <summary>
 /// Gets detailed verification result including error codes
  /// </summary>
        public async Task<ReCaptchaV2Response?> GetVerificationDetailsAsync(string token)
        {
      if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_secretKey))
    {
    return null;
            }

    try
 {
       var response = await _httpClient.PostAsync(
       $"https://www.google.com/recaptcha/api/siteverify?secret={_secretKey}&response={token}",
       null);

       var jsonResponse = await response.Content.ReadAsStringAsync();
         return System.Text.Json.JsonSerializer.Deserialize<ReCaptchaV2Response>(jsonResponse);
        }
            catch
            {
    return null;
  }
        }
    }

    public class ReCaptchaV2Response
    {
        [JsonPropertyName("success")]
      public bool Success { get; set; }

        [JsonPropertyName("challenge_ts")]
     public string ChallengeTs { get; set; } = string.Empty;

    [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = string.Empty;

   [JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
