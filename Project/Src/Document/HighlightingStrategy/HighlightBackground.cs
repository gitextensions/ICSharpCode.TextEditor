﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System.Drawing;
using System.Xml;

namespace ICSharpCode.TextEditor.Document
{
    /// <summary>
    ///     Extens the highlighting color with a background image.
    /// </summary>
    public class HighlightBackground : HighlightColor
    {
        /// <summary>
        ///     Creates a new instance of <see cref="HighlightBackground" />
        /// </summary>
        public HighlightBackground(XmlElement el) : base(el)
        {
            if (el.Attributes["image"] != null)
                BackgroundImage = new Bitmap(el.Attributes["image"].InnerText);
        }

        /// <summary>
        ///     Creates a new instance of <see cref="HighlightBackground" />
        /// </summary>
        public HighlightBackground(string systemColor, string systemBackgroundColor, bool bold, bool italic) : base(systemColor, systemBackgroundColor, bold, italic)
        {
        }

        /// <value>
        ///     The image used as background
        /// </value>
        public Image BackgroundImage { get; }
    }
}