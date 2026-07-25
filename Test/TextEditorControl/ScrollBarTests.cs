using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FluentAssertions;
using NUnit.Framework;

namespace ICSharpCode.TextEditor.Tests.TextEditorControl;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class ScrollBarTests
{
    private Form _form;
    private ICSharpCode.TextEditor.TextEditorControl _textEditorControl;

    [TearDown]
    public void TearDown()
    {
        _form.Dispose();
    }

    [Test]
    public void TextEditorControl_with_text_requiring_both_scrollbars_should_show_both()
    {
        SetupForm(width: 300, height: 276);

        _textEditorControl.Text = """
          This is a
          multi
          row
          text
          to
          almost
          get
          a
          scrollbar
          at a
          bottom row
          which
          is
          lower and very long and would only show HScrollBar while visible, but would get invisible once the HScrollBar is visible
          """;

        Application.DoEvents();

        _textEditorControl.ActiveTextAreaControl.VScrollBar.Visible.Should().BeTrue();
        _textEditorControl.ActiveTextAreaControl.HScrollBar.Visible.Should().BeTrue();
    }

    [Test]
    public void TextEditorControl_with_last_partialy_hidden_by_horizontal_scrollbar_should_cause_vertical_scrollbar()
    {
        SetupForm(width: 300, height: 270);

        _textEditorControl.Text = """
          This is a
          multi
          row
          text
          positioned
          to
          only
          show
          last
          line
          partially
          which
          should still cause VScrollBar to be visible. Some more text to make this line really long.
          """;

        Application.DoEvents();

        _textEditorControl.ActiveTextAreaControl.VScrollBar.Visible.Should().BeTrue();
        _textEditorControl.ActiveTextAreaControl.HScrollBar.Visible.Should().BeTrue();
    }

    [Test]
    public void TextEditorControl_with_partial_lines_at_top_and_bottom_should_take_last_lines_width_into_account()
    {
        SetupForm(width: 300, height: 238);

        _textEditorControl.Text = """
          This is a
          multi
          row
          text
          scrolled
          to show only some
          line
          at
          the top
          and a very long line
          at
          the bottom
          thus if you have 0.1 line at the top, 0.1 line at the bottom and 3 lines in between, you should calculate the width of 5 lines.
          """;

        Application.DoEvents();

        _textEditorControl.ActiveTextAreaControl.VScrollBar.Value = 12;

        Application.DoEvents();

        _textEditorControl.ActiveTextAreaControl.VScrollBar.Visible.Should().BeTrue();
        _textEditorControl.ActiveTextAreaControl.HScrollBar.Visible.Should().BeTrue();
    }

    [Test]
    public void TextEditorControl_with_vScrollBar_should_have_no_scrollbars_if_not_necessary_after_update()
    {
        SetupForm(width: 300, height: 250);

        // First, set text to require vertical scrollbar
        _textEditorControl.Text = string.Join("\r\n", Enumerable.Repeat("a", 16));

        _textEditorControl.Text = """
          This is a line that would fit
          without a VScrollBar
          but now it doesn't.
          Also
          this
          text
          is
          long
          enough
          to
          "threatten"
          a VScrollBar
          """;

        Application.DoEvents();

        _textEditorControl.ActiveTextAreaControl.VScrollBar.Visible.Should().BeFalse();
        _textEditorControl.ActiveTextAreaControl.HScrollBar.Visible.Should().BeFalse();
    }

    [Test]
    public void HScrollBar_should_be_shown_when_wide_glyphs_exceed_the_visible_width()
    {
        SetupForm(width: 300, height: 200);

        var textAreaControl = _textEditorControl.ActiveTextAreaControl;
        var view = textAreaControl.TextArea.TextView;
        var font = view.TextEditorProperties.FontContainer.RegularFont;

        int glyphWidth;
        using (var g = textAreaControl.CreateGraphics())
        {
            glyphWidth = view.GetWidth(g, WideGlyph, font);
        }

        // the test is only meaningful if the glyph really is wider than one column
        glyphWidth.Should().BeGreaterThan(view.WideSpaceWidth);

        // exactly as many characters as there are visible columns: counting characters sees no
        // overflow at all, while the glyphs need roughly twice that width to be painted
        _textEditorControl.Text = new string(WideGlyph, view.DrawingPosition.Width / view.WideSpaceWidth);

        Application.DoEvents();

        textAreaControl.HScrollBar.Visible.Should().BeTrue();
    }

    [Test]
    public void HScrollBar_should_allow_scrolling_to_the_end_of_a_line_of_wide_glyphs()
    {
        SetupForm(width: 300, height: 200);

        _textEditorControl.Text = new string(WideGlyph, 100);

        Application.DoEvents();

        var textAreaControl = _textEditorControl.ActiveTextAreaControl;
        var view = textAreaControl.TextArea.TextView;
        var font = view.TextEditorProperties.FontContainer.RegularFont;

        int lineWidth;
        using (var g = textAreaControl.CreateGraphics())
        {
            view.GetWidth(g, WideGlyph, font).Should().BeGreaterThan(view.WideSpaceWidth);
            lineWidth = view.GetWidth(g, _textEditorControl.Text, font);
        }

        // dragging the thumb all the way to the right stops at Maximum - LargeChange + 1;
        // only setting HScrollBar.Value programmatically can go as far as Maximum
        var hScrollBar = textAreaControl.HScrollBar;
        var rightmostThumbValue = hScrollBar.Maximum - hScrollBar.LargeChange + 1;
        var rightmostVisiblePixel = (rightmostThumbValue * view.WideSpaceWidth) + view.DrawingPosition.Width;

        rightmostVisiblePixel.Should().BeGreaterThanOrEqualTo(lineWidth);
    }

    /// <summary>U+4E2D, a CJK ideograph: a single character which is painted about two columns wide.</summary>
    private const char WideGlyph = (char)0x4E2D;

    private void SetupForm(int width, int height)
    {
        _form = new Form
        {
            Width = width,
            Height = height,
        };
        _textEditorControl = new TextEditor.TextEditorControl { Parent = _form, Dock = DockStyle.Fill };

        _form.Show();

        Application.DoEvents();
    }
}
