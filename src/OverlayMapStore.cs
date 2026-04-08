using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace NavimowDesktopController
{
    internal sealed class OverlayMapStore
    {
        private readonly string imageFilePath;
        private readonly string metadataFilePath;

        public OverlayMapStore()
        {
            var baseDirectory = SessionStore.GetBaseDirectory();
            this.imageFilePath = Path.Combine(baseDirectory, "map-overlay.png");
            this.metadataFilePath = Path.Combine(baseDirectory, "map-overlay.json");
        }

        public OverlayMapState Load()
        {
            if (!File.Exists(this.imageFilePath) || !File.Exists(this.metadataFilePath))
            {
                return null;
            }

            try
            {
                var root = JsonUtils.ParseObject(File.ReadAllText(this.metadataFilePath));
                using (var image = Image.FromFile(this.imageFilePath))
                {
                    return new OverlayMapState
                    {
                        Image = new Bitmap(image),
                        ScalePercent = JsonUtils.GetInt(root, "scale_percent", 100),
                        RotationDegrees = (float)(JsonUtils.GetDouble(root, "rotation_degrees") ?? 0D),
                        CenterX = (float)(JsonUtils.GetDouble(root, "center_x") ?? 0D),
                        CenterY = (float)(JsonUtils.GetDouble(root, "center_y") ?? 0D),
                        BaseWidthWorld = (float)(JsonUtils.GetDouble(root, "base_width_world") ?? 0D),
                        BaseHeightWorld = (float)(JsonUtils.GetDouble(root, "base_height_world") ?? 0D),
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        public void Save(Image image, OverlayMapState state)
        {
            if (image == null || state == null)
            {
                return;
            }

            using (var bitmap = new Bitmap(image))
            {
                bitmap.Save(this.imageFilePath, ImageFormat.Png);
            }

            var payload = new
            {
                scale_percent = state.ScalePercent,
                rotation_degrees = state.RotationDegrees,
                center_x = state.CenterX,
                center_y = state.CenterY,
                base_width_world = state.BaseWidthWorld,
                base_height_world = state.BaseHeightWorld,
                saved_at_utc = DateTime.UtcNow.ToString("o"),
            };

            File.WriteAllText(this.metadataFilePath, JsonUtils.ToJson(payload));
        }

        public void Clear()
        {
            if (File.Exists(this.imageFilePath))
            {
                File.Delete(this.imageFilePath);
            }

            if (File.Exists(this.metadataFilePath))
            {
                File.Delete(this.metadataFilePath);
            }
        }
    }
}
