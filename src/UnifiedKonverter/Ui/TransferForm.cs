using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Prh.UnifiedKonverter.Openness;
using Prh.UnifiedKonverter.Transfer;

namespace Prh.UnifiedKonverter.Ui
{
    /// <summary>
    /// Schritt 3 „Zu Unified übertragen“: Ziel-Unified-HMI wählen, Umfang festlegen, Vorschau prüfen,
    /// nach Bestätigung übertragen. Danach zeigt dieselbe Liste das Ergebnis je Objekt.
    /// </summary>
    internal sealed class TransferForm : Form
    {
        /// <summary>Führt den Schreibteil unter ExclusiveAccess und in einer Transaktion aus (Parameter: Abbruch-Abfrage).</summary>
        public delegate void WriteRunner(string description, Action<Func<bool>> body);

        private readonly SourceData _source;
        private readonly List<UnifiedTarget> _targets;
        private readonly WriteRunner _runWriting;
        private readonly string _sourceName;
        private readonly Theme _t = Theme.Current();

        private readonly ComboBox _target = new ComboBox();
        private readonly CategoryCard _cardTags, _cardLists, _cardAlarms;
        private readonly CheckBox _update = new CheckBox();
        private readonly TabStrip _filter;
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _status = new Label();
        private readonly PrButton _run;
        private List<TransferItem> _items = new List<TransferItem>();
        private bool _done;

        public TransferForm(string sourceName, SourceData source, List<UnifiedTarget> targets, WriteRunner runWriting)
        {
            _sourceName = sourceName;
            _source = source;
            _targets = targets;
            _runWriting = runWriting;

            Text = "Unified-Konverter · Zu Unified übertragen";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1280, 820);
            MinimumSize = new Size(980, 600);
            BackColor = _t.Bg;
            ForeColor = _t.Text;
            Font = Theme.Body;
            TopMost = true;
            Shown += (s, e) => { TopMost = false; Activate(); };
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            _cardTags = new CategoryCard(_t, "HMI-Variablen", true);
            _cardLists = new CategoryCard(_t, "Textlisten", true);
            _cardAlarms = new CategoryCard(_t, "Bitmeldungen", true);
            foreach (CategoryCard c in new[] { _cardTags, _cardLists, _cardAlarms }) c.CheckedChanged += (s, e) => Replan();
            _cardAlarms.ActionText = "Export-Datei wählen …";
            _cardAlarms.ActionClicked += (s, e) => ChooseAlarmExport();

            _filter = new TabStrip(_t, new[] { "Alle", "Neu", "Vorhanden", "Fehler" });
            _filter.SelectedChanged += (s, e) => FillGrid();
            _run = new PrButton(_t, PrButton.Style.Primary, "Übertragen");
            _run.Click += (s, e) => Run();

            Control content = BuildContent();
            Controls.Add(content);
            Controls.Add(BuildFooter());
            Controls.Add(BuildHeader());

            foreach (UnifiedTarget target in targets) _target.Items.Add(target);
            _target.SelectedIndexChanged += (s, e) => Replan();
            if (_target.Items.Count > 0) _target.SelectedIndex = 0;
            else Replan();
        }

        // ── Aufbau ──────────────────────────────────────────────────────────────

        private Control BuildHeader()
        {
            LinePanel header = new LinePanel(_t.Bg, _t.Border, DockStyle.Bottom) { Dock = DockStyle.Top, Height = 58, Padding = new Padding(24, 0, 24, 0) };
            PictureBox logo = new PictureBox { Image = Draw.Logo(_t.Text, 24, 1f), SizeMode = PictureBoxSizeMode.AutoSize, BackColor = _t.Bg };
            Label product = new Label { Text = "Unified-Konverter", Font = Theme.Product, ForeColor = _t.Text, AutoSize = true, BackColor = _t.Bg };
            Label step = new Label { Text = "Schritt 3 · Zu Unified übertragen", ForeColor = _t.Text3, AutoSize = true, BackColor = _t.Bg };
            Label right = new Label { Text = _sourceName + " · Comfort/Advanced", ForeColor = _t.Text2, AutoSize = true, BackColor = _t.Bg };
            header.Controls.AddRange(new Control[] { logo, product, step, right });
            header.Layout += (s, e) =>
            {
                int x = header.Padding.Left;
                logo.Location = new Point(x, (header.Height - logo.Height) / 2);
                x += logo.Width + 12;
                product.Location = new Point(x, (header.Height - product.Height) / 2);
                x += product.Width + 10;
                step.Location = new Point(x, (header.Height - step.Height) / 2 + 1);
                right.Location = new Point(header.Width - header.Padding.Right - right.Width, (header.Height - right.Height) / 2);
            };
            return header;
        }

        private Control BuildContent()
        {
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(32, 26, 32, 12), BackColor = _t.Bg };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label eyebrow = new Label { Text = "SCHRITT 3 · NACHBEARBEITUNG", Font = Theme.Eyebrow, ForeColor = _t.RedText, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            Label title = new Label { Text = "Zu Unified übertragen", Font = Theme.Title, ForeColor = _t.Text, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            Label lead = new Label
            {
                Text = "Überträgt die Objekte von „" + _sourceName + "“ in die gewählte Unified-HMI. Namen nach den Regeln des Converters. Erst nach Bestätigung wird geschrieben.",
                ForeColor = _t.Text2, AutoSize = true, MaximumSize = new Size(900, 0), Margin = new Padding(0, 0, 0, 18),
            };

            FlowLayoutPanel targetRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = _t.Bg, Margin = new Padding(0, 0, 0, 16) };
            Label targetLabel = new Label { Text = "Ziel-Unified-HMI", Font = Theme.Strong, ForeColor = _t.Text, AutoSize = true, Margin = new Padding(0, 7, 14, 0) };
            _target.DropDownStyle = ComboBoxStyle.DropDownList;
            _target.FlatStyle = FlatStyle.Flat;
            _target.BackColor = _t.Bg;
            _target.ForeColor = _t.Text;
            _target.Font = Theme.Body;
            _target.Width = 380;
            _target.Margin = new Padding(0, 2, 24, 0);
            _update.Text = "Vorhandene Objekte aktualisieren";
            _update.ForeColor = _t.Text2;
            _update.AutoSize = true;
            _update.FlatStyle = FlatStyle.Flat;
            _update.Margin = new Padding(0, 6, 0, 0);
            _update.CheckedChanged += (s, e) => Replan();
            targetRow.Controls.Add(targetLabel);
            targetRow.Controls.Add(_target);
            targetRow.Controls.Add(_update);

            FlowLayoutPanel cards = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = _t.Bg, Margin = new Padding(0, 0, 0, 14) };
            cards.Controls.Add(_cardTags);
            cards.Controls.Add(_cardLists);
            cards.Controls.Add(_cardAlarms);

            _filter.Dock = DockStyle.Bottom;
            Panel filterRow = new Panel { Dock = DockStyle.Fill, BackColor = _t.Bg, Margin = new Padding(0) };
            filterRow.Controls.Add(_filter);

            SetupGrid();
            Panel gridHost = new Panel { Dock = DockStyle.Fill, BackColor = _t.Bg, Margin = new Padding(0, 4, 0, 0) };
            gridHost.Controls.Add(_grid);

            layout.Controls.Add(eyebrow, 0, 0);
            layout.Controls.Add(title, 0, 1);
            layout.Controls.Add(lead, 0, 2);
            layout.Controls.Add(targetRow, 0, 3);
            layout.Controls.Add(cards, 0, 4);
            layout.Controls.Add(filterRow, 0, 5);
            layout.Controls.Add(gridHost, 0, 6);
            return layout;
        }

        private Control BuildFooter()
        {
            LinePanel footer = new LinePanel(_t.Bg, _t.Border, DockStyle.Top) { Dock = DockStyle.Bottom, Height = 66, Padding = new Padding(24, 0, 24, 0) };
            PictureBox logo = new PictureBox { Image = Draw.Logo(_t.Text, 18, 0.55f), SizeMode = PictureBoxSizeMode.AutoSize, BackColor = _t.Bg };
            Label author = new Label { Text = "Paul-Rafael Hasan-Man", Font = Theme.Small, ForeColor = _t.Text3, AutoSize = true, BackColor = _t.Bg };
            _status.Font = Theme.Small; _status.ForeColor = _t.Text2; _status.AutoSize = true; _status.BackColor = _t.Bg;
            PrButton report = new PrButton(_t, PrButton.Style.Ghost, "Liste speichern");
            report.Click += (s, e) => SaveList();
            PrButton close = new PrButton(_t, PrButton.Style.Ghost, "Schließen");
            close.Click += (s, e) => Close();
            footer.Controls.AddRange(new Control[] { logo, author, _status, close, report, _run });
            footer.Layout += (s, e) =>
            {
                int cy = footer.Height / 2;
                logo.Location = new Point(footer.Padding.Left, cy - logo.Height / 2);
                author.Location = new Point(logo.Right + 8, cy - author.Height / 2);
                _status.Location = new Point(author.Right + 24, cy - _status.Height / 2);
                int x = footer.Width - footer.Padding.Right;
                foreach (PrButton b in new[] { _run, report, close })
                {
                    x -= b.Width;
                    b.Location = new Point(x, cy - b.Height / 2);
                    x -= 10;
                }
            };
            return footer;
        }

        private void SetupGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _t.StyleGrid(_grid);
            _grid.Columns.Add("kind", "ART");
            _grid.Columns.Add("source", "ALTE HMI");
            _grid.Columns.Add("target", "NAME IN UNIFIED");
            _grid.Columns.Add("container", "TABELLE / KLASSE");
            _grid.Columns.Add("state", "STATUS");
            _grid.Columns.Add("notes", "HINWEISE");
            _grid.Columns["kind"].FillWeight = 9;
            _grid.Columns["source"].FillWeight = 20;
            _grid.Columns["target"].FillWeight = 20;
            _grid.Columns["container"].FillWeight = 13;
            _grid.Columns["state"].FillWeight = 10;
            _grid.Columns["notes"].FillWeight = 34;
            _grid.Columns["source"].DefaultCellStyle.Font = Theme.Mono;
            _grid.Columns["target"].DefaultCellStyle.Font = Theme.Mono;
            _grid.Columns["notes"].DefaultCellStyle.ForeColor = _t.Text2;
        }

        // ── Logik ───────────────────────────────────────────────────────────────

        private TransferOptions Options()
        {
            return new TransferOptions { Tags = _cardTags.Checked, TextLists = _cardLists.Checked, Alarms = _cardAlarms.Checked, UpdateExisting = _update.Checked };
        }

        private void Replan()
        {
            if (_done) return;
            UnifiedTarget target = _target.SelectedItem as UnifiedTarget;
            List<TransferItem> all = new List<TransferItem>();
            if (target != null)
            {
                try
                {
                    UseWaitCursor = true;
                    all = new UnifiedTransfer(_source, target).Plan(new TransferOptions { UpdateExisting = _update.Checked });
                }
                catch (Exception ex)
                {
                    MessageForm.ShowError("Ziel nicht lesbar", ex.ToString());
                }
                finally
                {
                    UseWaitCursor = false;
                }
            }

            UpdateCard(_cardTags, all, TransferKind.Variable, _source.Tags == null ? null : _source.Tags.Tables.Count + " Tabellen der alten HMI");
            UpdateCard(_cardLists, all, TransferKind.Textliste, _source.TextListSource);
            UpdateCard(_cardAlarms, all, TransferKind.Meldung, _source.AlarmSource);

            TransferOptions o = Options();
            _items = all.Where(i => (i.Kind == TransferKind.Variable && o.Tags) || (i.Kind == TransferKind.Textliste && o.TextLists) || (i.Kind == TransferKind.Meldung && o.Alarms)).ToList();
            int writes = _items.Count(i => i.State == TransferState.Neu || (i.State == TransferState.Vorhanden && o.UpdateExisting));
            _run.Text = writes > 0 ? "Übertragen (" + writes.ToString("N0") + ")" : "Übertragen";
            _run.Enabled = target != null && writes > 0;
            _run.Parent?.PerformLayout();
            _status.Text = target == null ? "Keine Unified-HMI im Projekt gefunden – bitte zuerst mit Data2Unified anlegen." : "";
            _status.ForeColor = target == null ? _t.RedText : _t.Text2;
            FillGrid();
        }

        private static void UpdateCard(CategoryCard card, List<TransferItem> all, TransferKind kind, string source)
        {
            List<TransferItem> items = all.Where(i => i.Kind == kind).ToList();
            int neu = items.Count(i => i.State == TransferState.Neu);
            int vorhanden = items.Count(i => i.State == TransferState.Vorhanden);
            int skip = items.Count(i => i.State == TransferState.Übersprungen);
            card.Summary = items.Count == 0 ? "nichts zu übertragen" : neu.ToString("N0") + " neu · " + vorhanden.ToString("N0") + " vorhanden" + (skip > 0 ? " · " + skip + " gesperrt" : "");
            card.Note = source ?? "";
            card.Invalidate();
        }

        private void FillGrid()
        {
            IEnumerable<TransferItem> items = _items;
            switch (_filter.Selected)
            {
                case 1: items = items.Where(i => i.State == TransferState.Neu || i.State == TransferState.Angelegt); break;
                case 2: items = items.Where(i => i.State == TransferState.Vorhanden || i.State == TransferState.Aktualisiert || i.State == TransferState.Übersprungen); break;
                case 3: items = items.Where(i => i.State == TransferState.Fehler || i.Notes.Count > 0); break;
            }
            _filter.SetText(3, "Fehler und Hinweise  " + _items.Count(i => i.State == TransferState.Fehler || i.Notes.Count > 0));

            _grid.SuspendLayout();
            _grid.Rows.Clear();
            List<DataGridViewRow> rows = new List<DataGridViewRow>();
            foreach (TransferItem i in items.Take(20000))
            {
                DataGridViewRow row = new DataGridViewRow();
                row.CreateCells(_grid, KindText(i.Kind), i.SourceName, i.TargetName, i.Container ?? "", i.StateText, string.Join(" · ", i.Notes));
                if (i.TargetName != i.SourceName) row.Cells[2].Style.ForeColor = _t.RedText;
                if (i.State == TransferState.Fehler) { row.Cells[4].Style.ForeColor = _t.RedText; row.Cells[4].Style.Font = Theme.Strong; }
                else if (i.State == TransferState.Angelegt || i.State == TransferState.Aktualisiert) row.Cells[4].Style.Font = Theme.Strong;
                else if (i.State == TransferState.Vorhanden || i.State == TransferState.Übersprungen) row.Cells[4].Style.ForeColor = _t.Text3;
                rows.Add(row);
            }
            _grid.Rows.AddRange(rows.ToArray());
            _grid.ResumeLayout();
        }

        private static string KindText(TransferKind kind)
        {
            switch (kind)
            {
                case TransferKind.Variable: return "Variable";
                case TransferKind.Textliste: return "Textliste";
                default: return "Meldung";
            }
        }

        private void ChooseAlarmExport()
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "TIA-Export der Bitmeldungen wählen",
                Filter = "Excel-Arbeitsmappe (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SourceReader.ReadAlarmsFromExcel(dlg.FileName, _source);
                    _cardAlarms.Checked = true;
                    Replan();
                }
                catch (Exception ex)
                {
                    MessageForm.ShowError("Export-Datei nicht lesbar", ex.Message);
                }
            }
        }

        private void Run()
        {
            UnifiedTarget target = _target.SelectedItem as UnifiedTarget;
            if (target == null) return;
            TransferOptions options = Options();
            int neu = _items.Count(i => i.State == TransferState.Neu);
            int update = options.UpdateExisting ? _items.Count(i => i.State == TransferState.Vorhanden) : 0;

            StringBuilder text = new StringBuilder();
            text.AppendLine("Ziel: " + target);
            text.AppendLine();
            foreach (TransferKind kind in new[] { TransferKind.Variable, TransferKind.Textliste, TransferKind.Meldung })
            {
                int n = _items.Count(i => i.Kind == kind && i.State == TransferState.Neu);
                int u = options.UpdateExisting ? _items.Count(i => i.Kind == kind && i.State == TransferState.Vorhanden) : 0;
                if (n + u > 0) text.AppendLine("  " + KindText(kind) + ": " + n.ToString("N0") + " anlegen" + (u > 0 ? ", " + u.ToString("N0") + " aktualisieren" : ""));
            }
            text.AppendLine();
            text.AppendLine("Alles läuft in einem Schritt und lässt sich in TIA mit „Rückgängig“ zurücknehmen. Bitte vorher das Projekt sichern bzw. archivieren.");
            if (!MessageForm.Confirm("Jetzt übertragen?", text.ToString(), "Jetzt übertragen")) return;

            List<TransferItem> items = _items;
            try
            {
                UseWaitCursor = true;
                _runWriting("Zu Unified übertragen (" + (neu + update) + " Objekte)", cancelled => new UnifiedTransfer(_source, target).Execute(items, options, cancelled));
            }
            catch (Exception ex)
            {
                MessageForm.ShowError("Übertragen abgebrochen", ex.ToString());
            }
            finally
            {
                UseWaitCursor = false;
            }

            _done = true;
            _run.Enabled = false;
            _target.Enabled = false;
            int ok = items.Count(i => i.State == TransferState.Angelegt || i.State == TransferState.Aktualisiert);
            int failed = items.Count(i => i.State == TransferState.Fehler);
            int notes = items.Count(i => i.State != TransferState.Fehler && i.Notes.Count > 0);
            _status.Text = ok.ToString("N0") + " übertragen" + (failed > 0 ? " · " + failed + " Fehler" : "") + (notes > 0 ? " · " + notes + " mit Hinweisen" : "");
            _status.ForeColor = failed > 0 ? _t.RedText : _t.Text2;
            _filter.SetText(1, "Angelegt");
            FillGrid();
        }

        private void SaveList()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Unified-Konverter", "Berichte", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Art;Alte HMI;Name in Unified;Tabelle/Klasse;Status;Hinweise");
                foreach (TransferItem i in _items)
                {
                    sb.AppendLine(string.Join(";", new[] { KindText(i.Kind), i.SourceName, i.TargetName, i.Container, i.StateText, string.Join(" · ", i.Notes) }
                        .Select(v => v == null ? "" : v.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v)));
                }
                File.WriteAllText(Path.Combine(dir, "Uebertragung.csv"), sb.ToString(), new UTF8Encoding(true));
                _status.Text = "Liste gespeichert – Ordner wird geöffnet";
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                _status.Text = "Speichern fehlgeschlagen: " + ex.Message;
                _status.ForeColor = _t.RedText;
            }
        }
    }

    /// <summary>Auswahlkarte für einen Objekttyp: Häkchen, Titel, Anzahl, Herkunft, optional ein Text-Link.</summary>
    internal sealed class CategoryCard : Control
    {
        private readonly Theme _t;
        private bool _checked;
        private Rectangle _actionBounds;
        public string Title;
        public string Summary = "";
        public string Note = "";
        public string ActionText;
        public event EventHandler CheckedChanged;
        public event EventHandler ActionClicked;

        public CategoryCard(Theme theme, string title, bool isChecked)
        {
            _t = theme;
            Title = title;
            _checked = isChecked;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(336, 104);
            Margin = new Padding(0, 0, 14, 0);
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (ActionText != null && _actionBounds.Contains(e.Location))
            {
                if (ActionClicked != null) ActionClicked(this, EventArgs.Empty);
                return;
            }
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : _t.Bg);
            RectangleF r = new RectangleF(1.5f, 1.5f, Width - 3.5f, Height - 3.5f);
            if (_checked)
            {
                using (GraphicsPath ring = Draw.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 11))
                using (Pen p = new Pen(Draw.Mix(_t.Bg, _t.Red, 0.25f), 3f)) g.DrawPath(p, ring);
            }
            using (GraphicsPath path = Draw.Rounded(r, 10))
            using (SolidBrush b = new SolidBrush(_t.Bg))
            using (Pen p = new Pen(_checked ? _t.Red : _t.Border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }

            // Häkchen-Kreis oben rechts (wie Auswahlkarte im Design Kit)
            Rectangle box = new Rectangle(Width - 36, 16, 18, 18);
            if (_checked)
            {
                using (SolidBrush red = new SolidBrush(_t.Red)) g.FillEllipse(red, box);
                using (Pen white = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLines(white, new[] { new Point(box.X + 5, box.Y + 9), new Point(box.X + 8, box.Y + 12), new Point(box.X + 13, box.Y + 6) });
                }
            }
            else
            {
                using (Pen p = new Pen(_t.BorderStrong, 1.2f)) g.DrawEllipse(p, box);
            }

            TextRenderer.DrawText(g, Title, Theme.Strong, new Point(18, 14), _t.Text, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Summary, Theme.Body, new Point(18, 38), _checked ? _t.Text2 : _t.Text3, TextFormatFlags.NoPadding);
            Rectangle noteRect = new Rectangle(18, 62, Width - 36, 18);
            TextRenderer.DrawText(g, Note, Theme.Small, noteRect, _t.Text3, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            if (ActionText != null)
            {
                Size s = TextRenderer.MeasureText(ActionText, Theme.Strong);
                _actionBounds = new Rectangle(18, 80, s.Width, 18);
                TextRenderer.DrawText(g, ActionText, Theme.Strong, new Point(18, 80), _t.RedText, TextFormatFlags.NoPadding);
            }
        }
    }
}
