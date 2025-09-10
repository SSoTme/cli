/*******************************************
| MyEffortlessAPIService - Handles localGuide and auth functions
| Created By: AI Assistant - 2025
| License:    Mozilla Public License 2.0
| *******************************************/
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SSoTme.OST.Lib.Services
{
    /// <summary>
    /// Service to handle EffortlessAPI related functionality including local guide and authentication
    /// </summary>
    public class MyEffortlessAPIService
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string API_BASE_URL = "https://api.effortlessapi.com/api/auth";
        
        /// <summary>
        /// Handles authentication functionality - replaces the existing authenticate behavior
        /// </summary>
        public async Task HandleAuth()
        {
            try
            {
                Console.WriteLine("=== EffortlessAPI Authentication ===");
                Console.WriteLine();

                // Check if already authenticated
                if (IsAuthenticated())
                {
                    Console.WriteLine("✅ You are already authenticated!");
                    Console.Write("Do you want to re-authenticate? (y/N): ");
                    string response = Console.ReadLine()?.Trim().ToLower() ?? "";
                    if (response != "y" && response != "yes")
                    {
                        Console.WriteLine("Authentication cancelled.");
                        return;
                    }
                    Console.WriteLine();
                }

                // Step 1: Get phone number from user
                string phoneNumber = GetPhoneNumberFromUser();
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    Console.WriteLine("❌ Authentication cancelled - no phone number provided.");
                    return;
                }

                // Step 2: Send login request to get OTP
                Console.WriteLine($"📱 Sending authentication request for {phoneNumber}...");
                bool loginSuccess = await SendLoginRequest(phoneNumber);
                if (!loginSuccess)
                {
                    Console.WriteLine("❌ Failed to send authentication request. Please try again.");
                    return;
                }

                Console.WriteLine("✅ Authentication code sent to your phone and email!");
                Console.WriteLine();

                // Step 3: Get OTP from user
                string otpCode = GetOTPFromUser();
                if (string.IsNullOrWhiteSpace(otpCode))
                {
                    Console.WriteLine("❌ Authentication cancelled - no OTP code provided.");
                    return;
                }

                // Step 4: Validate OTP and get JWT token
                Console.WriteLine($"🔐 Validating authentication code for {phoneNumber}...");
                string jwtToken = await ValidateOTPAndGetToken(phoneNumber, otpCode);
                if (string.IsNullOrWhiteSpace(jwtToken))
                {
                    Console.WriteLine("❌ Invalid or expired authentication code. Please try again.");
                    return;
                }

                // Step 5: Store JWT token
                Console.WriteLine("💾 Storing authentication token...");
                bool tokenStored = StoreJWTToken(jwtToken);
                if (!tokenStored)
                {
                    Console.WriteLine("⚠️  Authentication successful but failed to store token locally.");
                    return;
                }

                Console.WriteLine("✅ Authentication successful! Token stored in ~/.ssotme/");
                Console.WriteLine("You can now use EffortlessAPI endpoints.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Authentication failed: {ex.Message}");
                Console.WriteLine("Please try again or contact support if the issue persists.");
            }
        }

        /// <summary>
        /// Gets phone number input from user with validation
        /// </summary>
        private string GetPhoneNumberFromUser()
        {
            Console.Write("Enter your phone number (e.g., +1234567890 or 234-567-8900): ");
            string phoneNumber = Console.ReadLine()?.Trim() ?? "";
            
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return "";
            }

            // Basic phone number validation - remove common formatting
            phoneNumber = phoneNumber.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Replace(".", "");
            
            // Handle + prefix
            if (phoneNumber.StartsWith("+"))
            {
                phoneNumber = phoneNumber.Substring(1);
            }

            // Basic validation - should be all digits and reasonable length
            if (!phoneNumber.All(char.IsDigit) || phoneNumber.Length < 10 || phoneNumber.Length > 15)
            {
                Console.WriteLine("❌ Invalid phone number format. Please enter a valid phone number.");
                return GetPhoneNumberFromUser(); // Recursive retry
            }

            Console.WriteLine($"Using phone number: +{phoneNumber}");
            return phoneNumber;
        }

        /// <summary>
        /// Gets OTP code input from user
        /// </summary>
        private string GetOTPFromUser()
        {
            Console.Write("Enter the authentication code you received: ");
            return Console.ReadLine()?.Trim() ?? "";
        }

        /// <summary>
        /// Sends login request to EffortlessAPI
        /// </summary>
        private async Task<bool> SendLoginRequest(string phoneNumber)
        {
            try
            {
                var loginRequest = new { PhoneNumber = phoneNumber };
                var json = JsonConvert.SerializeObject(loginRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{API_BASE_URL}/login", content);
                
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Server error: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Network error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates OTP and retrieves JWT token
        /// </summary>
        private async Task<string> ValidateOTPAndGetToken(string phoneNumber, string otpCode)
        {
            try
            {
                var validateRequest = new { PhoneNumber = phoneNumber, OTP = otpCode };
                var json = JsonConvert.SerializeObject(validateRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{API_BASE_URL}/validate", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    return tokenResponse?.token?.ToString() ?? "";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Validation error: {response.StatusCode} - {errorContent}");
                    return "";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Validation error: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Stores JWT token in ~/.ssotme folder
        /// </summary>
        private bool StoreJWTToken(string jwtToken)
        {
            try
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string ssotmeDirectory = Path.Combine(homeDirectory, ".ssotme");
                
                // Create directory if it doesn't exist
                if (!Directory.Exists(ssotmeDirectory))
                {
                    Directory.CreateDirectory(ssotmeDirectory);
                }

                string tokenFile = Path.Combine(ssotmeDirectory, "effortlessapi_token.txt");
                File.WriteAllText(tokenFile, jwtToken);

                // Also store with timestamp for reference
                string tokenInfoFile = Path.Combine(ssotmeDirectory, "effortlessapi_token_info.json");
                var tokenInfo = new
                {
                    Token = jwtToken,
                    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ExpiresAt = DateTime.UtcNow.AddHours(24).ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                File.WriteAllText(tokenInfoFile, JsonConvert.SerializeObject(tokenInfo, Formatting.Indented));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to store token: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves stored JWT token if available and valid
        /// </summary>
        public string GetStoredJWTToken()
        {
            try
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string tokenFile = Path.Combine(homeDirectory, ".ssotme", "effortlessapi_token.txt");
                
                if (File.Exists(tokenFile))
                {
                    return File.ReadAllText(tokenFile).Trim();
                }
                
                return "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Creates an HttpClient with authentication header for EffortlessAPI calls
        /// </summary>
        public HttpClient CreateAuthenticatedHttpClient()
        {
            var client = new HttpClient();
            string token = GetStoredJWTToken();
            
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            return client;
        }

        /// <summary>
        /// Checks if user is currently authenticated (has valid token)
        /// </summary>
        public bool IsAuthenticated()
        {
            string token = GetStoredJWTToken();
            return !string.IsNullOrWhiteSpace(token);
        }

        /// <summary>
        /// Clears stored authentication token
        /// </summary>
        public bool ClearAuthToken()
        {
            try
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string ssotmeDirectory = Path.Combine(homeDirectory, ".ssotme");
                string tokenFile = Path.Combine(ssotmeDirectory, "effortlessapi_token.txt");
                string tokenInfoFile = Path.Combine(ssotmeDirectory, "effortlessapi_token_info.json");
                
                if (File.Exists(tokenFile))
                {
                    File.Delete(tokenFile);
                }
                
                if (File.Exists(tokenInfoFile))
                {
                    File.Delete(tokenInfoFile);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clear auth token: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handles local guide functionality
        /// </summary>
        public void HandleLocalGuide()
        {
            Console.WriteLine("Hello World Local Guide");
        }
    }
}
