﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ICSharpCode.TextEditor.Document
{
    internal sealed class LineManager
    {
        // use always the same DelimiterSegment object for the NextDelimiter
        private readonly DelimiterSegment delimiterSegment = new DelimiterSegment();

        private readonly IDocument document;
        private readonly LineSegmentTree lineCollection = new LineSegmentTree();
        private IHighlightingStrategy highlightingStrategy;

        public LineManager(IDocument document, IHighlightingStrategy highlightingStrategy)
        {
            this.document = document;
            this.highlightingStrategy = highlightingStrategy;
        }

        public IList<LineSegment> LineSegmentCollection => lineCollection;

        public int TotalNumberOfLines => lineCollection.Count;

        public IHighlightingStrategy HighlightingStrategy
        {
            get => highlightingStrategy;
            set
            {
                if (highlightingStrategy != value)
                {
                    highlightingStrategy = value;
                    highlightingStrategy?.MarkTokens(document);
                }
            }
        }

        public int GetLineNumberForOffset(int offset)
        {
            return GetLineSegmentForOffset(offset).LineNumber;
        }

        public LineSegment GetLineSegmentForOffset(int offset)
        {
            return lineCollection.GetByOffset(offset);
        }

        public LineSegment GetLineSegment(int lineNr)
        {
            return lineCollection[lineNr];
        }

        public void Insert(int offset, string text)
        {
            Replace(offset, length: 0, text);
        }

        public void Remove(int offset, int length)
        {
            Replace(offset, length, string.Empty);
        }

        public void Replace(int offset, int length, string text)
        {
            //Debug.WriteLine("Replace offset=" + offset + " length=" + length + " text.Length=" + text.Length);
            var lineStart = GetLineNumberForOffset(offset);
            var oldNumberOfLines = TotalNumberOfLines;
            var deferredEventList = new DeferredEventList();
            RemoveInternal(ref deferredEventList, offset, length);
            var numberOfLinesAfterRemoving = TotalNumberOfLines;
            if (!string.IsNullOrEmpty(text))
                InsertInternal(offset, text);
//            #if DEBUG
//            Console.WriteLine("New line collection:");
//            Console.WriteLine(lineCollection.GetTreeAsString());
//            Console.WriteLine("New text:");
//            Console.WriteLine("'" + document.TextContent + "'");
//            #endif
            // Only fire events after RemoveInternal+InsertInternal finished completely:
            // Otherwise we would expose inconsistent state to the event handlers.
            RunHighlighter(lineStart, 1 + Math.Max(val1: 0, TotalNumberOfLines - numberOfLinesAfterRemoving));

            if (deferredEventList.removedLines != null)
                foreach (var ls in deferredEventList.removedLines)
                    OnLineDeleted(new LineEventArgs(document, ls));
            deferredEventList.RaiseEvents();
            if (TotalNumberOfLines != oldNumberOfLines)
                OnLineCountChanged(new LineCountChangeEventArgs(document, lineStart, TotalNumberOfLines - oldNumberOfLines));
        }

        private void RemoveInternal(ref DeferredEventList deferredEventList, int offset, int length)
        {
            Debug.Assert(length >= 0);
            if (length == 0) return;
            var it = lineCollection.GetEnumeratorForOffset(offset);
            var startSegment = it.Current;
            var startSegmentOffset = startSegment.Offset;
            if (offset + length < startSegmentOffset + startSegment.TotalLength)
            {
                // just removing a part of this line segment
                startSegment.RemovedLinePart(ref deferredEventList, offset - startSegmentOffset, length);
                SetSegmentLength(startSegment, startSegment.TotalLength - length);
                return;
            }

            // merge startSegment with another line segment because startSegment's delimiter was deleted
            // possibly remove lines in between if multiple delimiters were deleted
            var charactersRemovedInStartLine = startSegmentOffset + startSegment.TotalLength - offset;
            Debug.Assert(charactersRemovedInStartLine > 0);
            startSegment.RemovedLinePart(ref deferredEventList, offset - startSegmentOffset, charactersRemovedInStartLine);

            var endSegment = lineCollection.GetByOffset(offset + length);
            if (endSegment == startSegment)
            {
                // special case: we are removing a part of the last line up to the
                // end of the document
                SetSegmentLength(startSegment, startSegment.TotalLength - length);
                return;
            }

            var endSegmentOffset = endSegment.Offset;
            var charactersLeftInEndLine = endSegmentOffset + endSegment.TotalLength - (offset + length);
            endSegment.RemovedLinePart(ref deferredEventList, startColumn: 0, endSegment.TotalLength - charactersLeftInEndLine);
            startSegment.MergedWith(endSegment, offset - startSegmentOffset);
            SetSegmentLength(startSegment, startSegment.TotalLength - charactersRemovedInStartLine + charactersLeftInEndLine);
            startSegment.DelimiterLength = endSegment.DelimiterLength;
            // remove all segments between startSegment (excl.) and endSegment (incl.)
            it.MoveNext();
            LineSegment segmentToRemove;
            do
            {
                segmentToRemove = it.Current;
                it.MoveNext();
                lineCollection.RemoveSegment(segmentToRemove);
                segmentToRemove.Deleted(ref deferredEventList);
            } while (segmentToRemove != endSegment);
        }

        private void InsertInternal(int offset, string text)
        {
            var segment = lineCollection.GetByOffset(offset);
            var ds = NextDelimiter(text, offset: 0);
            if (ds == null)
            {
                // no newline is being inserted, all text is inserted in a single line
                segment.InsertedLinePart(offset - segment.Offset, text.Length);
                SetSegmentLength(segment, segment.TotalLength + text.Length);
                return;
            }

            var firstLine = segment;
            firstLine.InsertedLinePart(offset - firstLine.Offset, ds.Offset);
            var lastDelimiterEnd = 0;
            while (ds != null)
            {
                // split line segment at line delimiter
                var lineBreakOffset = offset + ds.Offset + ds.Length;
                var segmentOffset = segment.Offset;
                var lengthAfterInsertionPos = segmentOffset + segment.TotalLength - (offset + lastDelimiterEnd);
                lineCollection.SetSegmentLength(segment, lineBreakOffset - segmentOffset);
                var newSegment = lineCollection.InsertSegmentAfter(segment, lengthAfterInsertionPos);
                segment.DelimiterLength = ds.Length;
                segment.EolMarker = ds.EolMarker;

                segment = newSegment;
                lastDelimiterEnd = ds.Offset + ds.Length;

                ds = NextDelimiter(text, lastDelimiterEnd);
            }

            firstLine.SplitTo(segment);
            // insert rest after last delimiter
            if (lastDelimiterEnd != text.Length)
            {
                segment.InsertedLinePart(startColumn: 0, text.Length - lastDelimiterEnd);
                SetSegmentLength(segment, segment.TotalLength + text.Length - lastDelimiterEnd);
            }
        }

        private void SetSegmentLength(LineSegment segment, int newTotalLength)
        {
            var delta = newTotalLength - segment.TotalLength;
            if (delta != 0)
            {
                lineCollection.SetSegmentLength(segment, newTotalLength);
                OnLineLengthChanged(new LineLengthChangeEventArgs(document, segment, delta));
            }
        }

        private void RunHighlighter(int firstLine, int lineCount)
        {
            if (highlightingStrategy != null)
            {
                var markLines = new List<LineSegment>();
                var it = lineCollection.GetEnumeratorForIndex(firstLine);
                for (var i = 0; i < lineCount && it.IsValid; i++)
                {
                    markLines.Add(it.Current);
                    it.MoveNext();
                }

                highlightingStrategy.MarkTokens(document, markLines);
            }
        }

        public void SetContent(string text)
        {
            lineCollection.Clear();
            if (text != null)
                Replace(offset: 0, length: 0, text);
        }

        public int GetVisibleLine(int logicalLineNumber)
        {
            if (!document.TextEditorProperties.EnableFolding)
                return logicalLineNumber;

            var visibleLine = 0;
            var foldEnd = 0;
            var foldings = document.FoldingManager.GetTopLevelFoldedFoldings();
            foreach (var fm in foldings)
            {
                if (fm.StartLine >= logicalLineNumber)
                    break;
                if (fm.StartLine >= foldEnd)
                {
                    visibleLine += fm.StartLine - foldEnd;
                    if (fm.EndLine > logicalLineNumber)
                        return visibleLine;
                    foldEnd = fm.EndLine;
                }
            }

//            Debug.Assert(logicalLineNumber >= foldEnd);
            visibleLine += logicalLineNumber - foldEnd;
            return visibleLine;
        }

        public int GetFirstLogicalLine(int visibleLineNumber)
        {
            if (!document.TextEditorProperties.EnableFolding)
                return visibleLineNumber;
            var v = 0;
            var foldEnd = 0;
            var foldings = document.FoldingManager.GetTopLevelFoldedFoldings();
            foreach (var fm in foldings)
                if (fm.StartLine >= foldEnd)
                {
                    if (v + fm.StartLine - foldEnd >= visibleLineNumber)
                        break;
                    v += fm.StartLine - foldEnd;
                    foldEnd = fm.EndLine;
                }

            // help GC
            foldings.Clear();
            return foldEnd + visibleLineNumber - v;
        }

        public int GetLastLogicalLine(int visibleLineNumber)
        {
            if (!document.TextEditorProperties.EnableFolding)
                return visibleLineNumber;
            return GetFirstLogicalLine(visibleLineNumber + 1) - 1;
        }

        // TODO : speedup the next/prev visible line search
        // HOW? : save the foldings in a sorted list and lookup the
        //        line numbers in this list
        public int GetNextVisibleLineAbove(int lineNumber, int lineCount)
        {
            var curLineNumber = lineNumber;
            if (document.TextEditorProperties.EnableFolding)
                for (var i = 0; i < lineCount && curLineNumber < TotalNumberOfLines; ++i)
                {
                    ++curLineNumber;
                    while (curLineNumber < TotalNumberOfLines && (curLineNumber >= lineCollection.Count || !document.FoldingManager.IsLineVisible(curLineNumber)))
                        ++curLineNumber;
                }
            else
                curLineNumber += lineCount;

            return Math.Min(TotalNumberOfLines - 1, curLineNumber);
        }

        public int GetNextVisibleLineBelow(int lineNumber, int lineCount)
        {
            var curLineNumber = lineNumber;
            if (document.TextEditorProperties.EnableFolding)
                for (var i = 0; i < lineCount; ++i)
                {
                    --curLineNumber;
                    while (curLineNumber >= 0 && !document.FoldingManager.IsLineVisible(curLineNumber))
                        --curLineNumber;
                }
            else
                curLineNumber -= lineCount;

            return Math.Max(val1: 0, curLineNumber);
        }

        private DelimiterSegment NextDelimiter(string text, int offset)
        {
            for (var i = offset; i < text.Length; i++)
            {
                bool newLineSet = false;
                switch (text[i])
                {
                    case '\r':
                        if (i + 1 < text.Length && text[i + 1] == '\n')
                        {
                            delimiterSegment.Offset = i;
                            delimiterSegment.Length = 2;
                            delimiterSegment.EolMarker = EolMarker.CrLf;
                            return delimiterSegment;
                        }

#if DATACONSISTENCYTEST
                        Debug.Assert(condition: false, "Found lone \\r, data consistency problems?");
#endif
                        newLineSet = true;
                        delimiterSegment.EolMarker = EolMarker.Cr;
                        goto case '\n';
                    case '\n':
                        delimiterSegment.Offset = i;
                        delimiterSegment.Length = 1;
                        if (!newLineSet)
                        {
                            delimiterSegment.EolMarker = EolMarker.Lf;
                        }
                        return delimiterSegment;
                }
            }
            return null;
        }

        private void OnLineCountChanged(LineCountChangeEventArgs e)
        {
            LineCountChanged?.Invoke(this, e);
        }

        private void OnLineLengthChanged(LineLengthChangeEventArgs e)
        {
            LineLengthChanged?.Invoke(this, e);
        }

        private void OnLineDeleted(LineEventArgs e)
        {
            LineDeleted?.Invoke(this, e);
        }

        public event EventHandler<LineLengthChangeEventArgs> LineLengthChanged;
        public event EventHandler<LineCountChangeEventArgs> LineCountChanged;
        public event EventHandler<LineEventArgs> LineDeleted;

        private sealed class DelimiterSegment
        {
            internal EolMarker EolMarker = EolMarker.None;
            internal int Length;
            internal int Offset;
        }
    }
}
