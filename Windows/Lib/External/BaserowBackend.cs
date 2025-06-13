using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSoTme.OST.Lib.SassySDK.Derived;
using System.Collections.Generic;

namespace SSoTme.OST.Core.Lib.External
{
    public class BaserowBackend
    {
        private string _baseUrl;
        private string _username;
        private string _password;
        private string _authToken;
        private string _refreshToken;
        private DateTime _tokenExpiry;

        public BaserowBackend(string username, string password)
        {
            _baseUrl = "https://api.baserow.io/api";
            _username = username;
            _password = password;
        }
            
        public BaserowBackend()
        {
            // base init -> return null client -> must manually run InitFromHomeFile()
            return;
        }

        public void InitFromHomeFile(string runAs = "")
        {
            var key = SSOTMEKey.GetSSoTmeKey(runAs);
            if (!key.APIKeys.ContainsKey("baserow"))
            {
                throw new Exception("Baserow credentials not found. Run: ssotme -setAccountAPIKey=baserow/username/password");
            }

            var baserowConfigJson = key.APIKeys["baserow"];
            var baserowConfig = JsonConvert.DeserializeObject<dynamic>(baserowConfigJson);
            
            _baseUrl = "https://api.baserow.io/api";
            _username = baserowConfig.username;
            _password = baserowConfig.password;
        }
        
        private async Task<string> GetValidTokenAsync()
        {
            if (!string.IsNullOrEmpty(_authToken) && DateTime.UtcNow < _tokenExpiry)
            {
                // we already have a valid token
                return _authToken;
            }

            string responseContent, endpoint;
            using var httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(_authToken) && DateTime.UtcNow >= _tokenExpiry)
            {
                // token expired -> refresh it
                Console.WriteLine($"Refreshing BaseRow JWT Auth token for {_username}");
                var tokenData = new { refresh_token = _refreshToken };
                var json = JsonConvert.SerializeObject(tokenData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                endpoint = "/user/token-refresh/";
                var response = await httpClient.PostAsync($"{_baseUrl}{endpoint}", content);
                responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to refresh Baserow token: {response.StatusCode} - {responseContent}");
                }
            }
            else {
                // request new token
                var loginData = new { email = _username, password = _password };
                var json = JsonConvert.SerializeObject(loginData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                Console.WriteLine($"Requesting new BaseRow JWT Auth token for {_username}");
                endpoint = "/user/token-auth/";
                var response = await httpClient.PostAsync($"{_baseUrl}{endpoint}", content);
                responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to authenticate with Baserow: {response.StatusCode} - {responseContent}");
                }
            }
            var authResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
            _authToken = authResponse.access_token;
            _tokenExpiry = DateTime.UtcNow.AddMinutes(9); // Refresh 1 minute before expiry
            if (endpoint == "/user/token-auth/")
            {
                // set the refresh token only if requesting a new token (valid for 168 hours)
                _refreshToken = authResponse.refresh_token;
            }
            return _authToken;
        }

        public JToken GetAvailableBases()
        {
            var task = GetAvailableBasesAsync();
            task.Wait();
            return task.Result;
        }

        private async Task<JToken> GetAvailableBasesAsync()
        {
            var token = await GetValidTokenAsync();
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");

            // 1. List workspaces
            var wsResponse = await httpClient.GetAsync($"{_baseUrl}/workspaces/");
            wsResponse.EnsureSuccessStatusCode();
            var wsJson = JToken.Parse(await wsResponse.Content.ReadAsStringAsync());
            var workspaces = wsJson as JArray;

            var allBases = new JArray();

            if (workspaces == null)
            {
                throw new Exception("You don't have any baserow workspaces! Please create one at https://baserow.io");
            }
            
            // 2. For each workspace, list bases
            foreach (var ws in workspaces)
            {
                var wsId = ws["id"]?.ToString();
                if (string.IsNullOrEmpty(wsId)) continue;
                var dbResponse = await httpClient.GetAsync($"{_baseUrl}/applications/workspace/{wsId}/");
                if (!dbResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to list bases for workspace {wsId}: {dbResponse.StatusCode}");
                    continue;
                }
                var wsResp = await dbResponse.Content.ReadAsStringAsync();
                var allApps = JToken.Parse(wsResp);
                foreach (var app in allApps)
                {
                    var trimmedInfo = new JObject
                    {
                        ["id"] = app["id"]
                    };
                    if (app["type"]?.ToString() == "database") allBases.Add(trimmedInfo);
                }
            }
    
            return allBases;
        }
        
        public JToken FetchTablesForBase(string baseId)
        {
            var task = FetchTablesAsync(baseId);
            task.Wait();
            return task.Result;
        }

        private async Task<JToken> FetchTablesAsync(string baseid)
        {
            var token = await GetValidTokenAsync();
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");
        
                var response = await httpClient.GetAsync($"{_baseUrl}/database/tables/database/{baseid}/");
        
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to fetch Baserow tables: {response.StatusCode} - {errorContent}");
                }

                string strResp = await response.Content.ReadAsStringAsync();
                try 
                {
                    return JToken.Parse(strResp);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON parsing error in FetchTablesAsync: {ex.Message}");
                    Console.WriteLine($"Response content (first 200 chars): {strResp.Substring(0, Math.Min(200, strResp.Length))}");
                    throw new Exception($"Failed to parse JSON response from Baserow: {ex.Message}");
                }
            }
        }

        public JToken GetCell(string tableId, string rowId, string fieldId)
        {
            var task = GetCellAsync(tableId, rowId, fieldId);
            task.Wait();
            return task.Result;
        }

        private async Task<JToken> GetCellAsync(string tableId, string rowId, string fieldId)
        {
            var token = await GetValidTokenAsync();
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");
                var response = await httpClient.GetAsync($"{_baseUrl}/database/rows/table/{tableId}/{rowId}/");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to fetch Baserow field: {response.StatusCode} - {errorContent}");
                }

                string strResp = await response.Content.ReadAsStringAsync();
                try 
                {
                    var json = JToken.Parse(strResp);
                    string field_key = $"field_{fieldId}";
                    if (!json.HasValues || json[field_key] == null)
                    {
                        throw new Exception($"Field '{field_key}' not found in row data.");
                    }
                    return new JObject{ ["content"] = json[field_key]}; // return only the requested field's value

                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON parsing error in GetFieldAsync: {ex.Message}");
                    Console.WriteLine($"Response content (first 200 chars): {strResp.Substring(0, Math.Min(200, strResp.Length))}");
                    throw new Exception($"Failed to parse JSON response from Baserow: {ex.Message}");
                }
            }
        }

        public JToken CreateRow(string tableId)
        {
            var task = CreateRowAsync(tableId);
            task.Wait();
            return task.Result;
        }

        private async Task<JToken> CreateRowAsync(string tableId)
        {
            var token = await GetValidTokenAsync(); // assume this gives you a valid JWT
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");

                var url = $"{_baseUrl}/database/rows/table/{tableId}/";
                var emptyPayload = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, emptyPayload);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to create row: {response.StatusCode} - {error}");
                }

                var content = await response.Content.ReadAsStringAsync();
                return JToken.Parse(content);
            }
        }

        public JToken MoveRow(string tableId, string rowId, string relRow = null)
        {
            var task = MoveRowAsync(tableId, rowId, relRow);
            task.Wait();
            return task.Result;
        }

        private async Task<JToken> MoveRowAsync(string tableId, string rowId, string relativeRowId)
        {
            var token = await GetValidTokenAsync();

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");
                
                JObject payload = new JObject
                {
                    ["table_id"] = tableId,
                    ["row_id"] = relativeRowId
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{_baseUrl}/database/rows/table/{tableId}/{rowId}/move/";
                var response = await httpClient.PatchAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to move Baserow row: {response.StatusCode} - {errorContent}");
                }

                string strResp = await response.Content.ReadAsStringAsync();
                try
                {
                    return JObject.Parse(strResp);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON parsing error in MoveRowAsync: {ex.Message}");
                    Console.WriteLine($"Response content (first 200 chars): {strResp.Substring(0, Math.Min(200, strResp.Length))}");
                    throw new Exception($"Failed to parse JSON response from Baserow: {ex.Message}");
                }
            }
        }

        public JToken DeleteField(string fieldId)
        {
            var task = DeleteFieldAsync(fieldId);
            task.Wait();
            return task.Result;
        }
        
        private async Task<JToken> DeleteFieldAsync(string fieldId)
        {
            var token = await GetValidTokenAsync();
            
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");
                
                var url = $"{_baseUrl}/database/fields/{fieldId}/";
                var response = await httpClient.DeleteAsync(url);
                
                Console.WriteLine($"Response: {response.StatusCode} - {response.ReasonPhrase}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to delete Baserow field: {response.StatusCode} - {errorContent}");
                }
                
                string strResp = await response.Content.ReadAsStringAsync();
                try
                {
                    return JToken.Parse(strResp);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON parsing error in DeleteFieldAsync: {ex.Message}");
                    Console.WriteLine($"Response content (first 200 chars): {strResp.Substring(0, Math.Min(200, strResp.Length))}");
                    throw new Exception($"Failed to parse JSON response from Baserow: {ex.Message}");
                }
            }
        }
        
        public JToken UpdateField(string fieldId, string newName, string newType)
        {
            var task = UpdateFieldAsync(fieldId, newName, newType);
            task.Wait();
            return task.Result;
        }

        private async Task<JToken> UpdateFieldAsync(string fieldId, string newName = null, string newType = null)
        {
            var token = await GetValidTokenAsync();

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");

                var fieldData = new JObject();
                if (!string.IsNullOrEmpty(newName)) fieldData["name"] = newName;
                if (!string.IsNullOrEmpty(newType)) fieldData["type"] = newType;

                if (!fieldData.HasValues)
                {
                    throw new ArgumentException("You must provide at least one field property to update.");
                }

                var json = JsonConvert.SerializeObject(fieldData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{_baseUrl}/database/fields/{fieldId}/";
                var response = await httpClient.PatchAsync(url, content);

                Console.WriteLine($"Response: {response.StatusCode} - {response.ReasonPhrase}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to update Baserow field: {response.StatusCode} - {errorContent}");
                }

                string strResp = await response.Content.ReadAsStringAsync();
                try
                {
                    return JToken.Parse(strResp);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON parsing error in UpdateFieldAsync: {ex.Message}");
                    Console.WriteLine($"Response content (first 200 chars): {strResp.Substring(0, Math.Min(200, strResp.Length))}");
                    throw new Exception($"Failed to parse JSON response from Baserow: {ex.Message}");
                }
            }
        }
        
        public JToken UpdateCell(string tableId, string rowId, string fieldId, string newValue)
        {
            var task = UpdateCellAsync(tableId, rowId, fieldId, newValue);
            task.Wait();
            return task.Result;
        }

        private async Task<JToken> UpdateCellAsync(string tableId, string rowId, string fieldId, string newValue)
        {
            var token = await GetValidTokenAsync();
            
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");
                
                // Console.WriteLine($"Updating cell: {fieldId} to: \"{newValue}\"");

                JObject valueJson = new JObject
                {
                    [$"field_{fieldId}"] = newValue
                };
                
                var json = JsonConvert.SerializeObject(valueJson);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PatchAsync($"{_baseUrl}/database/rows/table/{tableId}/{rowId}/", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to update Baserow field: {response.StatusCode} - {errorContent}");
                }
                string strResp = await response.Content.ReadAsStringAsync();
                try 
                {
                    return JObject.Parse(strResp);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON parsing error in UpdateFieldAsync: {ex.Message}");
                    Console.WriteLine($"Response content (first 200 chars): {strResp.Substring(0, Math.Min(200, strResp.Length))}");
                    throw new Exception($"Failed to parse JSON response from Baserow: {ex.Message}");
                }
            }
        }

        public JToken CreateField(string tableId, string fieldName, string fieldType)
        {
            var task = CreateFieldAsync(tableId, fieldName, fieldType);
            task.Wait();
            return task.Result;
        }

        private async Task<JToken> CreateFieldAsync(string tableId, string fieldName, string fieldType)
        {
            var token = await GetValidTokenAsync();
            
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");
                
                var fieldData = new { name = fieldName, type = fieldType };
                var json = JsonConvert.SerializeObject(fieldData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{_baseUrl}/database/fields/table/{tableId}/", content);
                Console.WriteLine($"Response: {response.StatusCode} - {response.ReasonPhrase}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to create Baserow field: {response.StatusCode} - {errorContent}");
                }
                string strResp = await response.Content.ReadAsStringAsync();
                try 
                {
                    return JToken.Parse(strResp);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON parsing error in CreateFieldAsync: {ex.Message}");
                    Console.WriteLine($"Response content (first 200 chars): {strResp.Substring(0, Math.Min(200, strResp.Length))}");
                    throw new Exception($"Failed to parse JSON response from Baserow: {ex.Message}");
                }
            }
        }

        public JToken GetTableSchema(string tableId)
        {
            var dataTask = GetTableSchemaAsync(tableId, false);
            dataTask.Wait();
            JToken tableData = dataTask.Result;
            
            var readableTask = GetTableSchemaAsync(tableId, true);
            readableTask.Wait();
            JToken readableData = readableTask.Result;
            
            // Transform the data to include field objects with columnName, value, and id
            return TransformTableDataWithFields(tableData, readableData);
        }

        private async Task<JToken> GetTableSchemaAsync(string tableId, bool userReadable)
        {
            var token = await GetValidTokenAsync();
            
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");
                var response = await httpClient.GetAsync($"{_baseUrl}/database/rows/table/{tableId}/?user_field_names={userReadable}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to get Baserow table schema: {response.StatusCode} - {errorContent}");
                }
                
                string strRes = await response.Content.ReadAsStringAsync();
                try 
                {
                    return JToken.Parse(strRes);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON parsing error in GetTableSchemaAsync: {ex.Message}");
                    Console.WriteLine($"Response content (first 200 chars): {strRes.Substring(0, Math.Min(200, strRes.Length))}");
                    throw new Exception($"Failed to parse JSON response from Baserow: {ex.Message}");
                }
            }
        }
        
        private JToken TransformTableDataWithFields(JToken rawData, JToken readableData)
        {
            var transformedRows = new JArray();

            if (rawData != null && rawData["results"] != null &&
                readableData != null && readableData["results"] != null)
            {
                var rawRows = rawData["results"].ToObject<JArray>();
                var readableRows = readableData["results"].ToObject<JArray>();

                // Create a dictionary of readable rows indexed by ID for quick lookup
                var readableRowsById = new Dictionary<string, JToken>();
                foreach (var readableRow in readableRows)
                {
                    var id = readableRow["id"]?.ToString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        readableRowsById[id] = readableRow;
                    }
                }

                foreach (var rawRow in rawRows)
                {
                    var transformedRow = new JObject();
                    var rowId = rawRow["id"]?.ToString();

                    if (!string.IsNullOrEmpty(rowId))
                    {
                        transformedRow["rowId"] = rowId;
                    }

                    // Get corresponding readable row
                    if (!string.IsNullOrEmpty(rowId) && readableRowsById.ContainsKey(rowId))
                    {
                        var readableRow = readableRowsById[rowId];
                        var rawProperties = rawRow.ToObject<JObject>().Properties();

                        int cellIndex = 0;
                        foreach (var property in rawProperties)
                        {
                            if (property.Name.StartsWith("field_"))
                            {
                                string fieldIdStr = property.Name.Substring(6);
                                if (int.TryParse(fieldIdStr, out int fieldId))
                                {
                                    var fieldValue = property.Value;
                                    var fieldName = FindColumnNameForField(readableRow, fieldValue?.ToString());

                                    var cell = new JObject
                                    {
                                        ["fieldName"] = fieldName ?? "Unknown",
                                        ["fieldId"] = fieldId,
                                        ["value"] = fieldValue
                                    };

                                    transformedRow[$"cell{cellIndex}"] = cell;
                                    cellIndex++;
                                }
                                else
                                {
                                    Console.WriteLine($"Warning: Could not parse field ID from property name: '{property.Name}', field ID string: '{fieldIdStr}'");

                                    var cell = new JObject
                                    {
                                        ["fieldName"] = "Unknown",
                                        ["fieldId"] = -1,
                                        ["value"] = property.Value
                                    };

                                    transformedRow[$"cell{cellIndex}"] = cell;
                                    cellIndex++;
                                }
                            }
                        }
                    }

                    transformedRows.Add(transformedRow);
                }
            }

            return new JObject
            {
                ["results"] = transformedRows
            };
        }

        
        private string FindColumnNameForField(JToken readableRow, string fieldValue)
        {
            // Find which column in the readable row has the same value as the field
            foreach (var property in readableRow.ToObject<JObject>().Properties())
            {
                if (property.Name != "id" && property.Name != "order" && 
                    string.Equals(property.Value?.ToString(), fieldValue, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Name;
                }
            }
            return null;
        }
    }
}