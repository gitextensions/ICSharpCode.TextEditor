// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using ICSharpCode.TextEditor.Document;

namespace ICSharpCode.TextEditor.Util
{
    public class RtfWriter
    {
        private static Dictionary<string, int> colors;
        private static int colorNum;
        private static StringBuilder colorString;

        public static string GenerateRtf(TextArea textArea)
        {
            colors = new Dictionary<string, int>();
            colorNum = 0;
            colorString = new StringBuilder();

            var rtf = new StringBuilder();

            rtf.Append(@"{\rtf1\ansi\ansicpg1252\deff0\deflang1031");
            BuildFontTable(textArea.Document, rtf);
            rtf.Append(value: '\n');

            var fileContent = BuildFileContent(textArea);
            BuildColorTable(rtf);
            rtf.Append(value: '\n');
            rtf.Append(@"\viewkind4\uc1\pard");
            rtf.Append(fileContent);
            rtf.Append('}');
            return rtf.ToString();
        }

        private static void BuildColorTable(StringBuilder rtf)
        {
            rtf.Append(@"{\colortbl ;");
            rtf.Append(colorString);
            rtf.Append('}');
        }

        private static void BuildFontTable(IDocument doc, StringBuilder rtf)
        {
            rtf.Append(@"{\fonttbl");
            rtf.Append(@"{\f0\fmodern\fprq1\fcharset0 ").Append(doc.TextEditorProperties.Font.Name).Append(";}");
            rtf.Append('}');
        }

        private static string BuildFileContent(TextArea textArea)
        {
            var rtf = new StringBuilder();
            var firstLine = true;
            var curColor = SystemColors.WindowText;
            var oldItalic = false;
            var oldBold = false;
            var escapeSequence = false;

            foreach (ISelection selection in CollectionsMarshal.AsSpan(textArea.SelectionManager.SelectionCollection))
            {
                var selectionOffset = textArea.Document.PositionToOffset(selection.StartPosition);
                var selectionEndOffset = textArea.Document.PositionToOffset(selection.EndPosition);
                for (var i = selection.StartPosition.Y; i <= selection.EndPosition.Y; ++i)
                {
                    var line = textArea.Document.GetLineSegment(i);
                    var offset = line.Offset;
                    if (line.Words == null)
                        continue;

                    foreach (TextWord word in CollectionsMarshal.AsSpan(line.Words))
                        switch (word.Type)
                        {
                            case TextWordType.Space:
                                if (selection.ContainsOffset(offset))
                                    rtf.Append(value: ' ');
                                ++offset;
                                break;

                            case TextWordType.Tab:
                                if (selection.ContainsOffset(offset))
                                    rtf.Append(@"\tab");
                                ++offset;
                                escapeSequence = true;
                                break;

                            case TextWordType.Word:
                                var c = word.Color;

                                if (offset + word.Word.Length > selectionOffset && offset < selectionEndOffset)
                                {
                                    var colorstr = c.R + ", " + c.G + ", " + c.B;

                                    if (!colors.TryGetValue(colorstr, out int color))
                                    {
                                        color = ++colorNum;
                                        colors[colorstr] = color;
                                        colorString.Append(@"\red").Append(c.R)
                                            .Append(@"\green").Append(c.G)
                                            .Append(@"\blue").Append(c.B)
                                            .Append(';');
                                    }

                                    if (c != curColor || firstLine)
                                    {
                                        rtf.Append(@"\cf").Append(color);
                                        curColor = c;
                                        escapeSequence = true;
                                    }

                                    if (oldItalic != word.Italic)
                                    {
                                        if (word.Italic)
                                            rtf.Append(@"\i");
                                        else
                                            rtf.Append(@"\i0");
                                        oldItalic = word.Italic;
                                        escapeSequence = true;
                                    }

                                    if (oldBold != word.Bold)
                                    {
                                        if (word.Bold)
                                            rtf.Append(@"\b");
                                        else
                                            rtf.Append(@"\b0");
                                        oldBold = word.Bold;
                                        escapeSequence = true;
                                    }

                                    if (firstLine)
                                    {
                                        rtf.Append(@"\f0\fs").Append(textArea.TextEditorProperties.Font.Size * 2);
                                        firstLine = false;
                                    }

                                    if (escapeSequence)
                                    {
                                        rtf.Append(value: ' ');
                                        escapeSequence = false;
                                    }

                                    string printWord;
                                    if (offset < selectionOffset)
                                        printWord = word.Word.Substring(selectionOffset - offset);
                                    else if (offset + word.Word.Length > selectionEndOffset)
                                        printWord = word.Word.Substring(startIndex: 0, offset + word.Word.Length - selectionEndOffset);
                                    else
                                        printWord = word.Word;

                                    AppendText(rtf, printWord);
                                }

                                offset += word.Length;
                                break;
                        }
                    if (offset < selectionEndOffset)
                        rtf.Append(@"\par");
                    rtf.Append(value: '\n');
                }
            }

            return rtf.ToString();
        }

        private static void AppendText(StringBuilder rtfOutput, string text)
        {
            //rtf.Append(printWord.Replace(@"\", @"\\").Replace("{", "\\{").Replace("}", "\\}"));
            foreach (var c in text)
                switch (c)
                {
                    case '\\':
                        rtfOutput.Append(@"\\");
                        break;
                    case '{':
                        rtfOutput.Append("\\{");
                        break;
                    case '}':
                        rtfOutput.Append("\\}");
                        break;
                    default:
                        if (c < 256)
                            rtfOutput.Append(c);
                        else
                            rtfOutput.Append("\\u").Append(unchecked((short)c)).Append('?');
                        break;
                }
        }
    }
}
