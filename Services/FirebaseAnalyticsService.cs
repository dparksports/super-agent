using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.Storage;

namespace OpenClaw.Windows.Services
{
    public class FirebaseAnalyticsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FirebaseAnalyticsService> _logger;
        private string? _measurementId;
        private string? _apiSecret;
        private string _clientId;

        private const string BaseUrl = "https://www.google-analytics.com/mp/collect";

        public FirebaseAnalyticsService(ILogger<FirebaseAnalyticsService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _clientId = GetOrCreateClientId();
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                var configFile = System.IO.Path.Combine(AppContext.BaseDirectory, "firebase_config.json");
                if (System.IO.File.Exists(configFile))
                {
                    var json = System.IO.File.ReadAllText(configFile);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("measurementId", out var mId))
                    {
                        _measurementId = mId.GetString();
                    }
                    if (root.TryGetProperty("apiSecret", out var secret))
                    {
                        _apiSecret = secret.GetString();
                    }
                }
                else
                {
                    _logger.LogWarning("firebase_config.json not found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Firebase configuration.");
            }
        }

        private string GetOrCreateClientId()
        {
            var id = SettingsHelper.Get<string>("FirebaseClientId", null!);
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }

            var newId = Guid.NewGuid().ToString();
            SettingsHelper.Set("FirebaseClientId", newId);
            return newId;
        }

        public async Task LogEventAsync(string eventName, object? parsedParams = null)
        {
            // Check Opt-out
            var enabled = SettingsHelper.Get<bool>("AnalyticsEnabled", true);
            if (!enabled) return;

            if (string.IsNullOrEmpty(_measurementId) || string.IsNullOrEmpty(_apiSecret))
            {
                // Config missing, skip
                return;
            }

            try
            {
                var payload = new
                {
                    client_id = _clientId,
                    events = new[]
                    {
                        new
                        {
                            name = eventName,
                            @params = parsedParams ?? new { }
                        }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var url = $"{BaseUrl}?measurement_id={_measurementId}&api_secret={_apiSecret}";

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to send analytics event {eventName}. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending analytics event {eventName}");
            }
        }
    }
}
