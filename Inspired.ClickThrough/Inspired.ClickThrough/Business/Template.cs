using System;
using System.Drawing;

namespace Inspired.ClickThrough.Business
{
    public class Template
    {
        public int      Cost     { get; set; }
        public string   Name     { get; set; }
        public Color    Color    { get; set; }
        public Bitmap   Icon     { get; set; }
        public Priority Priority { get; set; }
        public Point    Offset   { get; set; }
    }
}