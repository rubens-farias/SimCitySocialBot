﻿using System;
using System.Drawing;

namespace Inspired.ClickThrough.Business
{
    class Task
    {
        public int Cost                         { get; set; }
        public Priority Priority                { get; set; }
        public string Type                      { get; set; }
        public Point Location                   { get; set; }
        public DateTime LastClick               { get; set; }
        public TimeSpan Delay                   { get; set; }
        public bool Refresh                     { get; set; }
        public float Similarity                 { get; set; }
        public Mouse.MouseEvent[] MouseEvents   { get; set; }
    }
}
