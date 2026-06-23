// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Windows.Forms;

namespace ICSharpCode.TextEditor.Util
{
    /// <summary>
    ///     Accumulates mouse wheel deltas and reports the actual number of lines to scroll.
    /// </summary>
    internal sealed class MouseWheelHandler
    {
        // CODE DUPLICATION: See ICSharpCode.SharpDevelop.Widgets.MouseWheelHandler

        private const int WHEEL_DELTA = 120;

        private int mouseWheelDelta;

        public int GetScrollAmount(MouseEventArgs e)
        {
            // accumulate the delta to support high-resolution mice
            mouseWheelDelta += e.Delta;

            var linesPerClick = Math.Max(SystemInformation.MouseWheelScrollLines, val2: 1);

            var scrollDistance = mouseWheelDelta*linesPerClick/WHEEL_DELTA;
            mouseWheelDelta %= Math.Max(val1: 1, WHEEL_DELTA/linesPerClick);
            return scrollDistance;
        }
    }
}
