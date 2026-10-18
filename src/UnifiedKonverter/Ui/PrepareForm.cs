using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Prh.UnifiedKonverter.Openness;
using Prh.UnifiedKonverter.Report;
using Prh.UnifiedKonverter.Rules;

namespace Prh.UnifiedKonverter.Ui
{
    /// <summary>
    /// Schritt 1 „Für Unified vorbereiten“: links die Variablentabellen des HMI-Geräts, rechts Kennzahlen,
    /// Filter und Variablenliste mit Befunden. Umbenennen erst nach Bestätigung.
    /// </summary>
    internal sealed class PrepareForm : Form
    {
        private readonly ReadResult _result;
        private readonly Func<IList<HmiTagInfo>, List<RenameOutcome>> _rename;
        private readonly Theme _t = Theme.Current();

        private readonly ListBox _tables = new ListBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _eyebrow = new Label();
        private readonly Label _title = new Label();
        private readonly Label _lead = new Label();
        private readonly Label _status = new Label();
        private readonly StatTile _tileTags, _tileRename, _tileCollision, _tileOther;
        private readonly TabStrip _filter;
        private readonly SearchBox _search;
        private readonly PrButton _apply;
        private string _reportDir;

        private sealed class TableItem
        {
            public HmiTableInfo Table; // null = alle Tabellen
        }

        public PrepareForm(ReadResult result, Func<IList<HmiTagInfo>, List<RenameOutcome>> rename)
        {
            _result = result;
            _rename = rename;

            Text = "Unified-Konverter · Für Unified vorbereiten";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1280, 800);
            MinimumSize = new Size(960, 560);
            BackColor = _t.Bg;
            ForeColor = _t.Text;
            Font = Theme.Body;
            TopMost = true; // Add-In-Fenster gehören nicht zum TIA-Hauptfenster – sonst landen sie dahinter.
            Shown += (s, e) => { TopMost = false; Activate(); };
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            _tileTags = new StatTile(_t, "Variablen", false);
            _tileRename = new StatTile(_t, "werden umbenannt", true);
            _tileCollision = new StatTile(_t, "Kollisionen", true);
            _tileOther = new StatTile(_t, "weitere Hinweise", true);
            _filter = new TabStrip(_t, new[] { "Alle", "Mit Hinweisen", "Umbenennen" });
            _filter.SelectedChanged += (s, e) => FillGrid();
            _search = new SearchBox(_t, "Variable suchen …") { Anchor = AnchorStyles.Right | AnchorStyles.Top };
            _search.SearchChanged += (s, e) => FillGrid();
            _apply = new PrButton(_t, PrButton.Style.Primary, "Umbenennen übernehmen");
            _apply.Click += (s, e) => ApplyRenames();

            Control content = BuildContent();
            Control sidebar = BuildSidebar();
            Control footer = BuildFooter();
            Control header = BuildHeader();
            // Reihenfolge wichtig: zuletzt hinzugefügt = zuerst angedockt (Kopf/Fuß über volle Breite).
            Controls.Add(content);
            Controls.Add(sidebar);
            Controls.Add(footer);
            Controls.Add(header);

            FillTables();
        }

        // ── Aufbau ──────────────────────────────────────────────────────────────

        private Control BuildHeader()
        {
            LinePanel header = new LinePanel(_t.Bg, _t.Border, DockStyle.Bottom) { Dock = DockStyle.Top, Height = 58, Padding = new Padding(24, 0, 24, 0) };
            PictureBox logo = new PictureBox { Image = Draw.Logo(_t.Text, 24, 1f), SizeMode = PictureBoxSizeMode.AutoSize, BackColor = _t.Bg };
            Label product = new Label { Text = "Unified-Konverter", Font = Theme.Product, ForeColor = _t.Text, AutoSize = true, BackColor = _t.Bg };
            Label step = new Label { Text = "Schritt 1 · Für Unified vorbereiten", ForeColor = _t.Text3, AutoSize = true, BackColor = _t.Bg };

            HmiDeviceInfo device = _result.Devices.FirstOrDefault();
            string deviceText = _result.Devices.Count == 1
                ? device.Name + " · " + device.KindText
                : _result.Devices.Count + " HMI-Geräte";
            Label right = new Label { Text = deviceText, ForeColor = _t.Text2, AutoSize = true, BackColor = _t.Bg };

            header.Controls.Add(logo);
            header.Controls.Add(product);
            header.Controls.Add(step);
            header.Controls.Add(right);
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

        private Control BuildSidebar()
        {
            LinePanel side = new LinePanel(_t.Surface2, _t.Border, DockStyle.Right) { Dock = DockStyle.Left, Width = 300, Padding = new Padding(16, 20, 12, 16) };
            Label eyebrow = new Label { Text = "VARIABLENTABELLEN", Font = Theme.Eyebrow, ForeColor = _t.Text3, AutoSize = false, Height = 26, Dock = DockStyle.Top, BackColor = _t.Surface2, Padding = new Padding(8, 0, 0, 0) };

            _tables.Dock = DockStyle.Fill;
            _tables.BorderStyle = BorderStyle.None;
            _tables.BackColor = _t.Surface2;
            _tables.ForeColor = _t.Text;
            _tables.DrawMode = DrawMode.OwnerDrawFixed;
            _tables.ItemHeight = 50;
            _tables.IntegralHeight = false;
            _tables.DrawItem += DrawTableItem;
            _tables.SelectedIndexChanged += (s, e) => ShowSelection();
            _tables.MouseMove += (s, e) => _tables.Cursor = _tables.IndexFromPoint(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;

            side.Controls.Add(_tables);
            side.Controls.Add(eyebrow);
            return side;
        }

        private Control BuildContent()
        {
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(32, 26, 32, 12), BackColor = _t.Bg };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Eyebrow
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Titel
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Lead
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Kennzahlen
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));   // Filter + Suche
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Liste

            _eyebrow.Font = Theme.Eyebrow; _eyebrow.ForeColor = _t.RedText; _eyebrow.AutoSize = true; _eyebrow.Margin = new Padding(0, 0, 0, 6);
            _title.Font = Theme.Title; _title.ForeColor = _t.Text; _title.AutoSize = true; _title.Margin = new Padding(0, 0, 0, 6);
            _lead.ForeColor = _t.Text2; _lead.AutoSize = true; _lead.Margin = new Padding(0, 0, 0, 18);

            FlowLayoutPanel tiles = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = _t.Bg, Margin = new Padding(0, 0, 0, 18) };
            tiles.Controls.Add(_tileTags);
            tiles.Controls.Add(_tileRename);
            tiles.Controls.Add(_tileCollision);
            tiles.Controls.Add(_tileOther);

            Panel filterRow = new Panel { Dock = DockStyle.Fill, BackColor = _t.Bg, Margin = new Padding(0) };
            _filter.Height = 36;
            filterRow.Controls.Add(_search);
            filterRow.Controls.Add(_filter);
            filterRow.Layout += (s, e) =>
            {
                _search.Location = new Point(filterRow.Width - _search.Width, 0);
                _filter.SetBounds(0, filterRow.Height - _filter.Height - 6, filterRow.Width - _search.Width - 16, _filter.Height);
            };

            SetupGrid();
            Panel gridHost = new Panel { Dock = DockStyle.Fill, BackColor = _t.Bg, Margin = new Padding(0, 4, 0, 0) };
            gridHost.Controls.Add(_grid);

            layout.Controls.Add(_eyebrow, 0, 0);
            layout.Controls.Add(_title, 0, 1);
            layout.Controls.Add(_lead, 0, 2);
            layout.Controls.Add(tiles, 0, 3);
            layout.Controls.Add(filterRow, 0, 4);
            layout.Controls.Add(gridHost, 0, 5);
            return layout;
        }

        private Control BuildFooter()
        {
            LinePanel footer = new LinePanel(_t.Bg, _t.Border, DockStyle.Top) { Dock = DockStyle.Bottom, Height = 66, Padding = new Padding(24, 0, 24, 0) };
            PictureBox logo = new PictureBox { Image = Draw.Logo(_t.Text, 18, 0.55f), SizeMode = PictureBoxSizeMode.AutoSize, BackColor = _t.Bg };
            Label author = new Label { Text = "Paul-Rafael Hasan-Man", Font = Theme.Small, ForeColor = _t.Text3, AutoSize = true, BackColor = _t.Bg };
            _status.Font = Theme.Small; _status.ForeColor = _t.Text2; _status.AutoSize = true; _status.BackColor = _t.Bg;

            PrButton report = new PrButton(_t, PrButton.Style.Ghost, "Bericht speichern");
            report.Click += (s, e) => SaveReport();
            PrButton close = new PrButton(_t, PrButton.Style.Ghost, "Schließen");
            close.Click += (s, e) => Close();

            footer.Controls.AddRange(new Control[] { logo, author, _status, close, report, _apply });
            footer.Layout += (s, e) =>
            {
                int cy = footer.Height / 2;
                logo.Location = new Point(footer.Padding.Left, cy - logo.Height / 2);
                author.Location = new Point(logo.Right + 8, cy - author.Height / 2);
                _status.Location = new Point(author.Right + 24, cy - _status.Height / 2);
                int x = footer.Width - footer.Padding.Right;
                foreach (PrButton b in new[] { _apply, report, close })
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
            _grid.MultiSelect = true;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _t.StyleGrid(_grid);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "state", HeaderText = "", FillWeight = 4, MinimumWidth = 28, Resizable = DataGridViewTriState.False });
            _grid.Columns.Add("name", "NAME");
            _grid.Columns.Add("unified", "NAME IN UNIFIED");
            _grid.Columns.Add("table", "TABELLE");
            _grid.Columns.Add("type", "DATENTYP");
            _grid.Columns.Add("cycle", "ERFASSUNGSZYKLUS");
            _grid.Columns.Add("mode", "ERFASSUNGSART");
            _grid.Columns.Add("plc", "PLC-VARIABLE");
            _grid.Columns.Add("findings", "HINWEISE");
            _grid.Columns["name"].FillWeight = 18;
            _grid.Columns["unified"].FillWeight = 18;
            _grid.Columns["table"].FillWeight = 11;
            _grid.Columns["type"].FillWeight = 8;
            _grid.Columns["cycle"].FillWeight = 9;
            _grid.Columns["mode"].FillWeight = 10;
            _grid.Columns["plc"].FillWeight = 16;
            _grid.Columns["findings"].FillWeight = 30;
            _grid.Columns["name"].DefaultCellStyle.Font = Theme.Mono;
            _grid.Columns["unified"].DefaultCellStyle.Font = Theme.Mono;
            _grid.Columns["plc"].DefaultCellStyle.ForeColor = _t.Text2;
            _grid.Columns["findings"].DefaultCellStyle.ForeColor = _t.Text2;
            _grid.CellPainting += PaintStateCell;
        }

        // ── Daten ───────────────────────────────────────────────────────────────

        private void FillTables()
        {
            _tables.BeginUpdate();
            _tables.Items.Clear();
            if (_result.Tables.Count > 1) _tables.Items.Add(new TableItem());
            foreach (HmiTableInfo table in _result.Tables.OrderBy(t => t.Folder).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
            {
                _tables.Items.Add(new TableItem { Table = table });
            }
            _tables.EndUpdate();
            if (_tables.Items.Count > 0) _tables.SelectedIndex = 0;
        }

        private IEnumerable<HmiTagInfo> SelectedTags()
        {
            TableItem item = _tables.SelectedItem as TableItem;
            return item == null || item.Table == null ? _result.AllTags : item.Table.Tags;
        }

        private void ShowSelection()
        {
            TableItem item = _tables.SelectedItem as TableItem;
            List<HmiTagInfo> tags = SelectedTags().ToList();
            if (item == null || item.Table == null)
            {
                _eyebrow.Text = "ALLE VARIABLENTABELLEN";
                _title.Text = _result.Devices.Count == 1 ? _result.Devices[0].Name : "Alle Geräte";
                _lead.Text = _result.Tables.Count + " Tabellen · " + tags.Count.ToString("N0") + " Variablen · gelesen in "
                    + _result.Duration.TotalSeconds.ToString("0.0") + " s · am Projekt wurde nichts geändert";
            }
            else
            {
                _eyebrow.Text = "VARIABLENTABELLE";
                _title.Text = item.Table.Name;
                _lead.Text = (string.IsNullOrEmpty(item.Table.Folder) ? "" : item.Table.Folder + " · ") + tags.Count.ToString("N0") + " Variablen"
                    + (item.Table.Problems.Count > 0 ? " · " + item.Table.Problems.Count + " nicht lesbar" : "");
            }

            int rename = TagRenamer.Candidates(tags).Count;
            int collisions = tags.Count(t => t.Findings.Any(f => f.Kind == FindingKind.Kollision));
            int other = tags.Count(t => t.Findings.Any(f => f.Kind != FindingKind.Kollision && f.Kind != FindingKind.Sonderzeichen));
            _tileTags.SetValue(tags.Count);
            _tileRename.SetValue(rename);
            _tileCollision.SetValue(collisions);
            _tileOther.SetValue(other);

            _filter.SetText(1, "Mit Hinweisen  " + tags.Count(t => t.Findings.Count > 0));
            _filter.SetText(2, "Umbenennen  " + tags.Count(t => t.IsRenamed));

            int allRename = TagRenamer.Candidates(_result.AllTags).Count;
            _apply.Text = allRename > 0 ? "Umbenennen übernehmen (" + allRename.ToString("N0") + ")" : "Umbenennen übernehmen";
            _apply.Enabled = allRename > 0;
            _apply.Parent?.PerformLayout();

            FillGrid();
        }

        private void FillGrid()
        {
            string q = _search.Query;
            IEnumerable<HmiTagInfo> tags = SelectedTags();
            if (_filter.Selected == 1) tags = tags.Where(t => t.Findings.Count > 0);
            if (_filter.Selected == 2) tags = tags.Where(t => t.IsRenamed);
            if (q.Length > 0)
            {
                tags = tags.Where(t => t.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (t.UnifiedName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _grid.SuspendLayout();
            _grid.Rows.Clear();
            List<DataGridViewRow> rows = new List<DataGridViewRow>();
            foreach (HmiTagInfo t in tags.OrderBy(x => x.HasWarning ? 0 : x.Findings.Count > 0 ? 1 : 2).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                DataGridViewRow row = new DataGridViewRow();
                row.CreateCells(_grid, "", t.Name, t.IsRenamed ? t.UnifiedName : "unverändert", t.Table.Name, Dash(t.DataType),
                    Dash(t.AcquisitionCycle), Dash(t.AcquisitionMode), Dash(t.PlcTag), string.Join(" · ", t.Findings.Select(f => f.Text)));
                row.Tag = t;
                DataGridViewCell unified = row.Cells[2];
                if (t.IsRenamed)
                {
                    bool blocked = t.Findings.Any(f => f.Kind == FindingKind.Kollision);
                    unified.Style.ForeColor = _t.RedText;
                    unified.Style.Font = blocked ? Theme.Mono : Theme.MonoStrong;
                }
                else
                {
                    unified.Style.ForeColor = _t.Text3;
                    unified.Style.Font = Theme.Small;
                }
                rows.Add(row);
            }
            _grid.Rows.AddRange(rows.ToArray());
            _grid.Columns["table"].Visible = !(_tables.SelectedItem is TableItem) || ((TableItem)_tables.SelectedItem).Table == null;
            _grid.ResumeLayout();
        }

        private static string Dash(string s)
        {
            return string.IsNullOrEmpty(s) ? "–" : s;
        }

        // ── Zeichnen ────────────────────────────────────────────────────────────

        private void DrawTableItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            TableItem item = (TableItem)_tables.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (SolidBrush bg = new SolidBrush(_t.Surface2)) g.FillRectangle(bg, e.Bounds);
            Rectangle card = new Rectangle(e.Bounds.X, e.Bounds.Y + 2, e.Bounds.Width - 2, e.Bounds.Height - 4);
            if (selected)
            {
                using (System.Drawing.Drawing2D.GraphicsPath path = Draw.Rounded(card, 8))
                using (SolidBrush b = new SolidBrush(_t.Bg))
                using (Pen p = new Pen(_t.Border))
                {
                    g.FillPath(b, path);
                    g.DrawPath(p, path);
                }
                using (SolidBrush red = new SolidBrush(_t.Red)) g.FillRectangle(red, card.X, card.Y + 10, 2, card.Height - 20);
            }

            string title = item.Table == null ? "Alle Tabellen" : item.Table.Name;
            int count = item.Table == null ? _result.AllTags.Count() : item.Table.Tags.Count;
            int warnings = item.Table == null ? _result.AllTags.Count(t => t.HasWarning) : item.Table.Warnings;
            string sub = warnings > 0 ? warnings + (warnings == 1 ? " Variable mit Warnung" : " Variablen mit Warnung") : "keine Warnungen";

            Rectangle text = new Rectangle(card.X + 12, card.Y + 6, card.Width - 70, 20);
            TextRenderer.DrawText(g, title, selected ? Theme.Strong : Theme.Body, text, _t.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(text.X, text.Y + 21, text.Width, 18), warnings > 0 ? _t.RedText : _t.Text3, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            string countText = count.ToString("N0");
            Size size = TextRenderer.MeasureText(countText, Theme.Small);
            Rectangle badge = new Rectangle(card.Right - size.Width - 22, card.Y + (card.Height - 22) / 2, size.Width + 12, 22);
            using (System.Drawing.Drawing2D.GraphicsPath path = Draw.Rounded(badge, 11))
            using (SolidBrush b = new SolidBrush(selected ? _t.InkSoft : _t.Bg))
            {
                g.FillPath(b, path);
            }
            TextRenderer.DrawText(g, countText, Theme.Small, badge, _t.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void PaintStateCell(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
            e.PaintBackground(e.CellBounds, true);
            HmiTagInfo tag = _grid.Rows[e.RowIndex].Tag as HmiTagInfo;
            if (tag != null && tag.Findings.Count > 0)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int d = 8;
                Rectangle r = new Rectangle(e.CellBounds.X + (e.CellBounds.Width - d) / 2, e.CellBounds.Y + (e.CellBounds.Height - d) / 2, d, d);
                if (tag.HasWarning)
                {
                    using (SolidBrush b = new SolidBrush(_t.Red)) e.Graphics.FillEllipse(b, r);
                }
                else
                {
                    using (Pen p = new Pen(_t.Ink, 1.5f)) e.Graphics.DrawEllipse(p, r);
                }
            }
            e.Handled = true;
        }

        // ── Aktionen ────────────────────────────────────────────────────────────

        private void ApplyRenames()
        {
            List<HmiTagInfo> candidates = TagRenamer.Candidates(_result.AllTags);
            if (candidates.Count == 0) return;
            int blocked = _result.AllTags.Count(t => t.IsRenamed && t.Findings.Any(f => f.Kind == FindingKind.Kollision));

            StringBuilder text = new StringBuilder();
            text.AppendLine(candidates.Count.ToString("N0") + " Variablen werden nach der Regel des Converters umbenannt (jedes Zeichen außer A–Z, a–z, 0–9 wird zu „_“).");
            if (blocked > 0) text.AppendLine().AppendLine(blocked + " Variablen mit Kollision werden ausgelassen – bitte vorher von Hand eindeutig benennen.");
            text.AppendLine().AppendLine("Alles läuft in einem Schritt und lässt sich in TIA mit „Rückgängig“ zurücknehmen. Bitte trotzdem vorher das Projekt sichern bzw. archivieren.");
            text.AppendLine().AppendLine("Beispiele:");
            foreach (HmiTagInfo t in candidates.Take(5)) text.AppendLine("  " + t.Name + "  →  " + t.UnifiedName);
            if (candidates.Count > 5) text.AppendLine("  …");

            if (!MessageForm.Confirm("Umbenennen übernehmen?", text.ToString(), "Jetzt umbenennen")) return;

            List<RenameOutcome> outcomes;
            try
            {
                UseWaitCursor = true;
                outcomes = _rename(candidates);
            }
            catch (Exception ex)
            {
                MessageForm.ShowError("Umbenennen fehlgeschlagen", ex.ToString());
                return;
            }
            finally
            {
                UseWaitCursor = false;
            }

            ProjectChecks.Run(_result);
            _tables.Invalidate();
            ShowSelection();

            int ok = outcomes.Count(o => o.Success);
            List<RenameOutcome> failed = outcomes.Where(o => !o.Success).ToList();
            _status.Text = ok.ToString("N0") + " Variablen umbenannt" + (failed.Count > 0 ? ", " + failed.Count + " fehlgeschlagen" : "");
            _status.ForeColor = failed.Count > 0 ? _t.RedText : _t.Text2;
            if (failed.Count > 0)
            {
                StringBuilder details = new StringBuilder();
                foreach (RenameOutcome o in failed.Take(30)) details.AppendLine(o.OldName + " → " + o.NewName + ": " + o.Error);
                if (failed.Count > 30) details.AppendLine("…");
                MessageForm.ShowError(failed.Count + " Variablen nicht umbenannt", details.ToString());
            }
        }

        private void SaveReport()
        {
            try
            {
                _reportDir = ReportWriter.Write(_result);
                _status.Text = "Bericht gespeichert – Ordner wird geöffnet";
                _status.ForeColor = _t.Text2;
                Process.Start("explorer.exe", "\"" + _reportDir + "\"");
            }
            catch (Exception ex)
            {
                _status.Text = "Speichern fehlgeschlagen: " + ex.Message;
                _status.ForeColor = _t.RedText;
            }
        }
    }
}
