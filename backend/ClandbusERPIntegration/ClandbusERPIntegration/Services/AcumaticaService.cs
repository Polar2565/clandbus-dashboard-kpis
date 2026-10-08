using System.Net;
using System.Text;
using System.Text.Json;
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

        public async Task<IReadOnlyList<JsonElement>> GetTasksAsync()
        {
            // EP4040PL is backed by CRActivity records whose class is Task.
            // Depending on the published endpoint, those records can be exposed
            // through Task or through the broader Activity entity.
            var collected = new List<JsonElement>();
            foreach (var entity in new[] { "Task", "Activity" })
            {
                var filters = OwnerFilterValues()
                    .Select(owner => $"Owner eq '{owner.Replace("'", "''")}'")
                    .Append("WorkgroupIsMine eq true");
                foreach (var expression in filters)
                {
                    try
                    {
                        var filter = Uri.EscapeDataString(expression);
                        var rows = await GetRowsAsync(
                            $"entity/Default/{_settings.EndpointVersion}/{entity}?$filter={filter}&$top=1000");
                        var owned = rows.Where(row =>
                            IsCurrentOwner(row) || Boolean(row, "WorkgroupIsMine"));
                        if (entity == "Activity") owned = owned.Where(IsTaskActivity);
                        collected.AddRange(owned);
                    }
                    catch (HttpRequestException)
                    {
                        // Try the next identifier/entity exposed by this tenant.
                    }
                }
            }

            return collected
                .GroupBy(row =>
                {
                    var key = Value(row, "NoteID", "TaskID", "id", "ID");
                    return string.IsNullOrWhiteSpace(key) ? row.GetRawText() : key;
                })
                .Select(group => group.First())
                .ToArray();
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
            if (property.ValueKind == JsonValueKind.Object
                && property.TryGetProperty("value", out var wrapped)) property = wrapped;
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
                    var email = Value(row, "Email", "EmployeeEmail", "UserLogin", "LoginName").ToLowerInvariant();
                    if (email != login && !email.StartsWith(local + "@") && email != local) continue;
                    CurrentOwnerId = Value(row, "ContactID", "OwnerID", "BAccountID", "EmployeeID");
                    var name = Value(row, "EmployeeName", "DisplayName", "EmployeeID_description", "AcctName");
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
                if (!row.TryGetProperty(name, out var property)) continue;
                if (property.ValueKind == JsonValueKind.Object && property.TryGetProperty("value", out var wrapped)) property = wrapped;
                return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString();
            }
            return string.Empty;
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
