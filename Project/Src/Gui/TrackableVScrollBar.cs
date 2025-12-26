using System.Windows.Forms;

namespace ICSharpCode.TextEditor;

/// <summary>
/// A vertical scrollbar control that tracks mouse down events to detect when the user is actively dragging the scrollbar.
/// </summary>
public class TrackableVScrollBar : VScrollBar
{
    const int WM_LBUTTONDOWN = 0x0201;

    /// <summary>
    /// Gets a value indicating whether the mouse button is currently pressed down on the scrollbar.
    /// </summary>
    public bool IsMouseDown { get; private set; }

    /// <summary>
    /// Occurs when the user finishes scrolling and releases the mouse button.
    /// </summary>
    public event ScrollEventHandler MouseScrollEnded;

    protected override void OnScroll(ScrollEventArgs e)
    {
        if (e.Type == ScrollEventType.EndScroll)
        {
            if (IsMouseDown)
            {
                IsMouseDown = false;

                MouseScrollEnded?.Invoke(this, e);
            }
        }

        base.OnScroll(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_LBUTTONDOWN)
        {
            IsMouseDown = true;
        }

        base.WndProc(ref m);
    }
}
