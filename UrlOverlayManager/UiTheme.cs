using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public static class UiTheme
    {
        private const string FallbackFontFamilyName = "맑은 고딕";
        private const string AppFontResourceName = "UrlOverlayManager.Assets.Mabinogi_Classic_TTF.ttf";
        public const string SystemFontMode = "System";
        public const string AppFontMode = "MabinogiClassic";
        private static ThemePalette palette = ThemePalette.CreateLight();
        private static readonly PrivateFontCollection appFonts = new PrivateFontCollection();
        private static FontFamily? appFontFamily;
        private static IntPtr appFontMemory;
        private static int appFontMemoryLength;
        private static IntPtr appFontResource;
        private static string fontMode = AppFontMode;

        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);

        public static Color AppBackColor => palette.AppBackColor;
        public static Color PanelBackColor => palette.PanelBackColor;
        public static Color FieldBackColor => palette.FieldBackColor;
        public static Color TextColor => palette.TextColor;
        public static Color MutedTextColor => palette.MutedTextColor;
        public static Color BorderColor => palette.BorderColor;
        public static Color AccentColor => palette.AccentColor;
        public static Color AccentDarkColor => palette.AccentDarkColor;
        public static Color DangerColor => palette.DangerColor;
        public static Color HeaderBackColor => palette.HeaderBackColor;
        public static Color AlternateRowBackColor => palette.AlternateRowBackColor;
        public static Color PreviewBackColor => palette.PreviewBackColor;
        public static string AppFontFamilyName => GetAppFontFamily().Name;
        public static FontFamily AppFontFamily => GetAppFontFamily();
        public static string CurrentFontMode => fontMode;

        public static void ApplyTheme(string themeName)
        {
            palette = themeName switch
            {
                "Dark" => ThemePalette.CreateDark(),
                "Mint" => ThemePalette.CreateMint(),
                "Sky" => ThemePalette.CreateSky(),
                _ => ThemePalette.CreateLight()
            };
        }

        public static void ApplyFont(string selectedFontMode)
        {
            fontMode = string.Equals(selectedFontMode, SystemFontMode, System.StringComparison.OrdinalIgnoreCase)
                ? SystemFontMode
                : AppFontMode;
        }

        public static Font RegularFont(float size = 9F)
        {
            return CreateFont(size, FontStyle.Regular);
        }

        public static Font BoldFont(float size = 9F)
        {
            return CreateFont(size, FontStyle.Bold);
        }

        private static Font CreateFont(float size, FontStyle style)
        {
            FontFamily family = GetAppFontFamily();
            FontStyle supportedStyle = family.IsStyleAvailable(style) ? style : FontStyle.Regular;
            return new Font(family, size, supportedStyle);
        }

        private static FontFamily GetAppFontFamily()
        {
            if (string.Equals(fontMode, SystemFontMode, System.StringComparison.OrdinalIgnoreCase))
                return new FontFamily(FallbackFontFamilyName);

            if (appFontFamily != null)
                return appFontFamily;

            byte[]? fontData = ReadAppFontData();

            if (fontData != null)
            {
                try
                {
                    appFontMemory = Marshal.AllocCoTaskMem(fontData.Length);
                    appFontMemoryLength = fontData.Length;
                    Marshal.Copy(fontData, 0, appFontMemory, fontData.Length);
                    uint fontCount = 0;
                    appFontResource = AddFontMemResourceEx(appFontMemory, (uint)appFontMemoryLength, IntPtr.Zero, ref fontCount);
                    appFonts.AddMemoryFont(appFontMemory, appFontMemoryLength);

                    if (appFonts.Families.Length > 0)
                    {
                        appFontFamily = appFonts.Families[0];
                        return appFontFamily;
                    }
                }
                catch
                {
                    appFontFamily = null;
                }
            }

            appFontFamily = new FontFamily(FallbackFontFamilyName);
            return appFontFamily;
        }

        public static byte[]? ReadAppFontData()
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(AppFontResourceName);

            if (stream == null)
                return null;

            using MemoryStream memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }

        public static void StyleButton(Button button, Color? backColor = null, Color? foreColor = null)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = BorderColor;
            button.BackColor = backColor ?? AccentColor;
            button.ForeColor = foreColor ?? Color.White;
            button.Font = BoldFont();
            button.Height = 28;
            button.Cursor = Cursors.Hand;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(6, 0, 6, 0);
            button.UseVisualStyleBackColor = false;
        }

        public static void StyleToolbarButton(Button button, string text, Color backColor, Color foreColor, int width)
        {
            button.Text = text;
            button.Size = new Size(width, 28);
            button.Margin = new Padding(0, 0, 6, 6);
            StyleButton(button, backColor, foreColor);
        }

        public static void StyleTextBox(TextBox textBox)
        {
            textBox.BackColor = FieldBackColor;
            textBox.ForeColor = TextColor;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Font = RegularFont(10F);
        }

        public static void StyleListBox(ListBox listBox)
        {
            listBox.BackColor = PanelBackColor;
            listBox.ForeColor = TextColor;
            listBox.BorderStyle = BorderStyle.FixedSingle;
            listBox.Font = RegularFont(10F);
        }

        public static void StyleNumeric(NumericUpDown numeric)
        {
            numeric.BackColor = FieldBackColor;
            numeric.ForeColor = TextColor;
            numeric.Font = RegularFont(10F);
            numeric.Height = 28;
        }

        public static void StyleComboBox(ComboBox comboBox)
        {
            comboBox.BackColor = FieldBackColor;
            comboBox.ForeColor = TextColor;
            comboBox.Font = RegularFont(10F);
            comboBox.Height = 28;
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = AppBackColor;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.GridColor = BorderColor;
            grid.AllowUserToResizeRows = false;

            grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBackColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
            grid.ColumnHeadersDefaultCellStyle.Font = BoldFont();
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderBackColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextColor;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(7, 0, 7, 0);

            grid.DefaultCellStyle.BackColor = FieldBackColor;
            grid.DefaultCellStyle.ForeColor = TextColor;
            grid.DefaultCellStyle.SelectionBackColor = AccentDarkColor;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = RegularFont();
            grid.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);

            grid.AlternatingRowsDefaultCellStyle.BackColor = AlternateRowBackColor;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = TextColor;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = AccentDarkColor;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
        }

        public static void ApplyFonts(Control root)
        {
            ApplyFont(root);

            foreach (Control child in root.Controls)
            {
                ApplyFonts(child);
            }
        }

        private static void ApplyFont(Control control)
        {
            Font currentFont = control.Font;
            FontStyle style = currentFont.Style;

            if (style == FontStyle.Regular && currentFont.Bold)
                style = FontStyle.Bold;

            Font nextFont = style.HasFlag(FontStyle.Bold)
                ? BoldFont(currentFont.SizeInPoints)
                : RegularFont(currentFont.SizeInPoints);

            control.Font = nextFont;

            if (control is Label label)
            {
                label.UseCompatibleTextRendering = true;
            }
            else if (control is Button button)
            {
                button.UseCompatibleTextRendering = true;
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.UseCompatibleTextRendering = true;
            }
            else if (control is RadioButton radioButton)
            {
                radioButton.UseCompatibleTextRendering = true;
            }
        }

        private sealed class ThemePalette
        {
            public Color AppBackColor { get; init; }
            public Color PanelBackColor { get; init; }
            public Color FieldBackColor { get; init; }
            public Color TextColor { get; init; }
            public Color MutedTextColor { get; init; }
            public Color BorderColor { get; init; }
            public Color AccentColor { get; init; }
            public Color AccentDarkColor { get; init; }
            public Color DangerColor { get; init; }
            public Color HeaderBackColor { get; init; }
            public Color AlternateRowBackColor { get; init; }
            public Color PreviewBackColor { get; init; }

            public static ThemePalette CreateLight()
            {
                return new ThemePalette
                {
                    AppBackColor = Color.FromArgb(243, 243, 243),
                    PanelBackColor = Color.FromArgb(255, 255, 255),
                    FieldBackColor = Color.White,
                    TextColor = Color.FromArgb(30, 30, 30),
                    MutedTextColor = Color.FromArgb(90, 90, 90),
                    BorderColor = Color.FromArgb(204, 204, 204),
                    AccentColor = Color.FromArgb(0, 122, 204),
                    AccentDarkColor = Color.FromArgb(0, 95, 160),
                    DangerColor = Color.FromArgb(185, 62, 62),
                    HeaderBackColor = Color.FromArgb(238, 238, 242),
                    AlternateRowBackColor = Color.FromArgb(248, 248, 248),
                    PreviewBackColor = Color.FromArgb(30, 30, 30)
                };
            }

            public static ThemePalette CreateDark()
            {
                return new ThemePalette
                {
                    AppBackColor = Color.FromArgb(30, 30, 30),
                    PanelBackColor = Color.FromArgb(37, 37, 38),
                    FieldBackColor = Color.FromArgb(30, 30, 30),
                    TextColor = Color.FromArgb(212, 212, 212),
                    MutedTextColor = Color.FromArgb(150, 150, 150),
                    BorderColor = Color.FromArgb(63, 63, 70),
                    AccentColor = Color.FromArgb(0, 122, 204),
                    AccentDarkColor = Color.FromArgb(9, 71, 113),
                    DangerColor = Color.FromArgb(185, 62, 62),
                    HeaderBackColor = Color.FromArgb(45, 45, 48),
                    AlternateRowBackColor = Color.FromArgb(34, 34, 34),
                    PreviewBackColor = Color.FromArgb(16, 16, 16)
                };
            }

            public static ThemePalette CreateMint()
            {
                return new ThemePalette
                {
                    AppBackColor = Color.FromArgb(242, 249, 247),
                    PanelBackColor = Color.White,
                    FieldBackColor = Color.White,
                    TextColor = Color.FromArgb(29, 49, 48),
                    MutedTextColor = Color.FromArgb(87, 113, 109),
                    BorderColor = Color.FromArgb(176, 207, 201),
                    AccentColor = Color.FromArgb(58, 150, 134),
                    AccentDarkColor = Color.FromArgb(34, 114, 101),
                    DangerColor = Color.FromArgb(185, 88, 90),
                    HeaderBackColor = Color.FromArgb(230, 244, 240),
                    AlternateRowBackColor = Color.FromArgb(247, 252, 250),
                    PreviewBackColor = Color.FromArgb(24, 39, 38)
                };
            }

            public static ThemePalette CreateSky()
            {
                return new ThemePalette
                {
                    AppBackColor = Color.FromArgb(239, 248, 253),
                    PanelBackColor = Color.FromArgb(252, 254, 255),
                    FieldBackColor = Color.White,
                    TextColor = Color.FromArgb(33, 54, 67),
                    MutedTextColor = Color.FromArgb(94, 122, 139),
                    BorderColor = Color.FromArgb(171, 210, 231),
                    AccentColor = Color.FromArgb(104, 174, 214),
                    AccentDarkColor = Color.FromArgb(67, 139, 181),
                    DangerColor = Color.FromArgb(193, 94, 101),
                    HeaderBackColor = Color.FromArgb(226, 243, 252),
                    AlternateRowBackColor = Color.FromArgb(247, 252, 255),
                    PreviewBackColor = Color.FromArgb(25, 41, 52)
                };
            }
        }
    }
}
