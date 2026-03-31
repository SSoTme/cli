/*******************************************
| MyEffortlessAPIService - Handles localGuide and auth functions
| Created By: AI Assistant - 2025
| License:    Mozilla Public License 2.0
| *******************************************/
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SSoTme.OST.Lib.Extensions;

namespace SSoTme.OST.Lib.Services
{
    /// <summary>
    /// Service to handle EffortlessAPI related functionality including local guide and authentication
    /// </summary>
    public class MyEffortlessAPIService
    {
        /// <summary>
        /// Handles authentication functionality via email magic link through list-transpilers tool
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
                    var storedEmail = GetStoredEmail();
                    if (IsTokenExpired())
                    {
                        Console.WriteLine("Your session has expired. Attempting to refresh...");
                        if (RefreshToken())
                        {
                            storedEmail = GetStoredEmail();
                            Console.WriteLine(string.IsNullOrEmpty(storedEmail)
                                ? "Session refreshed. You are already authenticated."
                                : $"Session refreshed. You are already authenticated as {storedEmail}.");
                            Console.Write("Do you want to re-authenticate? (y/N): ");
                            string refreshResponse = Console.ReadLine()?.Trim().ToLower() ?? "";
                            if (refreshResponse != "y" && refreshResponse != "yes")
                            {
                                Console.WriteLine("Authentication cancelled.");
                                return;
                            }
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.WriteLine("Session refresh failed. Please log in again.");
                            ClearAuthToken();
                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Console.WriteLine(string.IsNullOrEmpty(storedEmail)
                            ? "You are already authenticated."
                            : $"You are already authenticated as {storedEmail}.");
                        Console.Write("Do you want to re-authenticate? (y/N): ");
                        string response = Console.ReadLine()?.Trim().ToLower() ?? "";
                        if (response != "y" && response != "yes")
                        {
                            Console.WriteLine("Authentication cancelled.");
                            return;
                        }
                        Console.WriteLine();
                    }
                }

                // Step 1: Get email from user
                string email = GetEmailFromUser();
                if (string.IsNullOrWhiteSpace(email))
                {
                    Console.WriteLine("Authentication cancelled - no email provided.");
                    return;
                }

                // Step 2: Send auth request via list-transpilers tool
                Console.WriteLine($"Sending verification code to {email}...");
                var authResult = InvokeListTranspilerAuth($"-p mode=auth -p email={email}");
                if (authResult == null || !authResult.Success)
                {
                    Console.WriteLine($"Failed to send verification code: {authResult?.Error ?? "Unknown error"}");
                    return;
                }

                Console.WriteLine("Verification code sent to your email.");
                Console.WriteLine();

                // Step 3: Get verification code from user
                Console.Write("Enter the verification code you received: ");
                string code = Console.ReadLine()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(code))
                {
                    Console.WriteLine("Authentication cancelled - no code provided.");
                    return;
                }

                // Step 4: Verify code and get JWT token via list-transpilers tool
                Console.WriteLine("Verifying code...");
                var verifyResult = InvokeListTranspilerAuth($"-p mode=verify -p email={email} -p code={code}");
                if (verifyResult == null || !verifyResult.Success || string.IsNullOrEmpty(verifyResult.Token))
                {
                    Console.WriteLine($"Verification failed: {verifyResult?.Error ?? "Invalid or expired code"}");
                    return;
                }

                // Step 5: Store JWT token
                bool tokenStored = StoreJWTToken(verifyResult.Token, email);
                if (!tokenStored)
                {
                    Console.WriteLine("Authentication successful but failed to store token locally.");
                    return;
                }

                Console.WriteLine("Authentication successful!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets email input from user with basic validation
        /// </summary>
        private string GetEmailFromUser()
        {
            Console.Write("Enter your email address: ");
            string email = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(email))
                return "";

            if (!email.Contains("@") || !email.Contains("."))
            {
                Console.WriteLine("Invalid email format. Please enter a valid email address.");
                return GetEmailFromUser();
            }

            return email;
        }

        public void HandleSubscription(string jwt)
        {
            if (string.IsNullOrEmpty(jwt))
            {
                Console.WriteLine("You are not logged in. Use `effortless login` first.");
                return;
            }

            var result = InvokeListTranspilerAuth($"-p mode=viewPlan -p jwt={jwt}");
            if (result == null || !result.Success)
            {
                Console.WriteLine($"Failed to fetch subscription: {result?.Error ?? "Unknown error"}");
                return;
            }

            var plan = result.Plan ?? "free";
            CliLog.LogLine("Account:", GetStoredEmail(), ConsoleColor.Cyan);
            var planColor = plan == "free" ? ConsoleColor.Yellow : ConsoleColor.Green;
            CliLog.LogLine("Subscription plan:", plan, planColor);
            if (plan == "free")
            {
                Console.WriteLine("Some tools may be unavailable on the free tier.");
                Console.WriteLine($"You can upgrade your plan by logging in at bases.effortlessapi.com with email: {GetStoredEmail()}");
                Console.WriteLine($"and contacting us at https://bases.effortlessapi.com/dashboard/contact");
            }
        }

        /// <summary>
        /// Invokes the list-transpilers tool with auth parameters and parses the result
        /// </summary>
        private AuthResult InvokeListTranspilerAuth(string paramString)
        {
            try
            {
                var commandLine = $"{SSoTme.OST.Lib.CLIOptions.SSoTmeCLIHandler.TRANSPILERS_LISTER_TOOL_NAME} {paramString}";
                var output = SSoTme.OST.Lib.CLIOptions.SSoTmeCLIHandler.InvokeToolAndGetOutput(commandLine, "auth-result.json");

                if (string.IsNullOrEmpty(output))
                    return new AuthResult { Success = false, Error = "No response from auth service" };

                return JsonConvert.DeserializeObject<AuthResult>(output);
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Refreshes the stored JWT token via the list-transpilers tool
        /// </summary>
        public bool RefreshToken()
        {
            try
            {
                string currentToken = GetStoredJWTToken();
                if (string.IsNullOrWhiteSpace(currentToken))
                    return false;

                var result = InvokeListTranspilerAuth($"-p mode=refresh -p jwt={currentToken}");
                if (result != null && result.Success && !string.IsNullOrEmpty(result.Token))
                {
                    StoreJWTToken(result.Token, GetStoredEmail());
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the stored token is near expiry and refreshes it if needed
        /// </summary>
        public void EnsureValidToken()
        {
            try
            {
                if (IsAuthenticated() && IsTokenExpired())
                    RefreshToken();
            }
            catch
            {
                // Best effort - don't block on refresh failures
            }
        }

        /// <summary>
        /// Stores JWT token in ~/.ssotme folder
        /// </summary>
        private bool StoreJWTToken(string jwtToken, string email = null)
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
                    Email = email,
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

        public string GetStoredEmail()
        {
            try
            {
                string tokenInfoFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssotme", "effortlessapi_token_info.json");
                if (File.Exists(tokenInfoFile))
                {
                    var info = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(File.ReadAllText(tokenInfoFile));
                    return info?["Email"]?.ToString();
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Creates an HttpClient with authentication header for EffortlessAPI calls.
        /// Automatically refreshes the token if it is near expiry.
        /// </summary>
        public HttpClient CreateAuthenticatedHttpClient()
        {
            EnsureValidToken();

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
        public string CheckLoginStatus()
        {
            if (!IsAuthenticated())
            {
                Console.WriteLine("You are not logged in. Some tools may be unavailable.");
                return null;
            }

            if (IsTokenExpired())
            {
                Console.WriteLine("Refreshing session...");
                if (RefreshToken())
                {
                    return GetStoredJWTToken();
                }
                else
                {
                    Console.WriteLine("Session refresh failed. You are not logged in. Some tools may be unavailable.");
                    ClearAuthToken();
                    return null;
                }
            }

            return GetStoredJWTToken();
        }

        public bool IsAuthenticated()
        {
            string token = GetStoredJWTToken();
            return !string.IsNullOrWhiteSpace(token);
        }

        public bool IsTokenExpired()
        {
            try
            {
                string token = GetStoredJWTToken();
                if (string.IsNullOrWhiteSpace(token)) return true;
                var parts = token.Split('.');
                if (parts.Length != 3) return true;
                var payloadBase64 = parts[1];
                switch (payloadBase64.Length % 4)
                {
                    case 2: payloadBase64 += "=="; break;
                    case 3: payloadBase64 += "="; break;
                }
                var payload = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64)));
                long exp = (long)(payload?.exp ?? 0);
                return exp == 0 || DateTimeOffset.FromUnixTimeSeconds(exp) <= DateTimeOffset.UtcNow;
            }
            catch { return true; }
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
        /// Handles local guide functionality - monitors and syncs project files
        /// </summary>
        public async Task HandleLocalGuide(string projectName)
        {
            try
            {
                Console.WriteLine("=== EffortlessAPI Local Guide ===");
                Console.WriteLine($"Project: {projectName}");
                Console.WriteLine();

                // Check authentication
                if (!IsAuthenticated())
                {
                    Console.WriteLine("❌ Not authenticated. Please run authentication first.");
                    Console.WriteLine("Use: cli --authenticate");
                    return;
                }

                Console.WriteLine("✅ Authentication verified");

                // Initialize project monitor
                var projectMonitor = new ProjectMonitor(projectName, this);
                
                Console.WriteLine($"📁 Monitoring project: {projectName}");
                Console.WriteLine($"📂 Looking for: {projectName}.json and {projectName}.xlsx");
                Console.WriteLine("🔄 Starting synchronization monitoring...");
                Console.WriteLine("Press Ctrl+C to stop monitoring");
                Console.WriteLine();

                // Start monitoring
                await projectMonitor.StartMonitoring();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Local guide failed: {ex.Message}");
                Console.WriteLine("Please check your project name and try again.");
            }
        }
    }

    /// <summary>
    /// Monitors a project for file changes and synchronizes with EffortlessAPI
    /// </summary>
    public class ProjectMonitor
    {
        private readonly string _projectName;
        private readonly MyEffortlessAPIService _apiService;
        private readonly string _jsonFilePath;
        private readonly string _xlsxFilePath;
        private readonly string _ssotJsonFilePath;
        private DateTime _lastJsonModified = DateTime.MinValue;
        private DateTime _lastXlsxModified = DateTime.MinValue;
        private string _lastJsonContent = "";
        private bool _isMonitoring = false;
        private const int POLLING_INTERVAL_MS = 3000; // 3 seconds

        public ProjectMonitor(string projectName, MyEffortlessAPIService apiService)
        {
            _projectName = projectName;
            _apiService = apiService;
            _jsonFilePath = Path.Combine(Environment.CurrentDirectory, $"{projectName}.json");
            _xlsxFilePath = Path.Combine(Environment.CurrentDirectory, $"{projectName}.xlsx");
            _ssotJsonFilePath = Path.Combine(Environment.CurrentDirectory, "ssot", $"{projectName}.json");
        }

        public async Task StartMonitoring()
        {
            _isMonitoring = true;
            
            // Set up Ctrl+C handler
            Console.CancelKeyPress += (sender, e) => {
                e.Cancel = true;
                _isMonitoring = false;
                Console.WriteLine("\n🛑 Stopping monitoring...");
            };

            // Initial file discovery
            DiscoverProjectFiles();

            // Start monitoring loop
            while (_isMonitoring)
            {
                try
                {
                    await MonitoringCycle();
                    await Task.Delay(POLLING_INTERVAL_MS);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Monitoring error: {ex.Message}");
                    await Task.Delay(POLLING_INTERVAL_MS * 2); // Wait longer on error
                }
            }

            Console.WriteLine("✅ Monitoring stopped");
        }

        private void DiscoverProjectFiles()
        {
            // Check for JSON file in current directory or ssot subdirectory
            string activeJsonPath = null;
            if (File.Exists(_jsonFilePath))
            {
                activeJsonPath = _jsonFilePath;
            }
            else if (File.Exists(_ssotJsonFilePath))
            {
                activeJsonPath = _ssotJsonFilePath;
            }

            if (activeJsonPath != null)
            {
                Console.WriteLine($"📄 Found JSON file: {Path.GetFileName(activeJsonPath)}");
                _lastJsonModified = File.GetLastWriteTime(activeJsonPath);
                _lastJsonContent = File.ReadAllText(activeJsonPath);
            }
            else
            {
                Console.WriteLine($"⚠️  JSON file not found: {_projectName}.json");
            }

            if (File.Exists(_xlsxFilePath))
            {
                Console.WriteLine($"📊 Found Excel file: {Path.GetFileName(_xlsxFilePath)}");
                _lastXlsxModified = File.GetLastWriteTime(_xlsxFilePath);
            }
            else
            {
                Console.WriteLine($"⚠️  Excel file not found: {_projectName}.xlsx");
            }
        }

        private async Task MonitoringCycle()
        {
            // 1. Check for local file changes
            await CheckLocalFileChanges();

            // 2. Poll remote status
            await CheckRemoteStatus();
        }

        private async Task CheckLocalFileChanges()
        {
            // Check JSON file changes
            string activeJsonPath = GetActiveJsonPath();
            if (activeJsonPath != null && File.Exists(activeJsonPath))
            {
                var currentModified = File.GetLastWriteTime(activeJsonPath);
                if (currentModified > _lastJsonModified)
                {
                    Console.WriteLine($"📝 Local JSON file changed: {Path.GetFileName(activeJsonPath)}");
                    
                    var newContent = File.ReadAllText(activeJsonPath);
                    if (newContent != _lastJsonContent)
                    {
                        Console.WriteLine("🔄 Syncing local changes to remote...");
                        bool success = await SyncLocalToRemote(newContent);
                        if (success)
                        {
                            _lastJsonContent = newContent;
                            _lastJsonModified = currentModified;
                            Console.WriteLine("✅ Local changes synced successfully");
                        }
                        else
                        {
                            Console.WriteLine("❌ Failed to sync local changes");
                        }
                    }
                }
            }
        }

        private async Task CheckRemoteStatus()
        {
            try
            {
                var status = await GetProjectStatus();
                if (status != null)
                {
                    await ProcessRemoteStatus(status);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Failed to check remote status: {ex.Message}");
            }
        }

        private async Task<ProjectStatus> GetProjectStatus()
        {
            using var client = _apiService.CreateAuthenticatedHttpClient();
            var response = await client.GetAsync($"https://api.effortlessapi.com/api/projects/{_projectName}/sync/status");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ProjectStatus>(content);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("❌ Authentication expired. Please re-authenticate.");
                _isMonitoring = false;
                return null;
            }
            else
            {
                Console.WriteLine($"⚠️  API error: {response.StatusCode}");
                return null;
            }
        }

        private async Task ProcessRemoteStatus(ProjectStatus status)
        {
            bool hasRemoteChanges = false;

            // Check if remote JSON is newer
            if (status.Rulebook != null && !string.IsNullOrEmpty(status.JsonContent))
            {
                string activeJsonPath = GetActiveJsonPath();
                if (activeJsonPath != null)
                {
                    var remoteModified = DateTime.Parse(status.Rulebook.LastModified);
                    if (remoteModified > _lastJsonModified || status.JsonContent != _lastJsonContent)
                    {
                        Console.WriteLine("📥 Remote JSON changes detected, updating local file...");
                        await UpdateLocalJsonFile(status.JsonContent, activeJsonPath);
                        hasRemoteChanges = true;
                    }
                }
            }

            // Check if remote Excel is newer
            if (status.Xlsx != null && File.Exists(_xlsxFilePath))
            {
                var remoteModified = DateTime.Parse(status.Xlsx.LastModified);
                if (remoteModified > _lastXlsxModified)
                {
                    Console.WriteLine("📥 Remote Excel changes detected, downloading...");
                    await DownloadExcelFile();
                    hasRemoteChanges = true;
                }
            }

            if (hasRemoteChanges)
            {
                Console.WriteLine("✅ Remote changes synchronized");
            }
        }

        private async Task<bool> SyncLocalToRemote(string jsonContent)
        {
            try
            {
                using var client = _apiService.CreateAuthenticatedHttpClient();
                var request = new { content = JsonConvert.DeserializeObject(jsonContent) };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"https://api.effortlessapi.com/api/projects/{_projectName}/save-json", content);
                
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Sync error: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync error: {ex.Message}");
                return false;
            }
        }

        private async Task UpdateLocalJsonFile(string jsonContent, string filePath)
        {
            try
            {
                // Create backup
                string backupPath = filePath + ".backup";
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, backupPath, true);
                }

                // Write new content
                var formattedJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(jsonContent), Formatting.Indented);
                File.WriteAllText(filePath, formattedJson);
                
                _lastJsonModified = File.GetLastWriteTime(filePath);
                _lastJsonContent = formattedJson;
                
                Console.WriteLine($"✅ Updated local JSON: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to update local JSON: {ex.Message}");
            }
        }

        private async Task DownloadExcelFile()
        {
            try
            {
                using var client = _apiService.CreateAuthenticatedHttpClient();
                var response = await client.GetAsync($"https://api.effortlessapi.com/api/my-projects/{_projectName}/xlsx");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsByteArrayAsync();
                    
                    // Create backup
                    string backupPath = _xlsxFilePath + ".backup";
                    if (File.Exists(_xlsxFilePath))
                    {
                        File.Copy(_xlsxFilePath, backupPath, true);
                    }

                    File.WriteAllBytes(_xlsxFilePath, content);
                    _lastXlsxModified = File.GetLastWriteTime(_xlsxFilePath);
                    
                    Console.WriteLine($"✅ Updated local Excel: {Path.GetFileName(_xlsxFilePath)}");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to download Excel: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to download Excel: {ex.Message}");
            }
        }

        private string GetActiveJsonPath()
        {
            if (File.Exists(_jsonFilePath))
                return _jsonFilePath;
            if (File.Exists(_ssotJsonFilePath))
                return _ssotJsonFilePath;
            return null;
        }
    }

    /// <summary>
    /// Project status response from the API
    /// </summary>
    public class ProjectStatus
    {
        [JsonProperty("project_id")]
        public string ProjectId { get; set; }

        [JsonProperty("json_content")]
        public string JsonContent { get; set; }

        [JsonProperty("in_sync")]
        public bool InSync { get; set; }

        [JsonProperty("rulebook")]
        public ProjectFileInfo Rulebook { get; set; }

        [JsonProperty("xlsx")]
        public ProjectFileInfo Xlsx { get; set; }

        [JsonProperty("sync_status")]
        public string SyncStatus { get; set; }

        [JsonProperty("has_error")]
        public bool HasError { get; set; }

        [JsonProperty("current_error")]
        public string CurrentError { get; set; }

        [JsonProperty("last_sync")]
        public string LastSync { get; set; }
    }

    /// <summary>
    /// File information from the API
    /// </summary>
    public class ProjectFileInfo
    {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("exists")]
        public bool Exists { get; set; }

        [JsonProperty("last_modified")]
        public string LastModified { get; set; }
    }

    /// <summary>
    /// Response from list-transpilers auth operations
    /// </summary>
    public class AuthResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("plan")]
        public string Plan { get; set; }
    }
}
