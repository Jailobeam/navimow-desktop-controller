using System;
using System.IO;

namespace NavimowDesktopController
{
    internal sealed class SessionStore
    {
        private readonly string sessionFilePath;

        public SessionStore()
        {
            var baseDirectory = GetBaseDirectory();
            this.sessionFilePath = Path.Combine(baseDirectory, "session.json");
        }

        public static string GetBaseDirectory()
        {
            var baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NavimowDesktopController");

            Directory.CreateDirectory(baseDirectory);
            return baseDirectory;
        }

        public SessionData Load()
        {
            if (!File.Exists(this.sessionFilePath))
            {
                return null;
            }

            try
            {
                var root = JsonUtils.ParseObject(File.ReadAllText(this.sessionFilePath));
                return new SessionData
                {
                    AccessToken = JsonUtils.GetString(root, "access_token"),
                    RefreshToken = JsonUtils.GetString(root, "refresh_token"),
                    ExpiresIn = JsonUtils.GetInt(root, "expires_in", 0),
                    ObtainedAtUtc = this.ParseTimestamp(JsonUtils.GetString(root, "obtained_at_utc")),
                };
            }
            catch
            {
                return null;
            }
        }

        public void Save(SessionData session)
        {
            var payload = new
            {
                access_token = session.AccessToken,
                refresh_token = session.RefreshToken,
                expires_in = session.ExpiresIn,
                obtained_at_utc = session.ObtainedAtUtc.ToString("o"),
            };

            File.WriteAllText(this.sessionFilePath, JsonUtils.ToJson(payload));
        }

        public void Clear()
        {
            if (File.Exists(this.sessionFilePath))
            {
                File.Delete(this.sessionFilePath);
            }
        }

        private DateTime ParseTimestamp(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed))
            {
                return parsed.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }
    }
}
