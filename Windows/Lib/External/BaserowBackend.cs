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
            // Check if we have a valid token (expires after 60 minutes)
            if (!string.IsNullOrEmpty(_authToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _authToken;
            }

            // Request new JWT token
            using (var httpClient = new HttpClient())
            {
                var loginData = new { username = _username, password = _password };
                var json = JsonConvert.SerializeObject(loginData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                Console.WriteLine($"Requesting new BaseRow JWT Auth token for {_username}");
                var response = await httpClient.PostAsync($"{_baseUrl}/user/token-auth/", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to authenticate with Baserow: {response.StatusCode} - {responseContent}");
                }
                
                var authResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);
                _authToken = authResponse.token;
                _tokenExpiry = DateTime.UtcNow.AddMinutes(58); // Refresh 2 minutes before expiry
                
                return _authToken;
            }
        }

        public JToken GetAvailableBases()
        {
            var task = GetAvailableBasesAsync();
            task.Wait();
            return task.Result;
        }

        public async Task<JToken> GetAvailableBasesAsync()
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
                    if (app["type"]?.ToString() == "database") allBases.Add(app);
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

        public JToken GetField(string tableId, string rowId, string fieldId)
        {
            var task = GetFieldAsync(tableId, rowId, fieldId);
            task.Wait();
            return task.Result;
        }

        public async Task<JToken> GetFieldAsync(string tableId, string rowId, string fieldId)
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
        
        public JObject UpdateField(string tableId, string rowId, string fieldId, string newValue)
        {
            var task = UpdateFieldAsync(tableId, rowId, fieldId, newValue);
            task.Wait();
            return task.Result;
        }

        public async Task<JObject> UpdateFieldAsync(string tableId, string rowId, string fieldId, string newValue)
        {
            var token = await GetValidTokenAsync();
            
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"JWT {token}");
                
                Console.WriteLine($"Updating field: {fieldId} to: \"{newValue}\"");

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

        public async Task<JToken> CreateFieldAsync(string tableId, string fieldName, string fieldType)
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

        public async Task<JToken> GetTableSchemaAsync(string tableId, bool userReadable)
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