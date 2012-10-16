using System;
using System.Drawing;

namespace Inspired.ClickThrough.Business
{
    public class Template
    {
        public string   Name      { get; set; }
        public int      Cost      { get; set; }
        public Color    Color     { get; set; }
        public Priority Priority  { get; set; }
        public bool     AutoClick { get; set; }
        public Point    Offset    { get; set; }
        public Bitmap   Icon      { get; set; }

        public static Template Create(string name, int cost, Color color, Priority priority, bool autoClick)
        {
            return new Template
            {
                Name      = name,
                Cost      = cost,
                Color     = color,
                Priority  = priority,
                AutoClick = autoClick,
                Offset    = new Point(12, 12),
                Icon      = (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\" + name + ".jpg")
            };
        }
    }
}