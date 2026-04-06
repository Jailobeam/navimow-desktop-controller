using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace NavimowDesktopController
{
    internal sealed class NavimowApiClient
    {
        public const string ApiBaseUrl = "https://navimow-fra.ninebot.com";
        public const string LoginUrl = "https://navimow-h5-fra.willand.com/smartHome/login?channel=homeassistant&client_id=homeassistant&response_type=code&redirect_uri=http%3A%2F%2Flocalhost%3A1%2Fcallback";
        public const string ClientId = "homeassistant";
        public const string ClientSecret = "57056e15-722e-42be-bbaa-b0cbfb208a52";
        public const string RedirectUri = "http://localhost:1/callback";

        private readonly HttpClient httpClient;

        public NavimowApiClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            this.httpClient = new HttpClient();
            this.httpClient.Timeout = TimeSpan.FromSeconds(30);
            this.httpClient.BaseAddress = new Uri(ApiBaseUrl);
        }

        public static string ExtractAuthorizationCode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var value = input.Trim();
            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri))
            {
                var query = uri.Query;
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var parts = query.TrimStart('?').Split('&');
                    foreach (var part in parts)
                    {
                        var keyValue = part.Split(new[] { '=' }, 2);
                        if (keyValue.Length == 2 && string.Equals(keyValue[0], "code", StringComparison.OrdinalIgnoreCase))
                        {
                            return Uri.UnescapeDataString(keyValue[1]);
                        }
                    }
                }
            }

            return value;
        }

        public async Task<SessionData> ExchangeCodeForTokenAsync(string input)
        {
            var code = ExtractAuthorizationCode(input);
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("redirect_uri", RedirectUri),
            });

            return await this.SendTokenRequestAsync(content).ConfigureAwait(false);
        }

        public async Task<SessionData> RefreshTokenAsync(string refreshToken)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
            });

            return await this.SendTokenRequestAsync(content).ConfigureAwait(false);
        }

        public async Task<List<NavimowDevice>> GetDevicesAsync(string accessToken)
        {
            var response = await this.SendAuthorizedAsync(HttpMethod.Get, "/openapi/smarthome/authList", accessToken, null).ConfigureAwait(false);
            EnsureSuccess(response);

            var payload = JsonUtils.GetObject(JsonUtils.GetObject(response, "data"), "payload");
            var deviceItems = JsonUtils.GetArray(payload, "devices");
            var devices = new List<NavimowDevice>();

            foreach (var item in deviceItems)
            {
                var device = item as Dictionary<string, object>;
                if (device == null)
                {
                    continue;
                }

                var id = JsonUtils.GetString(device, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                devices.Add(new NavimowDevice
                {
                    Id = id,
                    Name = JsonUtils.GetString(device, "name"),
                    Raw = device,
                });
            }

            return devices;
        }

        public async Task<Dictionary<string, object>> GetDeviceStatusAsync(string accessToken, string deviceId)
        {
            var payload = new
            {
                devices = new[]
                {
                    new { id = deviceId }
                }
            };

            var response = await this.SendAuthorizedAsync(HttpMethod.Post, "/openapi/smarthome/getVehicleStatus", accessToken, payload).ConfigureAwait(false);
            EnsureSuccess(response);

            var wrapper = JsonUtils.GetObject(JsonUtils.GetObject(response, "data"), "payload");
            var devices = JsonUtils.GetArray(wrapper, "devices");
            if (devices.Length == 0)
            {
                return new Dictionary<string, object>();
            }

            var first = devices[0] as Dictionary<string, object>;
            return first ?? new Dictionary<string, object>();
        }

        public async Task<MqttConnectionInfo> GetMqttInfoAsync(string accessToken)
        {
            var response = await this.SendAuthorizedAsync(HttpMethod.Get, "/openapi/mqtt/userInfo/get/v2", accessToken, null).ConfigureAwait(false);
            EnsureSuccess(response);

            var data = JsonUtils.GetObject(response, "data");
            return new MqttConnectionInfo
            {
                MqttHost = JsonUtils.GetString(data, "mqttHost"),
                MqttUrl = JsonUtils.GetString(data, "mqttUrl"),
                UserName = JsonUtils.GetString(data, "userName"),
                Password = JsonUtils.GetString(data, "pwdInfo"),
            };
        }

        public async Task<string> SendCommandAsync(string accessToken, string deviceId, string commandName)
        {
            var execution = BuildExecution(commandName);
            var payload = new
            {
                commands = new[]
                {
                    new
                    {
                        devices = new[]
                        {
                            new { id = deviceId }
                        },
                        execution = execution,
                    }
                }
            };

            var response = await this.SendAuthorizedAsync(HttpMethod.Post, "/openapi/smarthome/sendCommands", accessToken, payload).ConfigureAwait(false);
            EnsureSuccess(response);
            return JsonUtils.ToJson(response);
        }

        public async Task<SessionData> EnsureValidSessionAsync(SessionData currentSession)
        {
            if (currentSession == null || string.IsNullOrWhiteSpace(currentSession.AccessToken))
            {
                return null;
            }

            if (!currentSession.IsRefreshRecommended() || string.IsNullOrWhiteSpace(currentSession.RefreshToken))
            {
                return currentSession;
            }

            return await this.RefreshTokenAsync(currentSession.RefreshToken).ConfigureAwait(false);
        }

        private static object BuildExecution(string commandName)
        {
            var lower = (commandName ?? string.Empty).Trim().ToLowerInvariant();
            if (lower == "start")
            {
                return new { command = "action.devices.commands.StartStop", @params = new { on = true } };
            }

            if (lower == "stop")
            {
                return new { command = "action.devices.commands.StartStop", @params = new { on = false } };
            }

            if (lower == "pause")
            {
                return new { command = "action.devices.commands.PauseUnpause", @params = new { on = false } };
            }

            if (lower == "resume")
            {
                return new { command = "action.devices.commands.PauseUnpause", @params = new { on = true } };
            }

            if (lower == "dock")
            {
                return new { command = "action.devices.commands.Dock" };
            }

            throw new InvalidOperationException("Unbekannter Befehl: " + commandName);
        }

        private async Task<SessionData> SendTokenRequestAsync(FormUrlEncodedContent content)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, "/openapi/oauth/getAccessToken"))
            {
                request.Content = content;
                var response = await this.httpClient.SendAsync(request).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var root = JsonUtils.ParseObject(json);
                return new SessionData
                {
                    AccessToken = JsonUtils.GetString(root, "access_token"),
                    RefreshToken = JsonUtils.GetString(root, "refresh_token"),
                    ExpiresIn = JsonUtils.GetInt(root, "expires_in", 0),
                    ObtainedAtUtc = DateTime.UtcNow,
                };
            }
        }

        private async Task<Dictionary<string, object>> SendAuthorizedAsync(HttpMethod method, string relativeUrl, string accessToken, object body)
        {
            using (var request = new HttpRequestMessage(method, relativeUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("requestId", Guid.NewGuid().ToString());

                if (body != null)
                {
                    request.Content = new StringContent(JsonUtils.ToJson(body), Encoding.UTF8, "application/json");
                }

                var response = await this.httpClient.SendAsync(request).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("HTTP " + (int)response.StatusCode + ": " + json);
                }

                return JsonUtils.ParseObject(json);
            }
        }

        private static void EnsureSuccess(Dictionary<string, object> response)
        {
            var code = JsonUtils.GetInt(response, "code", 0);
            if (code != 1)
            {
                var description = JsonUtils.GetString(response, "desc");
                if (string.IsNullOrWhiteSpace(description))
                {
                    description = JsonUtils.ToJson(response);
                }

                throw new InvalidOperationException(description);
            }
        }
    }
}
