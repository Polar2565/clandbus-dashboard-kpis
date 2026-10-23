using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using ClandbusERPIntegration.Configurations;
using ClandbusERPIntegration.DTOs;
using ClandbusERPIntegration.Interfaces;
using Microsoft.Extensions.Options;

using System.Net.Http.Headers;

namespace ClandbusERPIntegration.Services
{
    public class AcumaticaService : IAcumaticaService
    {
        private readonly AcumaticaSettings _settings;

        private readonly HttpClient _httpClient;

        private bool _isLoggedIn = false;

        private LoginRequestDto? _currentSession;

        public bool IsLoggedIn => _isLoggedIn;
        public string CurrentUsername { get; private set; } = string.Empty;
        public string CurrentDisplayName { get; private set; } = string.Empty;
        public string CurrentOwnerId { get; private set; } = string.Empty;

        public AcumaticaService(
            HttpClient httpClient,
            IOptions<AcumaticaSettings> settings)
        {
            _settings = settings.Value;

            _httpClient = httpClient;

            _httpClient.BaseAddress =
                new Uri(_settings.BaseUrl);

            _httpClient.DefaultRequestHeaders.Clear();

            _httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "PostmanRuntime/7.43.0");

            _httpClient.DefaultRequestHeaders.Add(
                "Accept",
                "*/*");
        }

        public async Task<bool> LoginAsync(
            LoginRequestDto loginRequest)
        {
            if (_isLoggedIn)
            {
                if (string.Equals(CurrentUsername, loginRequest.Username.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                await LogoutAsync();
            }

            var loginData = new
            {
                name = loginRequest.Username,
                password = loginRequest.Password,
                tenant = _settings.Company,
                branch = _settings.Branch
            };

            var json =
                JsonSerializer.Serialize(
                    loginData);

            var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    "entity/auth/login");

            request.Version =
                HttpVersion.Version11;

            request.Content = content;

            var response =
                await _httpClient.SendAsync(
                    request);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            _currentSession =
                loginRequest;

            _isLoggedIn = true;

            CurrentUsername = loginRequest.Username.Trim();
            CurrentDisplayName = DisplayNameFromUsername(CurrentUsername);
            await ResolveCurrentEmployeeAsync();

            return true;
        }

        public async Task<List<SalesOrderDto>>
            GetLastSalesOrdersAsync()
        {
            if (!_isLoggedIn ||
                _currentSession == null)
            {
                return new List<SalesOrderDto>();
            }

            var endpoint =
                "entity/Default/24.200.001/SalesOrder";

            var response =
                await _httpClient.GetAsync(
                    endpoint);

            var json =
                await response.Content
                    .ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new List<SalesOrderDto>();
            }

            var options =
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

            var orders =
                JsonSerializer.Deserialize
                <List<SalesOrderDto>>(
                    json,
                    options);

            return orders ??
                new List<SalesOrderDto>();
        }

        public async Task<IReadOnlyList<JsonElement>> GetCasesAsync()
        {
            if (_settings.PreferDefaultCaseEndpoint)
            {
                try
                {
                    return await GetDefaultCasesAsync(includeAllOwners: false);
                }
                catch (HttpRequestException)
                {
                    // Keep the published GI as a compatibility fallback while the
                    // 26.200.001 contract is validated in each Acumatica tenant.
                }
            }

            var ownerId = CurrentOwnerId;
            if (string.IsNullOrWhiteSpace(ownerId)
                && CurrentDisplayName.Equals(_settings.OwnerName, StringComparison.OrdinalIgnoreCase))
            {
                ownerId = _settings.OwnerId;
            }

            if (string.IsNullOrWhiteSpace(ownerId))
            {
                throw new InvalidOperationException(
                    "Acumatica did not expose the Owner/Contact ID for the authenticated user.");
            }

            var tenant = Uri.EscapeDataString(_settings.Company);
            var giEndpoint = $"t/{tenant}/api/odata/gi/CR-Cases2018R1";
            var sample = await GetRowsAsync(
                $"{giEndpoint}?$top=1",
                useBasicAuthentication: true);
            if (sample.Count == 0) return Array.Empty<JsonElement>();

            var ownerProperty = FindOwnerProperty(sample[0]);
            if (ownerProperty is null)
            {
                var available = string.Join(", ", sample[0].EnumerateObject().Select(x => x.Name));
                throw new InvalidOperationException(
                    $"The cases GI does not expose an Owner field. Available fields: {available}");
            }

            var ownerLiteral = ownerProperty.Value.Value.ValueKind == JsonValueKind.Number
                ? ownerId
                : $"'{ownerId.Replace("'", "''")}'";
            var expression = $"{ownerProperty.Value.Name} eq {ownerLiteral}";
            var filter = Uri.EscapeDataString(expression);
            var rows = await GetRowsAsync(
                $"{giEndpoint}?$filter={filter}&$top=1000",
                useBasicAuthentication: true);
            return rows.Where(IsCurrentOwner).ToArray();
        }

        public async Task<IReadOnlyList<JsonElement>> GetAllCasesAsync()
        {
            if (!_isLoggedIn) throw new InvalidOperationException("ERP session is not active.");
            var tenant = Uri.EscapeDataString(_settings.Company);
            IReadOnlyList<JsonElement> cases;
            if (_settings.PreferDefaultCaseEndpoint)
            {
                try
                {
                    cases = await GetDefaultCasesAsync(includeAllOwners: true);
                }
                catch (HttpRequestException)
                {
                    cases = await GetAllRowsAsync(
                        $"t/{tenant}/api/odata/gi/CR-Cases2018R1",
                        useBasicAuthentication: true);
                }
            }
            else
            {
                cases = await GetAllRowsAsync(
                    $"t/{tenant}/api/odata/gi/CR-Cases2018R1",
                    useBasicAuthentication: true);
            }
            var ownerNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in _settings.OwnerMappings)
                ownerNames[NormalizeIdentifier(mapping.Key)] = mapping.Value;
            if (!string.IsNullOrWhiteSpace(_settings.OwnerId) && !string.IsNullOrWhiteSpace(_settings.OwnerName))
                ownerNames[NormalizeIdentifier(_settings.OwnerId)] = _settings.OwnerName;
            if (!string.IsNullOrWhiteSpace(CurrentOwnerId) && !string.IsNullOrWhiteSpace(CurrentDisplayName))
                ownerNames[NormalizeIdentifier(CurrentOwnerId)] = CurrentDisplayName;
            foreach (var entity in new[] { "Employee", "Contact", "User" })
            {
                try
                {
                    foreach (var person in await GetEntityRowsAsync(entity))
                    {
                        var name = PersonName(person);
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        foreach (var id in PersonIdentifiers(person)) ownerNames.TryAdd(id, name);
                    }
                }
                catch (HttpRequestException)
                {
                    // Endpoint availability varies by Acumatica tenant.
                }
            }

            return cases.Select(row =>
            {
                var owner = FindOwnerProperty(row);
                var ownerId = owner.HasValue ? NormalizeIdentifier(ScalarValue(owner.Value.Value)) : string.Empty;
                var objectNode = JsonNode.Parse(row.GetRawText())?.AsObject();
                if (objectNode is null) return row;
                var endpointOwnerName = Value(row, "OwnerEmployeeName");
                if (!string.IsNullOrWhiteSpace(endpointOwnerName))
                    objectNode["OwnerDisplayName"] = endpointOwnerName;
                else if (ownerNames.TryGetValue(ownerId, out var ownerName))
                    objectNode["OwnerDisplayName"] = ownerName;
                return JsonSerializer.Deserialize<JsonElement>(objectNode.ToJsonString());
            }).ToArray();
        }

        private async Task<IReadOnlyList<JsonElement>> GetDefaultCasesAsync(bool includeAllOwners)
        {
            const string fields = "CaseID,Subject,BusinessAccount,BusinessAccountName,ClassID,Status,Reason,Owner,OwnerEmployeeName,DateReported,LastIncomingActivity,LastOutgoingActivity,ClosingDate,LastModifiedDateTime,NoteID";
            var endpoint = $"entity/Default/{_settings.EndpointVersion}/Case?$select={fields}";
            IReadOnlyList<JsonElement> rows;

            if (includeAllOwners)
            {
                rows = await GetAllRowsAsync(endpoint, useBasicAuthentication: false, pageSize: 500);
            }
            else
            {
                var collected = new List<JsonElement>();
                foreach (var property in new[] { "Owner", "OwnerEmployeeName" })
                {
                    foreach (var owner in OwnerFilterValues())
                    {
                        try
                        {
                            var expression = $"{property} eq '{owner.Replace("'", "''")}'";
                            var filter = Uri.EscapeDataString(expression);
                            collected.AddRange(await GetAllRowsAsync(
                                $"{endpoint}&$filter={filter}",
                                useBasicAuthentication: false,
                                pageSize: 500));
                        }
                        catch (HttpRequestException)
                        {
                            // Different Acumatica publications expose different
                            // owner representations; try the next supported form.
                        }
                    }
                }

                if (collected.Count == 0)
                {
                    var allRows = await GetAllRowsAsync(endpoint, useBasicAuthentication: false, pageSize: 500);
                    collected.AddRange(allRows.Where(IsCurrentOwner));
                }

                rows = collected
                    .GroupBy(row => Value(row, "NoteID", "CaseID", "id"))
                    .Select(group => group.First())
                    .ToArray();
            }

            return rows.Select(row =>
            {
                var objectNode = JsonNode.Parse(row.GetRawText())?.AsObject();
                if (objectNode is null) return row;
                var ownerName = Value(row, "OwnerEmployeeName", "Owner");
                if (!string.IsNullOrWhiteSpace(ownerName)) objectNode["OwnerDisplayName"] = ownerName;
                return JsonSerializer.Deserialize<JsonElement>(objectNode.ToJsonString());
            }).ToArray();
        }

        private async Task<IReadOnlyList<JsonElement>> GetAllRowsAsync(
            string endpoint,
            bool useBasicAuthentication,
            int pageSize = 1000)
        {
            var rows = new List<JsonElement>();
            for (var skip = 0; skip < 50_000; skip += pageSize)
            {
                var separator = endpoint.Contains('?') ? '&' : '?';
                var page = await GetRowsAsync(
                    $"{endpoint}{separator}$top={pageSize}&$skip={skip}",
                    useBasicAuthentication);
                rows.AddRange(page);
                if (page.Count < pageSize) break;
            }
            return rows;
        }

        public async Task<IReadOnlyList<JsonElement>> GetTasksAsync()
        {
            if (_settings.PreferDefaultTaskEndpoint)
                return await GetDefaultTasksAsync(includeAllOwners: false);

            throw new HttpRequestException("El endpoint Default de tareas está desactivado; se conserva la captura importada.");
        }

        public async Task<IReadOnlyList<JsonElement>> GetAllTasksAsync()
        {
            if (_settings.PreferDefaultTaskEndpoint)
                return await GetDefaultTasksAsync(includeAllOwners: true);

            throw new HttpRequestException("El endpoint Default de tareas está desactivado; se conserva la captura importada.");
        }

        public async Task<IReadOnlyList<JsonElement>> GetEmployeesAsync()
        {
            if (!_settings.PreferEmployeeDirectory) return Array.Empty<JsonElement>();
            return await GetEmployeeDirectoryAsync();
        }

        private async Task<IReadOnlyList<JsonElement>> GetEmployeeDirectoryAsync()
        {
            const string fields = "EmployeeID,EmployeeName,DisplayName,DepartmentID,Status,CreatedDateTime,LastModifiedDateTime";
            var rows = await GetAllRowsAsync(
                $"entity/Default/{_settings.EndpointVersion}/Employee?$select={fields}",
                useBasicAuthentication: false,
                pageSize: 500);
            return rows.Where(row =>
            {
                var status = Value(row, "Status");
                return string.IsNullOrWhiteSpace(status)
                    || status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("Activo", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("A", StringComparison.OrdinalIgnoreCase);
            }).ToArray();
        }

        private async Task<IReadOnlyList<JsonElement>> GetDefaultTasksAsync(bool includeAllOwners)
        {
            const string fields = "NoteID,Summary,Status,CompletionPercentage,Category,Owner,OwnerID_description,RelatedEntityDescription,RelatedEntityNoteID,StartDate,DueDate,CompletedAt,Priority,CreatedDateTime,LastModifiedDateTime";
            var endpoint = $"entity/Default/{_settings.EndpointVersion}/Task?$select={fields}";
            if (includeAllOwners)
            {
                var all = await GetAllRowsAsync(endpoint, useBasicAuthentication: false, pageSize: 500);
                return await AddOwnerDisplayNamesAsync(all);
            }

            var collected = new List<JsonElement>();
            foreach (var owner in OwnerFilterValues())
            {
                foreach (var property in new[] { "Owner", "OwnerID_description" })
                {
                    try
                    {
                        var expression = $"{property} eq '{owner.Replace("'", "''")}'";
                        var filter = Uri.EscapeDataString(expression);
                        collected.AddRange(await GetAllRowsAsync(
                            $"{endpoint}&$filter={filter}",
                            useBasicAuthentication: false,
                            pageSize: 500));
                    }
                    catch (HttpRequestException)
                    {
                        // Owner fields differ between endpoint publications.
                    }
                }
            }

            if (collected.Count == 0)
            {
                var all = await GetAllRowsAsync(endpoint, useBasicAuthentication: false, pageSize: 500);
                collected.AddRange(all.Where(IsCurrentOwner));
            }

            var rows = collected
                .GroupBy(row =>
                {
                    var key = Value(row, "NoteID", "TaskID", "id", "ID");
                    return string.IsNullOrWhiteSpace(key) ? row.GetRawText() : key;
                })
                .Select(group => group.First())
                .ToArray();
            return await AddOwnerDisplayNamesAsync(rows);
        }

        private async Task<IReadOnlyList<JsonElement>> AddOwnerDisplayNamesAsync(IEnumerable<JsonElement> rows)
        {
            var directory = (await GetEmployeeDirectoryAsync())
                .Select(employee => new
                {
                    Id = NormalizeIdentifier(Value(employee, "EmployeeID")),
                    Name = Value(employee, "EmployeeName", "DisplayName")
                })
                .Where(employee => !string.IsNullOrWhiteSpace(employee.Id) && !string.IsNullOrWhiteSpace(employee.Name))
                .GroupBy(employee => employee.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in _settings.OwnerMappings)
                directory[NormalizeIdentifier(mapping.Key)] = mapping.Value;
            return rows.Select(row =>
            {
                var objectNode = JsonNode.Parse(row.GetRawText())?.AsObject();
                if (objectNode is null) return row;
                var ownerId = NormalizeIdentifier(Value(row, "OwnerID_description", "Owner"));
                var ownerName = Value(row, "OwnerID_description");
                if ((string.IsNullOrWhiteSpace(ownerName) || ownerName.Equals(ownerId, StringComparison.OrdinalIgnoreCase))
                    && directory.TryGetValue(ownerId, out var employeeName)) ownerName = employeeName;
                if (string.IsNullOrWhiteSpace(ownerName)) ownerName = ownerId;
                if (!string.IsNullOrWhiteSpace(ownerName)) objectNode["OwnerDisplayName"] = ownerName;
                return JsonSerializer.Deserialize<JsonElement>(objectNode.ToJsonString());
            }).ToArray();
        }

        private async Task<IReadOnlyList<JsonElement>> GetEntityRowsAsync(string entity)
        {
            if (!_isLoggedIn)
            {
                throw new InvalidOperationException("ERP session is not active.");
            }

            var endpoint = $"entity/Default/{_settings.EndpointVersion}/{entity}?$top=1000";
            return await GetRowsAsync(endpoint);
        }

        private async Task<IReadOnlyList<JsonElement>> GetRowsAsync(
            string endpoint,
            bool useBasicAuthentication = false)
        {
            if (!_isLoggedIn) throw new InvalidOperationException("ERP session is not active.");
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (useBasicAuthentication && _currentSession is not null)
            {
                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{_currentSession.Username}:{_currentSession.Password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            }
            using var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var detail = json.Length > 600 ? json[..600] : json;
                throw new HttpRequestException(
                    $"Acumatica query '{endpoint}' failed with {(int)response.StatusCode}: {detail}");
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var value)) root = value;
            if (root.ValueKind != JsonValueKind.Array) return Array.Empty<JsonElement>();

            return root
                .EnumerateArray()
                .Select(row => row.Clone())
                .ToArray();
        }

        private bool IsCurrentOwner(JsonElement row)
        {
            var owner = string.Join(' ', row.EnumerateObject()
                .Where(property => property.Name.Contains("owner", StringComparison.OrdinalIgnoreCase))
                .Select(property => ScalarValue(property.Value)));
            if (!string.IsNullOrWhiteSpace(CurrentOwnerId)
                && owner.Contains(CurrentOwnerId, StringComparison.OrdinalIgnoreCase)) return true;
            if (owner.Contains(CurrentDisplayName, StringComparison.OrdinalIgnoreCase)) return true;
            if (owner.Contains(CurrentUsername, StringComparison.OrdinalIgnoreCase)) return true;
            var login = CurrentUsername.Split('@')[0];
            if (!string.IsNullOrWhiteSpace(login)
                && owner.Contains(login, StringComparison.OrdinalIgnoreCase)) return true;
            var parts = CurrentDisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1
                && parts.All(part => owner.Contains(part, StringComparison.OrdinalIgnoreCase));
        }

        private static string ScalarValue(JsonElement property)
        {
            while (property.ValueKind == JsonValueKind.Object)
            {
                if (property.TryGetProperty("value", out var wrapped))
                {
                    property = wrapped;
                    continue;
                }
                if (property.TryGetProperty("description", out var description))
                    return description.GetString() ?? description.ToString();
                if (property.TryGetProperty("id", out var id))
                    return id.GetString() ?? id.ToString();
                return string.Empty;
            }
            return property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : property.ToString();
        }

        private static JsonProperty? FindOwnerProperty(JsonElement row)
        {
            var candidates = row.EnumerateObject()
                .Where(property => property.Name.Contains("owner", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return candidates
                .OrderBy(property =>
                    property.Name.Contains("description", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("name", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(property => property.Name.Length)
                .Cast<JsonProperty?>()
                .FirstOrDefault();
        }

        private IEnumerable<string> OwnerFilterValues()
        {
            var login = CurrentUsername.Split('@')[0];
            return new[] { CurrentOwnerId, CurrentDisplayName, CurrentUsername, login }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsTaskActivity(JsonElement row)
        {
            var type = Value(row, "Type", "ClassID", "ActivityType", "Class");
            return string.IsNullOrWhiteSpace(type)
                || type.Equals("0", StringComparison.OrdinalIgnoreCase)
                || type.Equals("T", StringComparison.OrdinalIgnoreCase)
                || type.Contains("Task", StringComparison.OrdinalIgnoreCase)
                || type.Contains("Tarea", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Boolean(JsonElement row, params string[] names)
        {
            var raw = Value(row, names);
            return bool.TryParse(raw, out var value) && value;
        }

        private async Task ResolveCurrentEmployeeAsync()
        {
            try
            {
                var employees = await GetEntityRowsAsync("Employee");
                var login = CurrentUsername.ToLowerInvariant();
                var local = login.Split('@')[0];
                foreach (var row in employees)
                {
                    var identities = row.EnumerateObject()
                        .Where(property => property.Name.Contains("mail", StringComparison.OrdinalIgnoreCase)
                            || property.Name.Contains("login", StringComparison.OrdinalIgnoreCase)
                            || property.Name.Contains("user", StringComparison.OrdinalIgnoreCase))
                        .Select(property => ScalarValue(property.Value).ToLowerInvariant())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToArray();
                    if (!identities.Any(value => value == login || value == local || value.StartsWith(local + "@"))) continue;
                    CurrentOwnerId = PersonIdentifiers(row).FirstOrDefault() ?? string.Empty;
                    var name = PersonName(row);
                    if (!string.IsNullOrWhiteSpace(name)) CurrentDisplayName = name;
                    break;
                }
            }
            catch
            {
                // Some tenants do not expose Employee. The name derived from the
                // authenticated username remains a safe filtering fallback.
            }
        }

        private static string DisplayNameFromUsername(string username)
        {
            var local = username.Split('@')[0];
            var words = local.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', words.Select(word => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word.ToLowerInvariant())));
        }

        private static string Value(JsonElement row, params string[] names)
        {
            foreach (var name in names)
            {
                if (!row.TryGetProperty(name, out var property))
                {
                    var expected = NormalizeFieldName(name);
                    var match = row.EnumerateObject()
                        .Where(candidate => NormalizeFieldName(candidate.Name)
                            .Equals(expected, StringComparison.OrdinalIgnoreCase))
                        .Cast<JsonProperty?>()
                        .FirstOrDefault();
                    if (!match.HasValue) continue;
                    property = match.Value.Value;
                }
                return ScalarValue(property);
            }
            return string.Empty;
        }

        private static string NormalizeFieldName(string value) =>
            new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        private static string PersonName(JsonElement row)
        {
            var preferred = Value(row, "EmployeeName", "DisplayName", "FullName", "ContactID_description", "EmployeeID_description", "AcctName");
            if (!string.IsNullOrWhiteSpace(preferred)) return preferred;
            return row.EnumerateObject()
                .Where(property => property.Name.Contains("name", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("description", StringComparison.OrdinalIgnoreCase))
                .Select(property => ScalarValue(property.Value).Trim())
                .FirstOrDefault(value => value.Any(char.IsLetter) && value.Contains(' ')) ?? string.Empty;
        }

        private static IEnumerable<string> PersonIdentifiers(JsonElement row) => row.EnumerateObject()
            .Where(property => property.Name.Contains("id", StringComparison.OrdinalIgnoreCase))
            .Select(property => NormalizeIdentifier(ScalarValue(property.Value)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        private static string NormalizeIdentifier(string value)
        {
            value = value.Trim();
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? decimal.Truncate(number).ToString(CultureInfo.InvariantCulture)
                : value;
        }

        public async Task<bool> UpdateOrderAsync(
            UpdateOrderDto request)
        {
            if (!_isLoggedIn)
            {
                return false;
            }

            var updateBody = new
            {
                orderType = new
                {
                    value = request.OrderType
                },

                orderNbr = new
                {
                    value = request.OrderNbr
                },

                description = new
                {
                    value = request.Description
                }
            };

            var json =
                JsonSerializer.Serialize(
                    updateBody);

            var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var endpoint =
                "entity/Default/24.200.001/SalesOrder";

            var response =
                await _httpClient.PutAsync(
                    endpoint,
                    content);

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RemoveHoldAsync(
            RemoveHoldDto request)
        {
            if (!_isLoggedIn)
            {
                return false;
            }

            var updateBody = new
            {
                orderType = new
                {
                    value = request.OrderType
                },

                orderNbr = new
                {
                    value = request.OrderNbr
                },

                hold = new
                {
                    value = false
                }
            };

            var json =
                JsonSerializer.Serialize(
                    updateBody);

            var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            var endpoint =
                "entity/Default/24.200.001/SalesOrder";

            var response =
                await _httpClient.PutAsync(
                    endpoint,
                    content);

            return response.IsSuccessStatusCode;
        }

        public async Task LogoutAsync()
        {
            await _httpClient.PostAsync(
                "entity/auth/logout",
                null);

            _isLoggedIn = false;

            _currentSession = null;
            CurrentUsername = string.Empty;
            CurrentDisplayName = string.Empty;
            CurrentOwnerId = string.Empty;

        }

        public void Dispose() => _httpClient.Dispose();
    }
}
