using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Prh.UnifiedKonverter.Ui
{
    internal static class Draw
    {
        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Mix(Color a, Color b, float amountOfB)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amountOfB),
                (int)(a.G + (b.G - a.G) * amountOfB),
                (int)(a.B + (b.B - a.B) * amountOfB));
        }

        private static Bitmap _logoSource;

        /// <summary>Signatur „PR“ in Textfarbe (wie CSS-Maske im Design Kit), in gewünschter Höhe und Deckkraft.</summary>
        public static Bitmap Logo(Color color, int height, float opacity)
        {
            if (_logoSource == null)
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("pr-signatur.png"))
                {
                    if (s == null) return null;
                    _logoSource = new Bitmap(s);
                }
            }
            int width = (int)Math.Round(_logoSource.Width * (height / (double)_logoSource.Height));
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (ImageAttributes attrs = new ImageAttributes())
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                ColorMatrix m = new ColorMatrix(new[]
                {
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, opacity, 0 },
                    new float[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 1 },
                });
                attrs.SetColorMatrix(m);
                g.DrawImage(_logoSource, new Rectangle(0, 0, width, height), 0, 0, _logoSource.Width, _logoSource.Height, GraphicsUnit.Pixel, attrs);
            }
            return bmp;
        }
    }

    /// <summary>Button nach Design Kit: Haupt (dunkelrot), Zweit (schwarz/weiß) oder Ghost (Rahmen). Radius 8 px.</summary>
    internal sealed class PrButton : Control
    {
        public enum Style { Primary, Secondary, Ghost }

        private readonly Theme _t;
        private readonly Style _style;
        private bool _hover;

        public PrButton(Theme theme, Style style, string text)
        {
            _t = theme;
            _style = style;
            Text = text;
            Font = Theme.Strong;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            Height = 38;
            Margin = new Padding(10, 0, 0, 0);
            FitWidth();
        }

        public void FitWidth()
        {
            Width = TextRenderer.MeasureText(Text, Font).Width + 36;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            FitWidth();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parentBg = Parent != null ? Parent.BackColor : _t.Bg;
            g.Clear(parentBg);

            Color fill, border, text;
            switch (_style)
            {
                case Style.Primary:
                    fill = _hover ? _t.RedHover : _t.Red; border = fill; text = Color.White; break;
                case Style.Secondary:
                    fill = _hover ? Draw.Mix(_t.Ink, _t.Bg, 0.2f) : _t.Ink; border = fill; text = _t.OnInk; break;
                default:
                    fill = _hover ? _t.InkSoft : parentBg; border = _hover ? _t.Text : _t.BorderStrong; text = _t.Text; break;
            }
            if (!Enabled)
            {
                fill = Draw.Mix(parentBg, fill, 0.35f);
                border = Draw.Mix(parentBg, border, 0.35f);
                text = Draw.Mix(parentBg, text, 0.35f);
            }

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Draw.Rounded(r, 8))
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Focused && ShowFocusCues)
            {
                using (Pen focus = new Pen(_t.Ink) { DashStyle = DashStyle.Dot })
                {
                    g.DrawRectangle(focus, 3, 3, Width - 7, Height - 7);
                }
            }
        }
    }

    /// <summary>Kennzahl-Karte: große Zahl, kleine Beschriftung, 1 px Rahmen, Radius 10 px.</summary>
    internal sealed class StatTile : Control
    {
        private readonly Theme _t;
        public string Label;
        public int Value;
        /// <summary>true: Wert > 0 dunkelrot – „hier musst du etwas tun“.</summary>
        public bool Accent;

        public StatTile(Theme theme, string label, bool accent)
        {
            _t = theme;
            Label = label;
            Accent = accent;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(176, 78);
            Margin = new Padding(0, 0, 12, 0);
        }

        public void SetValue(int value)
        {
            Value = value;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : _t.Bg);
            using (GraphicsPath path = Draw.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 10))
            using (SolidBrush b = new SolidBrush(_t.Bg))
            using (Pen p = new Pen(_t.Border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            Color valueColor = Accent && Value > 0 ? _t.RedText : _t.Text;
            TextRenderer.DrawText(g, Value.ToString("N0"), Theme.Number, new Point(16, 10), valueColor, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Label, Theme.Small, new Point(17, 50), _t.Text3, TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Textreiter mit Linie unten; aktiv: Textfarbe und 2 px dunkelrote Unterstreichung.</summary>
    internal sealed class TabStrip : Control
    {
        private readonly Theme _t;
        private readonly List<string> _items = new List<string>();
        private readonly List<Rectangle> _bounds = new List<Rectangle>();
        public int Selected { get; private set; }
        public event EventHandler SelectedChanged;

        public TabStrip(Theme theme, IEnumerable<string> items)
        {
            _t = theme;
            _items.AddRange(items);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = theme.Bg;
            Cursor = Cursors.Hand;
            Height = 36;
        }

        public void SetText(int index, string text)
        {
            _items[index] = text;
            Invalidate();
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
            for (int i = 0; i < _items.Count; i++)
            {
                Font f = i == Selected ? Theme.Strong : Theme.Body;
                Size size = TextRenderer.MeasureText(_items[i], f);
                Rectangle r = new Rectangle(x, 0, size.Width + 4, Height);
                _bounds.Add(r);
                TextRenderer.DrawText(e.Graphics, _items[i], f, new Rectangle(r.X, 0, r.Width, Height - 3),
                    i == Selected ? _t.Text : _t.Text3, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (i == Selected)
                {
                    using (SolidBrush red = new SolidBrush(_t.Red))
                    {
                        e.Graphics.FillRectangle(red, r.X, Height - 2, r.Width, 2);
                    }
                }
                x += r.Width + 22;
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

    /// <summary>Suchfeld: 1 px Rahmen, Radius 8 px, Platzhaltertext.</summary>
    internal sealed class SearchBox : Panel
    {
        private readonly Theme _t;
        private readonly TextBox _box = new TextBox();
        private readonly string _placeholder;

        public event EventHandler SearchChanged;
        public string Query { get { return _box.Text.Trim(); } }

        public SearchBox(Theme theme, string placeholder)
        {
            _t = theme;
            _placeholder = placeholder;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = theme.Bg;
            Size = new Size(260, 34);
            Padding = new Padding(12, 8, 10, 6);
            _box.BorderStyle = BorderStyle.None;
            _box.BackColor = theme.Bg;
            _box.ForeColor = theme.Text;
            _box.Font = Theme.Body;
            _box.Dock = DockStyle.Fill;
            _box.TextChanged += (s, e) => { Invalidate(); if (SearchChanged != null) SearchChanged(this, EventArgs.Empty); };
            _box.GotFocus += (s, e) => Invalidate();
            _box.LostFocus += (s, e) => Invalidate();
            Controls.Add(_box);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : _t.Bg);
            using (GraphicsPath path = Draw.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 8))
            using (SolidBrush b = new SolidBrush(_t.Bg))
            using (Pen p = new Pen(_box.Focused ? _t.Ink : _t.BorderStrong))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            if (_box.TextLength == 0 && !_box.Focused)
            {
                TextRenderer.DrawText(g, _placeholder, Theme.Body, new Point(Padding.Left - 1, Padding.Top), _t.Text3, TextFormatFlags.NoPadding);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            _box.Focus();
        }
    }

    /// <summary>Panel mit 1 px Linie an einer Seite (Kopf-, Fußzeile, Seitenleiste).</summary>
    internal sealed class LinePanel : Panel
    {
        private readonly Color _line;
        private readonly DockStyle _side;

        public LinePanel(Color background, Color line, DockStyle side)
        {
            BackColor = background;
            _line = line;
            _side = side;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(_line))
            {
                switch (_side)
                {
                    case DockStyle.Top: e.Graphics.DrawLine(p, 0, 0, Width, 0); break;
                    case DockStyle.Bottom: e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1); break;
                    case DockStyle.Left: e.Graphics.DrawLine(p, 0, 0, 0, Height); break;
                    case DockStyle.Right: e.Graphics.DrawLine(p, Width - 1, 0, Width - 1, Height); break;
                }
            }
        }
    }
}
