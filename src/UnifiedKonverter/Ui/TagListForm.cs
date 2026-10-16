using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Prh.UnifiedKonverter.Openness;
using Prh.UnifiedKonverter.Report;

namespace Prh.UnifiedKonverter.Ui
{
    /// <summary>
    /// Ergebnisfenster des Machbarkeitstests: Reiter „Variablen“ (wichtigste Spalten) und
    /// „Attribute“ (alle Openness-Attribute mit Zugriffsart). Bericht als CSV speicherbar.
    /// </summary>
    internal sealed class TagListForm : Form
    {
        private readonly IList<TagTableSnapshot> _snapshots;
        private readonly Theme _t = Theme.Current();
        private readonly DataGridView _tagsGrid = new DataGridView();
        private readonly DataGridView _attrGrid = new DataGridView();
        private readonly TabStrip _tabs;
        private readonly Label _status = new Label();
        private readonly Button _openFolder;
        private string _reportDir;

        public TagListForm(IList<TagTableSnapshot> snapshots)
        {
            _snapshots = snapshots;

            Text = "Unified-Konverter · Variablen";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 680);
            MinimumSize = new Size(720, 420);
            BackColor = _t.Bg;
            ForeColor = _t.Text;
            Font = Theme.Body;
            TopMost = true; // Add-In-Fenster gehören nicht zum TIA-Hauptfenster – sonst landen sie dahinter.
            Shown += (s, e) => { TopMost = false; Activate(); };

            int total = snapshots.Sum(s => s.Tags.Count);
            TagTableSnapshot first = snapshots[0];
            string tables = snapshots.Count == 1 ? "Tabelle „" + first.TableName + "“" : snapshots.Count + " Tabellen";
            string kind = KindText(first.Kind);

            // Kopf: Eyebrow, Titel, Zusammenfassung
            Label eyebrow = new Label { Text = "MACHBARKEITSTEST · NUR LESEN", Font = Theme.Eyebrow, ForeColor = _t.RedText, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            Label title = new Label { Text = "Variablen auflisten", Font = Theme.Title, ForeColor = _t.Text, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            Label lead = new Label
            {
                Text = tables + " · Gerät „" + (first.DeviceName ?? "?") + "“ (" + kind + ") · " + total + " Variablen. Am Projekt wurde nichts geändert.",
                ForeColor = _t.Text2, AutoSize = true, Margin = new Padding(0, 0, 0, 14),
            };

            _tabs = new TabStrip(_t, new[] { "Variablen", "Attribute" }) { Dock = DockStyle.Fill, Height = 34 };
            _tabs.SelectedChanged += (s, e) =>
            {
                _tagsGrid.Visible = _tabs.Selected == 0;
                _attrGrid.Visible = _tabs.Selected == 1;
            };

            FillTagsGrid();
            FillAttributesGrid();
            _attrGrid.Visible = false;

            Panel gridHost = new Panel { Dock = DockStyle.Fill, BackColor = _t.Border, Padding = new Padding(1), Margin = new Padding(0, 0, 0, 14) };
            gridHost.Controls.Add(_tagsGrid);
            gridHost.Controls.Add(_attrGrid);

            // Fuß: Status/Autor links, Buttons rechts
            _status.Text = "Paul-Rafael Hasan-Man";
            _status.ForeColor = _t.Text3;
            _status.Font = Theme.Small;
            _status.AutoSize = true;
            _status.Anchor = AnchorStyles.Left;

            Button save = _t.PrimaryButton("Bericht speichern");
            save.Click += (s, e) => SaveReport();
            _openFolder = _t.GhostButton("Ordner öffnen");
            _openFolder.Enabled = false;
            _openFolder.Click += (s, e) => { if (_reportDir != null) Process.Start("explorer.exe", "\"" + _reportDir + "\""); };
            Button close = _t.GhostButton("Schließen");
            close.Click += (s, e) => Close();
            CancelButton = close;

            FlowLayoutPanel buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Right, WrapContents = false, BackColor = _t.Bg };
            buttons.Controls.Add(close);
            buttons.Controls.Add(_openFolder);
            buttons.Controls.Add(save);

            Panel footer = new Panel { Dock = DockStyle.Fill, Height = 40, BackColor = _t.Bg };
            footer.Controls.Add(_status);
            footer.Controls.Add(buttons);
            footer.Resize += (s, e) => _status.Location = new Point(0, (footer.Height - _status.Height) / 2);

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(28, 24, 28, 18), BackColor = _t.Bg };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.Controls.Add(eyebrow, 0, 0);
            layout.Controls.Add(title, 0, 1);
            layout.Controls.Add(lead, 0, 2);
            layout.Controls.Add(_tabs, 0, 3);
            layout.Controls.Add(gridHost, 0, 4);
            layout.Controls.Add(footer, 0, 5);
            Controls.Add(layout);
        }

        private static string KindText(HmiKind kind)
        {
            switch (kind)
            {
                case HmiKind.Classic: return "Comfort/Advanced";
                case HmiKind.Unified: return "Unified";
                default: return "Gerätetyp unbekannt";
            }
        }

        private void FillTagsGrid()
        {
            SetupGrid(_tagsGrid);
            bool multi = _snapshots.Count > 1;
            if (multi) _tagsGrid.Columns.Add("table", "TABELLE");
            _tagsGrid.Columns.Add("name", "NAME");
            _tagsGrid.Columns.Add("type", "DATENTYP");
            _tagsGrid.Columns.Add("length", "LÄNGE");
            _tagsGrid.Columns.Add("conn", "VERBINDUNG");
            _tagsGrid.Columns.Add("plc", "PLC-VARIABLE");
            _tagsGrid.Columns.Add("cycle", "ERFASSUNGSZYKLUS");
            _tagsGrid.Columns.Add("mode", "ERFASSUNGSART");

            foreach (TagTableSnapshot s in _snapshots)
            {
                foreach (TagRow t in s.Tags)
                {
                    List<object> cells = new List<object>();
                    if (multi) cells.Add(s.TableName);
                    cells.Add(t.Name);
                    cells.Add(Or(t.Get("DataType", "DataTypeName")));
                    cells.Add(Or(t.Get("Length")));
                    cells.Add(Or(t.Get("Connection")));
                    cells.Add(Or(t.Get("PlcTag", "ControllerTag", "PlcName")));
                    cells.Add(Or(t.Get("AcquisitionCycle")));
                    cells.Add(Or(t.Get("AcquisitionMode")));
                    _tagsGrid.Rows.Add(cells.ToArray());
                }
            }
            _tagsGrid.Columns[multi ? 1 : 0].DefaultCellStyle.Font = Theme.Mono;
        }

        private void FillAttributesGrid()
        {
            SetupGrid(_attrGrid);
            _attrGrid.Columns.Add("attr", "ATTRIBUT");
            _attrGrid.Columns.Add("access", "ZUGRIFF");
            _attrGrid.Columns.Add("example", "BEISPIELWERT");
            _attrGrid.Columns.Add("error", "LESEFEHLER");

            Dictionary<string, AttributeValue> seen = new Dictionary<string, AttributeValue>();
            foreach (AttributeValue a in _snapshots.SelectMany(s => s.Tags).SelectMany(t => t.Attributes))
            {
                AttributeValue existing;
                if (!seen.TryGetValue(a.Name, out existing) || (string.IsNullOrEmpty(existing.Value) && !string.IsNullOrEmpty(a.Value)))
                {
                    seen[a.Name] = a;
                }
            }
            foreach (AttributeValue a in seen.Values.OrderBy(x => x.Name))
            {
                int i = _attrGrid.Rows.Add(a.Name, a.Access, a.Value, a.Error);
                // Schreibbare Attribute hervorheben: dunkelrote Schrift (Akzent = was später geändert werden kann)
                if (a.Access != null && a.Access.IndexOf("Write", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _attrGrid.Rows[i].Cells[1].Style.ForeColor = _t.RedText;
                    _attrGrid.Rows[i].Cells[1].Style.Font = Theme.Strong;
                }
            }
            _attrGrid.Columns[0].DefaultCellStyle.Font = Theme.Mono;
        }

        private void SetupGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _t.StyleGrid(grid);
        }

        private static string Or(string value)
        {
            return string.IsNullOrEmpty(value) ? "–" : value;
        }

        private void SaveReport()
        {
            try
            {
                _reportDir = ReportWriter.Write(_snapshots);
                _status.Text = "Gespeichert: " + _reportDir;
                _status.ForeColor = _t.Text2;
                _openFolder.Enabled = true;
            }
            catch (Exception ex)
            {
                _status.Text = "Speichern fehlgeschlagen: " + ex.Message;
                _status.ForeColor = _t.RedText;
            }
        }
    }

    /// <summary>Textreiter mit Linie unten; aktiv: Textfarbe und 2 px dunkelrote Unterstreichung.</summary>
    internal sealed class TabStrip : Control
    {
        private readonly Theme _t;
        private readonly string[] _items;
        private readonly List<Rectangle> _bounds = new List<Rectangle>();
        public int Selected { get; private set; }
        public event EventHandler SelectedChanged;

        public TabStrip(Theme theme, string[] items)
        {
            _t = theme;
            _items = items;
            DoubleBuffered = true;
            BackColor = theme.Bg;
            Cursor = Cursors.Hand;
            Margin = new Padding(0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(_t.Bg);
            using (Pen line = new Pen(_t.Border))
            {
                e.Graphics.DrawLine(line, 0, Height - 1, Width, Height - 1);
            }
            _bounds.Clear();
            int x = 0;
            for (int i = 0; i < _items.Length; i++)
            {
                Font f = i == Selected ? Theme.Strong : Theme.Body;
                Size size = TextRenderer.MeasureText(_items[i], f);
                Rectangle r = new Rectangle(x, 0, size.Width + 8, Height);
                _bounds.Add(r);
                TextRenderer.DrawText(e.Graphics, _items[i], f, new Rectangle(r.X, 0, r.Width, Height - 4),
                    i == Selected ? _t.Text : _t.Text3, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (i == Selected)
                {
                    using (SolidBrush red = new SolidBrush(_t.Red))
                    {
                        e.Graphics.FillRectangle(red, r.X, Height - 2, r.Width, 2);
                    }
                }
                x += r.Width + 20;
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (int i = 0; i < _bounds.Count; i++)
            {
                if (_bounds[i].Contains(e.Location) && i != Selected)
                {
                    Selected = i;
                    Invalidate();
                    if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
                }
            }
        }
    }
}
