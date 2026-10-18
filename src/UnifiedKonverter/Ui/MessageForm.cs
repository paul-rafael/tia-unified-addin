using System.Drawing;
using System.Windows.Forms;

namespace Prh.UnifiedKonverter.Ui
{
    /// <summary>Schlichte Dialoge im Design Kit: Fehler (Details kopierbar), Hinweis und Rückfrage.</summary>
    internal static class MessageForm
    {
        public static void ShowError(string title, string details)
        {
            Show("FEHLER", title, details, true, null, "Schließen");
        }

        public static void ShowInfo(string title, string text)
        {
            Show("HINWEIS", title, text, false, null, "Schließen");
        }

        /// <summary>Rückfrage mit dunkelrotem Hauptbutton. true = bestätigt.</summary>
        public static bool Confirm(string title, string text, string confirmText)
        {
            return Show("BESTÄTIGEN", title, text, false, confirmText, "Abbrechen");
        }

        private static bool Show(string eyebrowText, string title, string text, bool isError, string confirmText, string closeText)
        {
            Theme t = Theme.Current();
            bool confirmed = false;
            using (Form form = new Form())
            {
                form.Text = "Unified-Konverter";
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Size = new Size(isError ? 720 : 560, isError ? 440 : 300);
                form.MinimumSize = new Size(420, 240);
                form.BackColor = t.Bg;
                form.ForeColor = t.Text;
                form.Font = Theme.Body;
                form.TopMost = true;
                form.KeyPreview = true;
                form.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) form.Close(); };

                Label eyebrow = new Label { Text = eyebrowText, Font = Theme.Eyebrow, ForeColor = t.RedText, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
                Label head = new Label { Text = title, Font = Theme.Title, AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
                TextBox box = new TextBox
                {
                    Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n"),
                    Multiline = true, ReadOnly = true, ScrollBars = isError ? ScrollBars.Both : ScrollBars.Vertical, WordWrap = !isError,
                    Dock = DockStyle.Fill, Font = isError ? Theme.Mono : Theme.Body,
                    BackColor = isError ? t.RedSoft : t.Bg, ForeColor = isError ? t.Text : t.Text2,
                    BorderStyle = isError ? BorderStyle.FixedSingle : BorderStyle.None, TabStop = false,
                };

                FlowLayoutPanel buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, WrapContents = false, BackColor = t.Bg, Padding = new Padding(0, 8, 0, 0) };
                PrButton close = new PrButton(t, PrButton.Style.Ghost, closeText);
                close.Click += (s, e) => form.Close();
                if (confirmText != null)
                {
                    PrButton ok = new PrButton(t, PrButton.Style.Primary, confirmText);
                    ok.Click += (s, e) => { confirmed = true; form.Close(); };
                    buttons.Controls.Add(ok);
                }
                buttons.Controls.Add(close);

                TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(28, 22, 28, 18), BackColor = t.Bg };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
                layout.Controls.Add(eyebrow, 0, 0);
                layout.Controls.Add(head, 0, 1);
                layout.Controls.Add(box, 0, 2);
                layout.Controls.Add(buttons, 0, 3);
                form.Controls.Add(layout);
                form.ShowDialog();
            }
            return confirmed;
        }
    }
}
