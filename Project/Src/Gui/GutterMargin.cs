// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ICSharpCode.TextEditor.Document;

namespace ICSharpCode.TextEditor
{
    /// <summary>
    ///     This class views the line numbers and folding markers.
    /// </summary>
    public class GutterMargin : AbstractMargin, IDisposable
    {
        public static Cursor RightLeftCursor;

        private readonly StringFormat numberStringFormat = (StringFormat)StringFormat.GenericTypographic.Clone();

        static GutterMargin()
        {
            using (var cursorStream = Assembly.GetCallingAssembly().GetManifestResourceStream("ICSharpCode.TextEditor.Resources.RightArrow.cur"))
            {
                if (cursorStream == null)
                    throw new Exception("could not find cursor resource");
                RightLeftCursor = new Cursor(cursorStream);
            }
        }

        public GutterMargin(TextArea textArea) : base(textArea)
        {
            numberStringFormat.LineAlignment = StringAlignment.Near;
            numberStringFormat.FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.FitBlackBox |
                                             StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
        }

        public override Cursor Cursor => RightLeftCursor;

        public override int Width
            => textArea.TextView.WideSpaceWidth*Math.Max(4, (int)Math.Log10(textArea.Document.TotalNumberOfLines) + 4);

        public override bool IsVisible => textArea.TextEditorProperties.ShowLineNumbers;

        public void Dispose()
        {
            numberStringFormat.Dispose();
        }

        public override void Paint(Graphics g, Rectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            var lineNumberPainterColor = textArea.Document.HighlightingStrategy.GetColorFor("LineNumbers");
            var fontHeight = textArea.TextView.FontHeight;
            var fillBrush = textArea.Enabled
                ? BrushRegistry.GetBrush(lineNumberPainterColor.BackgroundColor)
                : SystemBrushes.InactiveBorder;
            var drawBrush = BrushRegistry.GetBrush(lineNumberPainterColor.Color);

            for (var y = 0; y < (drawingPosition.Height + textArea.TextView.VisibleLineDrawingRemainder)/fontHeight + 1; ++y)
            {
                var ypos = drawingPosition.Y + fontHeight*y - textArea.TextView.VisibleLineDrawingRemainder;
                var backgroundRectangle = new Rectangle(drawingPosition.X, ypos, drawingPosition.Width, fontHeight);
                if (rect.IntersectsWith(backgroundRectangle))
                {
                    g.FillRectangle(fillBrush, backgroundRectangle);
                    var curLine = textArea.Document.GetFirstLogicalLine(textArea.Document.GetVisibleLine(textArea.TextView.FirstVisibleLine) + y);

                    if (curLine < textArea.Document.TotalNumberOfLines)
                        g.DrawString(
                            (curLine + 1).ToString(),
                            lineNumberPainterColor.GetFont(TextEditorProperties.FontContainer),
                            drawBrush,
                            backgroundRectangle,
                            numberStringFormat);
                }
            }
        }

        public override void HandleMouseDown(Point mousepos, MouseButtons mouseButtons)
        {
            textArea.SelectionManager.selectFrom.where = WhereFrom.Gutter;
            var realline = textArea.TextView.GetLogicalLine(mousepos.Y);
            if (realline >= 0 && realline < textArea.Document.TotalNumberOfLines)
            {
                // shift-select
                TextLocation selectionStartPos;
                if ((Control.ModifierKeys & Keys.Shift) != 0)
                {
                    if (!textArea.SelectionManager.HasSomethingSelected && realline != textArea.Caret.Position.Y)
                    {
                        if (realline >= textArea.Caret.Position.Y)
                        {
                            // at or below starting selection, place the cursor on the next line
                            // nothing is selected so make a new selection from cursor
                            selectionStartPos = textArea.Caret.Position;
                            // whole line selection - start of line to start of next line
                            if (realline < textArea.Document.TotalNumberOfLines - 1)
                            {
                                textArea.SelectionManager.SetSelection(new DefaultSelection(textArea.Document, selectionStartPos, new TextLocation(column: 0, realline + 1)));
                                textArea.Caret.Position = new TextLocation(column: 0, realline + 1);
                            }
                            else
                            {
                                textArea.SelectionManager.SetSelection(new DefaultSelection(textArea.Document, selectionStartPos, new TextLocation(textArea.Document.GetLineSegment(realline).Length + 1, realline)));
                                textArea.Caret.Position = new TextLocation(textArea.Document.GetLineSegment(realline).Length + 1, realline);
                            }
                        }
                        else
                        {
                            // prior lines to starting selection, place the cursor on the same line as the new selection
                            // nothing is selected so make a new selection from cursor
                            selectionStartPos = textArea.Caret.Position;
                            // whole line selection - start of line to start of next line
                            textArea.SelectionManager.SetSelection(new DefaultSelection(textArea.Document, selectionStartPos, new TextLocation(selectionStartPos.X, selectionStartPos.Y)));
                            textArea.SelectionManager.ExtendSelection(new TextLocation(selectionStartPos.X, selectionStartPos.Y), new TextLocation(column: 0, realline));
                            textArea.Caret.Position = new TextLocation(column: 0, realline);
                        }
                    }
                    else
                    {
                        // let MouseMove handle a shift-click in a gutter
                        var e = new MouseEventArgs(mouseButtons, clicks: 1, mousepos.X, mousepos.Y, delta: 0);
                        textArea.RaiseMouseMove(e);
                    }
                }
                else
                {
                    // this is a new selection with no shift-key
                    // sync the textareamousehandler mouse location
                    // (fixes problem with clicking out into a menu then back to the gutter whilst
                    // there is a selection)
                    textArea.mousepos = mousepos;

                    selectionStartPos = new TextLocation(column: 0, realline);
                    textArea.SelectionManager.ClearSelection();
                    // whole line selection - start of line to start of next line
                    if (realline < textArea.Document.TotalNumberOfLines - 1)
                    {
                        textArea.SelectionManager.SetSelection(new DefaultSelection(textArea.Document, selectionStartPos, new TextLocation(selectionStartPos.X, selectionStartPos.Y + 1)));
                        textArea.Caret.Position = new TextLocation(selectionStartPos.X, selectionStartPos.Y + 1);
                    }
                    else
                    {
                        textArea.SelectionManager.SetSelection(new DefaultSelection(textArea.Document, new TextLocation(column: 0, realline), new TextLocation(textArea.Document.GetLineSegment(realline).Length + 1, selectionStartPos.Y)));
                        textArea.Caret.Position = new TextLocation(textArea.Document.GetLineSegment(realline).Length + 1, selectionStartPos.Y);
                    }
                }
            }
        }
    }
}
