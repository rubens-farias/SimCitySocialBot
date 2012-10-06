using System;
using System.Drawing;

namespace Inspired.ClickThrough.Business
{
    class Task
    {
        public int Cost                         { get; set; }
        public int Amount                       { get; set; }
        public Priority Priority                { get; set; }
        public RewardType RewardType            { get; set; }
        public Point Location                   { get; set; }
        public Mouse.MouseEvent[] MouseEvents   { get; set; }
    }
}
