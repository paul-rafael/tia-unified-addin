using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Prh.UnifiedKonverter.EventExport;

namespace Prh.UnifiedKonverter.Ui
{
    /// <summary>
    /// HMI Event Exporter: erkanntes Gerät, Tabellen und Variablen, Export starten, Ergebnis und Speicherort.
    /// Der Export selbst läuft im Thread des Menü-Aufrufs (UiThread.Invoke); den Fortschritt je Variable zeigt
    /// der TIA-Dialog „Exklusiver Zugriff“ (mit Abbrechen).
    /// </summary>
    internal sealed class EventExportForm : Form
    {
        /// <summary>Upload-Seite des Web-Converters.</summary>
        public const string ConverterUrl = "https://paul-rafael.github.io/wincc-unified-list-converter/";

        private readonly ScanResult _scan;
        private readonly Func<string, ExportResult> _export;
        private readonly Theme _t = Theme.Current();
        private readonly Label _result = new Label();
        private readonly PrButton _start;
        private readonly PrButton _openFolder;
        private string _zipPath;

        public EventExportForm(ScanResult scan, Func<string, ExportResult> export)
        {
            _scan = scan;
            _export = export;

            Text = "HMI Event Exporter";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(760, 560);
            MinimumSize = new Size(620, 480);
            BackColor = _t.Bg;
            ForeColor = _t.Text;
            Font = Theme.Body;
            TopMost = true;
            Shown += (s, e) => { TopMost = false; Activate(); };
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            _start = new PrButton(_t, PrButton.Style.Primary, "Export starten");
            _start.Enabled = scan.Tags.Count > 0;
            _start.Click += (s, e) => StartExport();
            _openFolder = new PrButton(_t, PrButton.Style.Ghost, "Ordner öffnen") { Visible = false };
            _openFolder.Click += (s, e) => { if (_zipPath != null) Process.Start("explorer.exe", "/select,\"" + _zipPath + "\""); };

            Controls.Add(BuildContent());
            Controls.Add(BuildFooter());
            Controls.Add(BuildHeader());
        }

        private Control BuildHeader()
        {
            LinePanel header = new LinePanel(_t.Bg, _t.Border, DockStyle.Bottom) { Dock = DockStyle.Top, Height = 58, Padding = new Padding(24, 0, 24, 0) };
            PictureBox logo = new PictureBox { Image = Draw.Logo(_t.Text, 24, 1f), SizeMode = PictureBoxSizeMode.AutoSize, BackColor = _t.Bg };
            Label product = new Label { Text = "HMI Event Exporter", Font = Theme.Product, ForeColor = _t.Text, AutoSize = true, BackColor = _t.Bg };
            Label right = new Label { Text = "TIA Portal " + ManifestWriter.TiaPortalVersion, ForeColor = _t.Text2, AutoSize = true, BackColor = _t.Bg };
            header.Controls.AddRange(new Control[] { logo, product, right });
            header.Layout += (s, e) =>
            {
                logo.Location = new Point(header.Padding.Left, (header.Height - logo.Height) / 2);
                product.Location = new Point(logo.Right + 12, (header.Height - product.Height) / 2);
                right.Location = new Point(header.Width - header.Padding.Right - right.Width, (header.Height - right.Height) / 2);
            };
            return header;
        }

        private Control BuildContent()
        {
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(28, 24, 28, 12), BackColor = _t.Bg };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label eyebrow = new Label { Text = "VARIABLEN INKLUSIVE EVENTS EXPORTIEREN", Font = Theme.Eyebrow, ForeColor = _t.Text3, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            Label title = new Label { Text = _scan.HmiDeviceName, Font = Theme.Title, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            Label lead = new Label
            {
                Text = (string.IsNullOrEmpty(_scan.ProjectName) ? "" : "Projekt „" + _scan.ProjectName + "“ · ")
                    + "Jede Variable wird einzeln als SimaticML-XML exportiert (auch ohne Ereignisse) und mit Manifest und Bericht in ein ZIP gepackt. "
                    + "Das Projekt wird nicht verändert.",
                ForeColor = _t.Text2, AutoSize = true, MaximumSize = new Size(680, 0), Margin = new Padding(0, 0, 0, 16),
            };

            FlowLayoutPanel tiles = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = _t.Bg, WrapContents = false, Margin = new Padding(0) };
            StatTile tables = new StatTile(_t, "Variablentabellen", false) { Size = new Size(200, 88), Margin = new Padding(0, 0, 12, 0) };
            StatTile tags = new StatTile(_t, "Variablen", false) { Size = new Size(200, 88), Margin = new Padding(0, 0, 12, 0) };
            StatTile errors = new StatTile(_t, "nicht lesbar", true) { Size = new Size(200, 88), Margin = new Padding(0) };
            tables.SetValue(_scan.Tables.Count);
            tags.SetValue(_scan.Tags.Count);
            errors.SetValue(_scan.ScanErrors.Count);
            tiles.Controls.AddRange(new Control[] { tables, tags, errors });

            _result.AutoSize = false;
            _result.Dock = DockStyle.Fill;
            _result.Font = Theme.Mono;
            _result.ForeColor = _t.Text;
            _result.BackColor = _t.Bg;
            _result.Padding = new Padding(0, 14, 0, 0);
            _result.Text = _scan.Tags.Count == 0
                ? "Keine Variablen gefunden – nichts zu exportieren."
                : "Bereit. „Export starten“ fragt nach dem Speicherort des ZIP-Archivs.";

            layout.Controls.Add(eyebrow, 0, 0);
            layout.Controls.Add(title, 0, 1);
            layout.Controls.Add(lead, 0, 2);
            layout.Controls.Add(tiles, 0, 3);
            layout.Controls.Add(_result, 0, 4);
            return layout;
        }

        private Control BuildFooter()
        {
            LinePanel footer = new LinePanel(_t.Bg, _t.Border, DockStyle.Top) { Dock = DockStyle.Bottom, Height = 66, Padding = new Padding(24, 0, 24, 0) };
            PrButton close = new PrButton(_t, PrButton.Style.Ghost, "Schließen");
            close.Click += (s, e) => Close();
            PrButton converter = new PrButton(_t, PrButton.Style.Ghost, "Converter öffnen");
            converter.Click += (s, e) => Process.Start(ConverterUrl);
            footer.Controls.AddRange(new Control[] { close, converter, _openFolder, _start });
            footer.Layout += (s, e) =>
            {
                int cy = footer.Height / 2;
                int x = footer.Width - footer.Padding.Right;
                foreach (PrButton b in new[] { _start, _openFolder, converter, close })
                {
                    if (!b.Visible) continue;
                    x -= b.Width;
                    b.Location = new Point(x, cy - b.Height / 2);
                    x -= 10;
                }
            };
            return footer;
        }

        private void StartExport()
        {
            string path;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "HMI-Event-Export speichern";
                dialog.Filter = "ZIP-Archiv (*.zip)|*.zip";
                dialog.FileName = "HMI_Event_Export_" + FileNamer.Clean(_scan.HmiDeviceName) + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".zip";
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Unified-Konverter", "Export");
                try { Directory.CreateDirectory(dir); dialog.InitialDirectory = dir; }
                catch (Exception) { }
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                path = dialog.FileName;
            }

            _start.Enabled = false;
            _result.ForeColor = _t.Text2;
            _result.Text = "Export läuft … Fortschritt und Abbrechen im TIA-Dialog „Exklusiver Zugriff“.";
            _result.Refresh();
            ExportResult r;
            try
            {
                UseWaitCursor = true;
                r = UiThread.Invoke(() => _export(path));
            }
            catch (Exception ex)
            {
                _start.Enabled = true;
                _result.Text = "Export abgebrochen.";
                MessageForm.ShowError("Export fehlgeschlagen", ex.ToString());
                return;
            }
            finally
            {
                UseWaitCursor = false;
            }

            _zipPath = r.ZipPath;
            _openFolder.Visible = true;
            _openFolder.Parent.PerformLayout();
            _start.Text = "Erneut exportieren";
            _start.FitWidth();
            _start.Enabled = true;
            _result.ForeColor = r.Failed > 0 || r.Cancelled ? _t.SignalText : _t.Text;
            _result.Text = (r.Cancelled ? "Export abgebrochen – Archiv enthält nur die bis dahin exportierten Variablen" : "Export abgeschlossen") + "\n\n"
                + "Variablen gefunden:      " + r.Scan.Tags.Count.ToString("N0").PadLeft(8) + "\n"
                + "Erfolgreich exportiert:  " + r.Successful.ToString("N0").PadLeft(8) + "\n"
                + "Fehlgeschlagen:          " + r.Failed.ToString("N0").PadLeft(8) + "\n"
                + "Dauer:                   " + (r.Duration.TotalSeconds.ToString("0.0") + " s").PadLeft(8) + "\n\n"
                + "Datei:\n" + r.ZipPath
                + (r.Failed > 0 ? "\n\nErste Fehler:\n" + string.Join("\n", r.Files.Where(f => !f.Success).Take(5).Select(f => f.TagName + ": " + f.Error)) : "");
        }
    }
}
