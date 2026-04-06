using System;
using System.Collections.Generic;

namespace NavimowDesktopController
{
    internal sealed class SessionData
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public DateTime ObtainedAtUtc { get; set; }

        public bool IsRefreshRecommended()
        {
            if (string.IsNullOrWhiteSpace(this.AccessToken))
            {
                return false;
            }

            if (this.ExpiresIn <= 0)
            {
                return false;
            }

            return DateTime.UtcNow >= this.ObtainedAtUtc.AddSeconds(this.ExpiresIn - 300);
        }
    }

    internal sealed class NavimowDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Dictionary<string, object> Raw { get; set; }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(this.Name))
            {
                return this.Name + " (" + this.Id + ")";
            }

            return this.Id ?? "(unbekannt)";
        }
    }

    internal sealed class MqttConnectionInfo
    {
        public string MqttHost { get; set; }
        public string MqttUrl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    internal sealed class MqttPublishMessage
    {
        public string Topic { get; set; }
        public string Payload { get; set; }
    }
}
