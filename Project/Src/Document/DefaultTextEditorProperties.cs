// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="none" email=""/>
//     <version>$Revision$</version>
// </file>

using System.Drawing;
using System.Drawing.Text;
using System.Text;

namespace ICSharpCode.TextEditor.Document
{
    public enum BracketMatchingStyle
    {
        Before,
        After
    }

    public class DefaultTextEditorProperties : ITextEditorProperties
    {
        private static Font DefaultFont;

        public DefaultTextEditorProperties()
        {
            if (DefaultFont == null)
                DefaultFont = new Font("Courier New", emSize: 10);
            FontContainer = new FontContainer(DefaultFont);
        }

        public int TabIndent { get; set; } = 4;

        public int IndentationSize { get; set; } = 4;

        public IndentStyle IndentStyle { get; set; } = IndentStyle.Smart;

        public bool CaretLine { get; set; } = false;

        public DocumentSelectionMode DocumentSelectionMode { get; set; } = DocumentSelectionMode.Normal;

        public bool AllowCaretBeyondEOL { get; set; } = false;

        public bool ShowMatchingBracket { get; set; } = true;

        public bool ShowLineNumbers { get; set; } = true;

        public bool ShowSpaces { get; set; } = false;

        public bool ShowTabs { get; set; } = false;

        public EolMarkerStyle EolMarkerStyle { get; set; } = EolMarkerStyle.None;

        public bool ShowInvalidLines { get; set; } = false;

        public bool IsIconBarVisible { get; set; } = false;

        public bool EnableFolding { get; set; } = true;

        public bool ShowHorizontalRuler { get; set; } = false;

        public bool ShowVerticalRuler { get; set; } = true;

        public bool ConvertTabsToSpaces { get; set; } = false;

        public TextRenderingHint TextRenderingHint { get; set; } = TextRenderingHint.SystemDefault;

        public bool MouseWheelScrollDown { get; set; } = true;

        public bool MouseWheelTextZoom { get; set; } = true;

        public bool HideMouseCursor { get; set; } = false;

        public bool CutCopyWholeLine { get; set; } = true;

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public int VerticalRulerRow { get; set; } = 80;

        public LineViewerStyle LineViewerStyle { get; set; } = LineViewerStyle.None;

        public string LineTerminator { get; set; } = "\r\n";

        public bool AutoInsertCurlyBracket { get; set; } = true;

        public Font Font
        {
            get => FontContainer.DefaultFont;
            set => FontContainer.DefaultFont = value;
        }

        public FontContainer FontContainer { get; }

        public BracketMatchingStyle BracketMatchingStyle { get; set; } = BracketMatchingStyle.After;

        public bool SupportReadOnlySegments { get; set; } = false;
    }
}
