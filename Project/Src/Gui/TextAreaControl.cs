// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Document;
using ICSharpCode.TextEditor.Util;

namespace ICSharpCode.TextEditor
{
    /// <summary>
    ///     This class paints the textarea.
    /// </summary>
    [ToolboxItem(defaultType: false)]
    public class TextAreaControl : Panel
    {
        private const int LineLengthCacheAdditionalSize = 100;

        private readonly MouseWheelHandler mouseWheelHandler = new MouseWheelHandler();

        private readonly int scrollMarginHeight = 3;

        private bool adjustScrollBarsOnNextUpdate;

        private bool disposed;

        private HRuler hRuler;

        private int[] lineLengthCache;
        private TextEditorControl motherTextEditorControl;
        private Point scrollToPosOnNextUpdate;

        public TextAreaControl(TextEditorControl motherTextEditorControl)
        {
            this.motherTextEditorControl = motherTextEditorControl;

            TextArea = new TextArea(motherTextEditorControl, this);
            Controls.Add(TextArea);

            VScrollBar.ValueChanged += VScrollBarValueChanged;
            Controls.Add(VScrollBar);

            HScrollBar.ValueChanged += HScrollBarValueChanged;
            Controls.Add(HScrollBar);
            ResizeRedraw = true;

            Document.TextContentChanged += DocumentTextContentChanged;
            Document.DocumentChanged += AdjustScrollBarsOnDocumentChange;
            Document.UpdateCommited += DocumentUpdateCommitted;
        }

        public TextArea TextArea { get; }

        public SelectionManager SelectionManager => TextArea.SelectionManager;

        public Caret Caret => TextArea.Caret;

        [Browsable(browsable: false)]
        public IDocument Document => motherTextEditorControl?.Document;

        public ITextEditorProperties TextEditorProperties => motherTextEditorControl?.TextEditorProperties;

        public VScrollBar VScrollBar { get; private set; } = new VScrollBar();

        public HScrollBar HScrollBar { get; private set; } = new HScrollBar();

        public bool DoHandleMousewheel { get; set; } = true;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                if (!disposed)
                {
                    disposed = true;
                    Document.TextContentChanged -= DocumentTextContentChanged;
                    Document.DocumentChanged -= AdjustScrollBarsOnDocumentChange;
                    Document.UpdateCommited -= DocumentUpdateCommitted;
                    motherTextEditorControl = null;
                    if (VScrollBar != null)
                    {
                        VScrollBar.Dispose();
                        VScrollBar = null;
                    }

                    if (HScrollBar != null)
                    {
                        HScrollBar.Dispose();
                        HScrollBar = null;
                    }

                    if (hRuler != null)
                    {
                        hRuler.Dispose();
                        hRuler = null;
                    }
                }

            base.Dispose(disposing);
        }

        private void DocumentTextContentChanged(object sender, EventArgs e)
        {
            // after the text content is changed abruptly, we need to validate the
            // caret position - otherwise the caret position is invalid for a short amount
            // of time, which can break client code that expects that the caret position is always valid
            Caret.ValidateCaretPos();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayout();
        }

        private void AdjustScrollBarsOnDocumentChange(object sender, DocumentEventArgs e)
        {
            if (motherTextEditorControl.IsInUpdate == false)
            {
                AdjustScrollBarsClearCache();
                UpdateLayout();
            }
            else
            {
                adjustScrollBarsOnNextUpdate = true;
            }
        }

        private void DocumentUpdateCommitted(object sender, EventArgs e)
        {
            if (motherTextEditorControl.IsInUpdate == false)
            {
                Caret.ValidateCaretPos();

                // AdjustScrollBarsOnCommittedUpdate
                if (!scrollToPosOnNextUpdate.IsEmpty)
                    ScrollTo(scrollToPosOnNextUpdate.Y, scrollToPosOnNextUpdate.X);
                if (adjustScrollBarsOnNextUpdate)
                {
                    AdjustScrollBarsClearCache();
                    UpdateLayout();
                }
            }
        }

        private void AdjustScrollBarsClearCache()
        {
            if (lineLengthCache != null)
            {
                if (lineLengthCache.Length < Document.TotalNumberOfLines + 2*LineLengthCacheAdditionalSize)
                    lineLengthCache = null;
                else
                    Array.Clear(lineLengthCache, index: 0, lineLengthCache.Length);
            }
        }

        public void UpdateLayout()
        {
            if (TextArea == null)
                return;

            adjustScrollBarsOnNextUpdate = false;

            var view = TextArea.TextView;

            var currentVisibilites = GetScrollVisibilities(HScrollBar.Visible, VScrollBar.Visible);
            var visited = new HashSet<ScrollVisibilities>();

            // Start walking through any layout state transitions and see whether it ends up in a stable state.
            var fromVisibilities = currentVisibilites;
            while (true)
            {
                if (!visited.Add(fromVisibilities))
                    // Returning to a visited state -- unstable, so make no change
                    break;
                var bounds = Measure(fromVisibilities);
                var to = ComputeScrollBarVisibilities(bounds.textArea.Size);
                if (to.visibilities == fromVisibilities)
                {
                    // Layout is stable -- apply it
                    ApplyLayout(fromVisibilities, to.maxLength, bounds);
                    break;
                }

                fromVisibilities = to.visibilities;
            }

            ScrollVisibilities GetScrollVisibilities(bool h, bool v)
            {
                return (h ? ScrollVisibilities.H : ScrollVisibilities.None)
                       | (v ? ScrollVisibilities.V : ScrollVisibilities.None);
            }

            (Rectangle hRule, Rectangle textControl, Rectangle textArea, Rectangle hScroll, Rectangle vScroll)
            Measure(ScrollVisibilities scrollVisibilities)
            {
                var v = scrollVisibilities.HasFlag(ScrollVisibilities.V);
                var h = scrollVisibilities.HasFlag(ScrollVisibilities.H);
                var vScrollSize = v ? SystemInformation.VerticalScrollBarArrowHeight : 0;
                var hScrollSize = h ? SystemInformation.HorizontalScrollBarArrowWidth : 0;
                var x0 = TextArea.LeftMargins.Where(margin => margin.IsVisible).Sum(margin => margin.Width);

                var hRuleBounds = hRuler != null
                    ? new Rectangle(
                        x: 0,
                        y: 0,
                        Width - vScrollSize,
                        TextArea.TextView.FontHeight)
                    : default;

                var textControlBounds = new Rectangle(
                    x: 0,
                    hRuleBounds.Bottom,
                    Width - vScrollSize,
                    Height - hRuleBounds.Bottom - hScrollSize);

                var textAreaBounds = new Rectangle(
                    x0,
                    hRuleBounds.Bottom,
                    Width - x0 - vScrollSize,
                    Height - hRuleBounds.Bottom - hScrollSize);

                var vScrollBounds = v
                    ? new Rectangle(
                        textAreaBounds.Right,
                        y: 0,
                        SystemInformation.HorizontalScrollBarArrowWidth,
                        Height - hScrollSize)
                    : default;

                var hScrollBounds = h
                    ? new Rectangle(
                        x: 0,
                        textAreaBounds.Bottom,
                        Width - vScrollSize,
                        SystemInformation.VerticalScrollBarArrowHeight)
                    : default;

                return (hRuleBounds, textControlBounds, textAreaBounds, hScrollBounds, vScrollBounds);
            }

            (ScrollVisibilities visibilities, int maxLength) ComputeScrollBarVisibilities(Size size)
            {
                var visibleLineCount = 1 + size.Height/view.FontHeight;
                var visibleColumnCount = size.Width/view.WideSpaceWidth - 1;

                var firstLine = view.FirstVisibleLine;

                var lastLine = Document.GetFirstLogicalLine(firstLine + visibleLineCount);
                if (lastLine >= Document.TotalNumberOfLines)
                    lastLine = Document.TotalNumberOfLines - 1;

                if (lineLengthCache == null || lineLengthCache.Length <= lastLine)
                    lineLengthCache = new int[lastLine + LineLengthCacheAdditionalSize];

                var maxLength = 0;
                for (var lineNumber = firstLine; lineNumber <= lastLine; lineNumber++)
                {
                    var lineSegment = Document.GetLineSegment(lineNumber);
                    if (Document.FoldingManager.IsLineVisible(lineNumber))
                    {
                        if (lineLengthCache[lineNumber] > 0)
                        {
                            maxLength = Math.Max(maxLength, lineLengthCache[lineNumber]);
                        }
                        else
                        {
                            var visualLength = view.GetVisualColumnFast(lineSegment, lineSegment.Length);
                            lineLengthCache[lineNumber] = Math.Max(1, visualLength);
                            maxLength = Math.Max(maxLength, visualLength);
                        }
                    }
                }

                var vScrollBarVisible = VScrollBar.Value != 0 || TextArea.Document.TotalNumberOfLines >= visibleLineCount;
                var hScrollBarVisible = HScrollBar.Value != 0 || maxLength > visibleColumnCount;

                return (GetScrollVisibilities(hScrollBarVisible, vScrollBarVisible), maxLength);
            }

            void ApplyLayout(ScrollVisibilities scrollVisibilities, int maxColumn, (Rectangle hRule, Rectangle textControl, Rectangle textArea, Rectangle hScroll, Rectangle vScroll) bounds)
            {
                var visibleColumnCount = bounds.textArea.Width/view.WideSpaceWidth - 1;

                VScrollBar.Minimum = 0;
                VScrollBar.Maximum = TextArea.MaxVScrollValue; // number of visible lines in document (folding!)
                VScrollBar.LargeChange = Math.Max(0, bounds.textArea.Height);
                VScrollBar.SmallChange = Math.Max(0, view.FontHeight);
                VScrollBar.Visible = scrollVisibilities.HasFlag(ScrollVisibilities.V);
                VScrollBar.Bounds = bounds.vScroll;

                HScrollBar.Minimum = 0;
                HScrollBar.Maximum = Math.Max(maxColumn, visibleColumnCount - 1);
                HScrollBar.LargeChange = Math.Max(0, visibleColumnCount - 1);
                HScrollBar.SmallChange = 4;
                HScrollBar.Visible = scrollVisibilities.HasFlag(ScrollVisibilities.H);
                HScrollBar.Bounds = bounds.hScroll;

                if (hRuler != null)
                    hRuler.Bounds = bounds.hRule;

                TextArea.Bounds = bounds.textControl;
            }
        }

        public void OptionsChanged()
        {
            TextArea.OptionsChanged();

            if (TextArea.TextEditorProperties.ShowHorizontalRuler)
            {
                if (hRuler == null)
                {
                    hRuler = new HRuler(TextArea);
                    Controls.Add(hRuler);
                    UpdateLayout();
                }
                else
                {
                    hRuler.Invalidate();
                }
            }
            else
            {
                if (hRuler != null)
                {
                    Controls.Remove(hRuler);
                    hRuler.Dispose();
                    hRuler = null;
                    UpdateLayout();
                }
            }

            UpdateLayout();
        }

        private void VScrollBarValueChanged(object sender, EventArgs e)
        {
            TextArea.VirtualTop = new Point(TextArea.VirtualTop.X, VScrollBar.Value);
            TextArea.Invalidate();
            UpdateLayout();
        }

        private void HScrollBarValueChanged(object sender, EventArgs e)
        {
            TextArea.VirtualTop = new Point(HScrollBar.Value*TextArea.TextView.WideSpaceWidth, TextArea.VirtualTop.Y);
            TextArea.Invalidate();
        }

        public void HandleMouseWheel(MouseEventArgs e, bool hScroll = false)
        {
            var scrollDistance = mouseWheelHandler.GetScrollAmount(e);
            if (scrollDistance == 0)
                return;
            if (ModifierKeys.HasFlag(Keys.Control) && TextEditorProperties.MouseWheelTextZoom)
            {
                if (scrollDistance > 0)
                    motherTextEditorControl.Font = new Font(
                        motherTextEditorControl.Font.Name,
                        motherTextEditorControl.Font.Size + 1);
                else
                    motherTextEditorControl.Font = new Font(
                        motherTextEditorControl.Font.Name,
                        Math.Max(6, motherTextEditorControl.Font.Size - 1));
            }
            else
            {
                if (TextEditorProperties.MouseWheelScrollDown && !hScroll)
                    scrollDistance = -scrollDistance;
                if (hScroll || ModifierKeys.HasFlag(Keys.Shift))
                {
                    var newValue = HScrollBar.Value + HScrollBar.SmallChange*scrollDistance;
                    HScrollBar.Value = Math.Max(HScrollBar.Minimum, Math.Min(HScrollBar.Maximum - HScrollBar.LargeChange + 1, newValue));
                }
                else
                {
                    var newValue = VScrollBar.Value + VScrollBar.SmallChange*scrollDistance;
                    VScrollBar.Value = Math.Max(VScrollBar.Minimum, Math.Min(VScrollBar.Maximum - VScrollBar.LargeChange + 1, newValue));
                }
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (DoHandleMousewheel)
                HandleMouseWheel(e);
        }

        /// <summary>
        ///  Handles the WM_MOUSEHWHEEL message
        /// </summary>
        private void WmMouseHWheel(ref Message m)
        {
            if (!DoHandleMousewheel)
                return;

            Point p = new Point(Win32Util.SignedLOWORD(m.LParam), Win32Util.SignedHIWORD(m.LParam));
            p = PointToClient(p);
            HandledMouseEventArgs e = new (MouseButtons.None,
                0,
                p.X,
                p.Y,
                Win32Util.SignedHIWORD(m.WParam));

            // Note: System/WinForms/Control.cs Makes a call to OnMouseWheel(e); at this place
            HandleMouseWheel(e, hScroll: true);

            m.Result = (IntPtr)(e.Handled ? 0 : 1);
            if (!e.Handled)
            {
                // Forwarding the message to the parent window
                DefWndProc(ref m);
            }
        }

        public void ScrollToCaret()
        {
            ScrollTo(TextArea.Caret.Line, TextArea.Caret.Column);
        }

        public void ScrollTo(int line, int column)
        {
            if (motherTextEditorControl.IsInUpdate)
            {
                scrollToPosOnNextUpdate = new Point(column, line);
                return;
            }

            scrollToPosOnNextUpdate = Point.Empty;

            ScrollTo(line);

            var curCharMin = HScrollBar.Value - HScrollBar.Minimum;
            var curCharMax = curCharMin + TextArea.TextView.VisibleColumnCount;

            var pos = TextArea.TextView.GetVisualColumn(line, column);

            if (TextArea.TextView.VisibleColumnCount < 0)
            {
                HScrollBar.Value = 0;
            }
            else
            {
                if (pos < curCharMin)
                {
                    HScrollBar.Value = Math.Max(0, pos - scrollMarginHeight);
                }
                else
                {
                    if (pos > curCharMax)
                        HScrollBar.Value = Math.Max(0, Math.Min(HScrollBar.Maximum, pos - TextArea.TextView.VisibleColumnCount + scrollMarginHeight));
                }
            }
        }

        /// <summary>
        ///     Ensure that <paramref name="line" /> is visible.
        /// </summary>
        public void ScrollTo(int line)
        {
            line = Math.Max(0, Math.Min(Document.TotalNumberOfLines - 1, line));
            line = Document.GetVisibleLine(line);
            var curLineMin = TextArea.TextView.FirstPhysicalLine;
            if (TextArea.TextView.LineHeightRemainder > 0)
                curLineMin++;

            if (line - scrollMarginHeight + 3 < curLineMin)
            {
                VScrollBar.Value = Math.Max(0, Math.Min(VScrollBar.Maximum, (line - scrollMarginHeight + 3)*TextArea.TextView.FontHeight));
                VScrollBarValueChanged(this, EventArgs.Empty);
            }
            else
            {
                var curLineMax = curLineMin + TextArea.TextView.VisibleLineCount;
                if (line + scrollMarginHeight - 1 > curLineMax)
                {
                    if (TextArea.TextView.VisibleLineCount == 1)
                        VScrollBar.Value = Math.Max(0, Math.Min(VScrollBar.Maximum, (line - scrollMarginHeight - 1)*TextArea.TextView.FontHeight));
                    else
                        VScrollBar.Value = Math.Min(
                            VScrollBar.Maximum,
                            (line - TextArea.TextView.VisibleLineCount + scrollMarginHeight - 1)*TextArea.TextView.FontHeight);
                    VScrollBarValueChanged(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        ///     Scroll so that the specified line is centered.
        /// </summary>
        /// <param name="line">Line to center view on</param>
        /// <param name="treshold">
        ///     If this action would cause scrolling by less than or equal to
        ///     <paramref name="treshold" /> lines in any direction, don't scroll.
        ///     Use -1 to always center the view.
        /// </param>
        public void CenterViewOn(int line, int treshold)
        {
            line = Math.Max(0, Math.Min(Document.TotalNumberOfLines - 1, line));
            // convert line to visible line:
            line = Document.GetVisibleLine(line);
            // subtract half the visible line count
            line -= TextArea.TextView.VisibleLineCount/2;

            var curLineMin = TextArea.TextView.FirstPhysicalLine;
            if (TextArea.TextView.LineHeightRemainder > 0)
                curLineMin++;
            if (Math.Abs(curLineMin - line) > treshold)
            {
                // scroll:
                VScrollBar.Value = Math.Max(0, Math.Min(VScrollBar.Maximum, (line - scrollMarginHeight + 3)*TextArea.TextView.FontHeight));
                VScrollBarValueChanged(this, EventArgs.Empty);
            }
        }

        public void JumpTo(int line)
        {
            line = Math.Max(0, Math.Min(line, Document.TotalNumberOfLines - 1));
            var text = Document.GetText(Document.GetLineSegment(line));
            JumpTo(line, text.Length - text.TrimStart().Length);
        }

        public void JumpTo(int line, int column)
        {
            TextArea.Focus();
            TextArea.SelectionManager.ClearSelection();
            TextArea.Caret.Position = new TextLocation(column, line);
            TextArea.SetDesiredColumn();
            ScrollToCaret();
        }

        public event MouseEventHandler ShowContextMenu;

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x007B: // WM_CONTEXTMENU
                    if (ShowContextMenu != null)
                    {
                        Point location = m.LParam.ToPoint();
                        if (location.X == -1 && location.Y == -1)
                        {
                            var pos = Caret.ScreenPosition;
                            ShowContextMenu?.Invoke(this, new MouseEventArgs(MouseButtons.None, clicks: 0, pos.X, pos.Y + TextArea.TextView.FontHeight, delta: 0));
                        }
                        else
                        {
                            var pos = PointToClient(location);
                            ShowContextMenu?.Invoke(this, new MouseEventArgs(MouseButtons.Right, clicks: 1, pos.X, pos.Y, delta: 0));
                        }
                    }
                    break;

                case 0x020e: // WM_MOUSEHWHEEL
                    WmMouseHWheel(ref m);
                    break;
            }

            base.WndProc(ref m);
        }

        protected override void OnEnter(EventArgs e)
        {
            // SD2-1072 - Make sure the caret line is valid if anyone
            // has handlers for the Enter event.
            Caret.ValidateCaretPos();
            base.OnEnter(e);
        }

        [Flags]
        private enum ScrollVisibilities
        {
            None = 0,
            H = 1,
            V = 2
        }
    }
}
