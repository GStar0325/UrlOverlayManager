using System;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public class HotkeyConfig
    {
        public bool Enabled { get; set; } = true;
        public bool Control { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public Keys Key { get; set; } = Keys.None;

        public bool IsValid()
        {
            return Enabled && Key != Keys.None && (Control || Alt || Shift || Win);
        }

        public bool SameAs(HotkeyConfig other)
        {
            return IsValid() &&
                other.IsValid() &&
                Control == other.Control &&
                Alt == other.Alt &&
                Shift == other.Shift &&
                Win == other.Win &&
                Key == other.Key;
        }

        public HotkeyConfig Clone()
        {
            return new HotkeyConfig
            {
                Enabled = Enabled,
                Control = Control,
                Alt = Alt,
                Shift = Shift,
                Win = Win,
                Key = Key
            };
        }

        public override string ToString()
        {
            if (!Enabled)
                return "사용 안 함";

            if (Key == Keys.None)
                return "지정 안 됨";

            string text = "";

            if (Control)
                text += "Ctrl + ";

            if (Alt)
                text += "Alt + ";

            if (Shift)
                text += "Shift + ";

            if (Win)
                text += "Win + ";

            return text + Key;
        }
    }

    public class HotkeySettings
    {
        public HotkeyConfig ToggleOverlays { get; set; } = new HotkeyConfig();
        public HotkeyConfig EditMode { get; set; } = new HotkeyConfig();
        public HotkeyConfig ToggleClickThrough { get; set; } = new HotkeyConfig();
        public HotkeyConfig MoveToCurrentScreen { get; set; } = new HotkeyConfig();

        public static HotkeySettings CreateDefault()
        {
            return new HotkeySettings
            {
                ToggleOverlays = new HotkeyConfig { Enabled = false },
                EditMode = new HotkeyConfig { Enabled = false },
                ToggleClickThrough = new HotkeyConfig { Enabled = false },
                MoveToCurrentScreen = new HotkeyConfig { Enabled = false }
            };
        }

        public static HotkeySettings CreateRecommended()
        {
            return new HotkeySettings
            {
                ToggleOverlays = new HotkeyConfig { Control = true, Alt = true, Key = Keys.O },
                EditMode = new HotkeyConfig { Control = true, Alt = true, Key = Keys.E },
                ToggleClickThrough = new HotkeyConfig { Control = true, Alt = true, Key = Keys.C },
                MoveToCurrentScreen = new HotkeyConfig { Control = true, Alt = true, Key = Keys.M }
            };
        }

        public HotkeySettings Clone()
        {
            return new HotkeySettings
            {
                ToggleOverlays = ToggleOverlays.Clone(),
                EditMode = EditMode.Clone(),
                ToggleClickThrough = ToggleClickThrough.Clone(),
                MoveToCurrentScreen = MoveToCurrentScreen.Clone()
            };
        }
    }
}
