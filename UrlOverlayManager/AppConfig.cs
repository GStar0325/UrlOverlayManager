using System.Collections.Generic;

namespace UrlOverlayManager
{
    public class AppConfig
    {
        public List<OverlayItemConfig> Items { get; set; } = new List<OverlayItemConfig>();
        public HotkeySettings Hotkeys { get; set; } = HotkeySettings.CreateDefault();
        public List<PresetConfig> Presets { get; set; } = new List<PresetConfig>();
    }
}
