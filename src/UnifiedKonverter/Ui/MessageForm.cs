using System.Drawing;
using System.Windows.Forms;

namespace Prh.UnifiedKonverter.Ui
{
    /// <summary>Schlichte Fehlermeldung im Design Kit: dunkelrote Eyebrow, Details kopierbar.</summary>
    internal static class MessageForm
    {
        public static void ShowError(string title, string details)
        {
            Theme t = Theme.Current();
            using (Form form = new Form())
            {
                form.Text = "Unified-Konverter";
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Size = new Size(640, 400);
                form.BackColor = t.Bg;
                form.ForeColor = t.Text;
                form.Font = Theme.Body;
                form.TopMost = true;

                Label eyebrow = new Label { Text = "FEHLER", Font = Theme.Eyebrow, ForeColor = t.RedText, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
                Label head = new Label { Text = title, Font = Theme.Title, AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
                TextBox box = new TextBox
                {
                    Text = details, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                    Dock = DockStyle.Fill, Font = Theme.Mono, BackColor = t.RedSoft, ForeColor = t.Text, BorderStyle = BorderStyle.FixedSingle,
                };
                Button close = t.GhostButton("Schließen");
                close.Anchor = AnchorStyles.Right;
                close.Click += (s, e) => form.Close();
                form.CancelButton = close;

                TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(24, 20, 24, 16), BackColor = t.Bg };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                layout.Controls.Add(eyebrow, 0, 0);
                layout.Controls.Add(head, 0, 1);
                layout.Controls.Add(box, 0, 2);
                layout.Controls.Add(close, 0, 3);
                form.Controls.Add(layout);
                form.ShowDialog();
            }
        }
    }
}
