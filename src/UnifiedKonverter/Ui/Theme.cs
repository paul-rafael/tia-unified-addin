using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Prh.UnifiedKonverter.Ui
{
    /// <summary>
    /// Farben und Schriften nach PRH Design Kit (brand/DESIGN.md im Converter-Repo).
    /// Dunkelrot ist der einzige Farbakzent, Schwarz (dunkel: Weiß) der Zweitakzent, keine Blautöne.
    /// Hell/Dunkel folgt der Windows-Einstellung „App-Modus“.
    /// </summary>
    internal sealed class Theme
    {
        public Color Bg, Surface2, Border, BorderStrong, Text, Text2, Text3, Red, RedHover, RedText, RedSoft, Ink, InkSoft, OnInk;
        public bool Dark;

        public static readonly Font Body = new Font("Segoe UI", 9.75f, FontStyle.Regular);
        public static readonly Font Strong = new Font("Segoe UI Semibold", 9.75f, FontStyle.Regular);
        public static readonly Font Title = new Font("Segoe UI Semibold", 16f, FontStyle.Regular);
        public static readonly Font Eyebrow = new Font("Segoe UI Semibold", 7.5f, FontStyle.Regular);
        public static readonly Font Small = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font Mono = new Font("Consolas", 9f, FontStyle.Regular);

        public static Theme Current()
        {
            return IsWindowsDark() ? DarkTheme() : LightTheme();
        }

        private static Theme LightTheme()
        {
            return new Theme
            {
                Dark = false,
                Bg = Hex("#ffffff"), Surface2 = Hex("#f8f8f8"), Border = Hex("#e7e7e7"), BorderStrong = Hex("#d2d2d2"),
                Text = Hex("#121212"), Text2 = Hex("#4b4b4b"), Text3 = Hex("#8a8a8a"),
                Red = Hex("#8a0b18"), RedHover = Hex("#6d0812"), RedText = Hex("#8a0b18"), RedSoft = Hex("#f9ecee"),
                Ink = Hex("#121212"), InkSoft = Hex("#f4f4f4"), OnInk = Hex("#ffffff"),
            };
        }

        private static Theme DarkTheme()
        {
            return new Theme
            {
                Dark = true,
                Bg = Hex("#0a0a0a"), Surface2 = Hex("#161616"), Border = Hex("#232323"), BorderStrong = Hex("#343434"),
                Text = Hex("#f0f0f0"), Text2 = Hex("#b5b5b5"), Text3 = Hex("#7f7f7f"),
                Red = Hex("#9a0e1b"), RedHover = Hex("#b31220"), RedText = Hex("#c7232f"), RedSoft = Hex("#1b0608"),
                Ink = Hex("#f0f0f0"), InkSoft = Hex("#171717"), OnInk = Hex("#0a0a0a"),
            };
        }

        private static bool IsWindowsDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Color Hex(string hex)
        {
            return ColorTranslator.FromHtml(hex);
        }

        /// <summary>Hauptbutton: dunkelrot, weiße Schrift. Pro Ansicht nur einer.</summary>
        public Button PrimaryButton(string text)
        {
            Button b = BaseButton(text);
            b.BackColor = Red;
            b.ForeColor = Color.White;
            b.FlatAppearance.BorderColor = Red;
            b.FlatAppearance.MouseOverBackColor = RedHover;
            b.FlatAppearance.MouseDownBackColor = RedHover;
            return b;
        }

        /// <summary>Ghost-Button: transparent, feiner Rahmen.</summary>
        public Button GhostButton(string text)
        {
            Button b = BaseButton(text);
            b.BackColor = Bg;
            b.ForeColor = Text;
            b.FlatAppearance.BorderColor = BorderStrong;
            b.FlatAppearance.MouseOverBackColor = InkSoft;
            b.FlatAppearance.MouseDownBackColor = InkSoft;
            return b;
        }

        private static Button BaseButton(string text)
        {
            Button b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Font = Strong,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 6, 14, 6),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        public void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Bg;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Border;
            grid.EnableHeadersVisualStyles = false;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.RowHeadersVisible = false;
            grid.ColumnHeadersHeight = 32;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = 28;

            grid.ColumnHeadersDefaultCellStyle.BackColor = Surface2;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Text2;
            grid.ColumnHeadersDefaultCellStyle.Font = Eyebrow;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface2;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            grid.DefaultCellStyle.BackColor = Bg;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.Font = Body;
            grid.DefaultCellStyle.SelectionBackColor = RedSoft;
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        }
    }
}
