using System;
using System.Drawing;

namespace ICSharpCode.TextEditor.Util
{
    public static class Win32Util
    {
        public static Point ToPoint(this IntPtr lparam) =>
            new Point(unchecked((int)lparam.ToInt64()));

        public static int SignedLOWORD(nint n)
            => SignedLOWORD(unchecked((int)n));

        public static int SignedHIWORD(nint n)
            => SignedHIWORD(unchecked((int)n));

        public static int SignedHIWORD(int n)
            => (int)(short)HIWORD(n);

        public static int HIWORD(int n)
            => (n >> 16) & 0xffff;

        public static int LOWORD(int n)
            => n & 0xffff;

        public static int SignedLOWORD(int n)
            => (int)(short)LOWORD(n);
    }
}
