using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Image = System.Drawing.Image;
using Point = System.Drawing.Point;

namespace Inspired.ClickThrough.Business
{
    public class Game
    {
        public int Monitor          { get; set; }
        public int Clicks           { get { return clicks; } set { clicks = value; } }
        
        public TimeSpan Interval    { get; set; }
        public TimeSpan Spawn       { get; set; }
        public PictureBox Preview   { get; set; }

        private readonly object locker = new object();
        private readonly ManualResetEvent busy = new ManualResetEvent(false);
        private readonly BackgroundWorker detectWorker = new BackgroundWorker();
        private readonly BackgroundWorker executeWorker = new BackgroundWorker();
        private readonly BackgroundWorker addClicksWorker = new BackgroundWorker();

        private int clicks;
        private Bitmap current;
        private Rectangle bounds;
        private readonly List<Task> tasks = new List<Task>();
        private readonly Dictionary<Point, DateTime> tracker = new Dictionary<Point, DateTime>();
        private readonly ColorFiltering keepYellowOnly = new ColorFiltering
        {
            Red   = new IntRange( 255,  255),
            Green = new IntRange(   0,  255),
            Blue  = new IntRange(   0,  255),
            FillColor = new RGB(Color.Black),
            FillOutsideRange = true
        };

        private readonly Template[] templates = new[]
        {
            Template.Create("Close"    , 0, Color.Blue   , Priority.Highest, true ),
            //Template.Create("Refresh"  , 0, Color.Blue   , Priority.Highest, true ),
            Template.Create("Maximize" , 0, Color.Blue   , Priority.Highest, true ),
            //Template.Create("Ok"       , 0, Color.Blue   , Priority.High   , false),
            Template.Create("Student1" , 1, Color.Yellow , Priority.High   , true ),
            Template.Create("Student2" , 1, Color.Yellow , Priority.High   , true ),
            Template.Create("BioHazard", 1, Color.Red    , Priority.Medium , true ),
            Template.Create("Coin"     , 1, Color.Yellow , Priority.Low    , true ),
            Template.Create("Material" , 1, Color.Brown  , Priority.Low    , true )
        };

        private readonly Dictionary<string, Bitmap> numbers = new Dictionary<string, Bitmap>
        {
            { "0", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\0.jpg") },
            { "1", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\1.jpg") },
            { "2", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\2.jpg") },
            { "3", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\3.jpg") },
            { "4", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\4.jpg") },
            { "5", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\5.jpg") },
            { "6", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\6.jpg") },
            { "7", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\7.jpg") },
            { "8", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\8.jpg") },
            { "9", (Bitmap) Bitmap.FromFile(@"..\..\Resources\SimCitySocial\9.jpg") }
        };

        public event LogHandler Log;
        public delegate void LogHandler(string message);
        
        public void Start()
        {
            clicks = 0;
            bounds = Screen.AllScreens[this.Monitor].Bounds;
            current = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);

            //current = new Bitmap(@"C:\Users\Rubens\Desktop\newicon.png");
            //Detect(null, null);

            detectWorker.DoWork += Detect;
            executeWorker.DoWork += Execute;
            addClicksWorker.DoWork += AddClicks;

            detectWorker.RunWorkerAsync();
            executeWorker.RunWorkerAsync();
            addClicksWorker.RunWorkerAsync();

            Play();
        }

        private void Detect(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                using (Graphics g = Graphics.FromImage(current))
                {
                    Mouse.Click(new Point(bounds.Location.X + 10, bounds.Location.Y + 10), new[] { Mouse.MouseEvent.LeftDown, Mouse.MouseEvent.LeftUp });
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                    lock (locker)
                    {
                        tasks.Clear();
                    }

                    ExhaustiveTemplateMatching actionMatching = new ExhaustiveTemplateMatching(0.85f);
                    BlobCounter actions = new BlobCounter { FilterBlobs = true, MinWidth =  30, MinHeight = 30, MaxWidth =  42, MaxHeight = 42 };
                    BlobCounter buttons = new BlobCounter { FilterBlobs = true, MinWidth = 140, MinHeight = 40, MaxWidth = 175, MaxHeight = 50 };
                    Bitmap yellowOnly = keepYellowOnly.Apply(current);
                    actions.ProcessImage(yellowOnly);
                    buttons.ProcessImage(yellowOnly);

                    ////new ColorFiltering
                    ////{
                    ////    Red = new IntRange(00, 75),
                    ////    Green = new IntRange(00, 75),
                    ////    Blue = new IntRange(00, 75),
                    ////    FillColor = new RGB(Color.White),
                    ////    FillOutsideRange = true
                    ////}.ApplyInPlace(current);
                    ////numbers.ProcessImage(current);

                    //int i = 0;

                    ////keepYellowOnly.ApplyInPlace(current);   // output image with color filter applied
                    ////foreach (Blob blob in buttons.GetObjectsInformation().Concat(actions.GetObjectsInformation()))
                    ////    if (!templates.Where(t => t.Icon.Height <= blob.Rectangle.Height && t.Icon.Width <= blob.Rectangle.Width)
                    ////                    .Any(t => exhaustive.ProcessImage(current, t.Icon, blob.Rectangle).Length == 0))
                    //foreach (Blob blob in buttons.GetObjectsInformation().Concat(actions.GetObjectsInformation()))
                    //    {
                    //        using (Bitmap icon = new Bitmap(blob.Rectangle.Width, blob.Rectangle.Height))
                    //        using (Graphics g1 = Graphics.FromImage(icon))
                    //        {
                    //            icon.SetResolution(current.HorizontalResolution, current.VerticalResolution);
                    //            g1.DrawImage(current, 0, 0, blob.Rectangle, GraphicsUnit.Pixel);
                    //            icon.Save(String.Format(@"C:\Users\Rubens\Desktop\simcity\{0}.jpg", ++i), ImageFormat.Jpeg);
                    //        }
                    //        g.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.Yellow)), blob.Rectangle);
                    //    }
                    ////current.Save(@"C:\Users\Rubens\Desktop\newIconXXX.png");

                    #region Identify actions and buttons

                    foreach (Blob blob in actions.GetObjectsInformation().Concat(
                                          buttons.GetObjectsInformation()))
                    {
                        foreach (var template in templates.Where(t => t.Icon.Height <= blob.Rectangle.Height && t.Icon.Width <= blob.Rectangle.Width))
                        foreach (var match in actionMatching.ProcessImage(current, template.Icon, blob.Rectangle))
                        {
                            Rectangle target = CalculateOffset(match.Rectangle, template.Offset);
                            g.FillRectangle(new SolidBrush(Color.FromArgb(128, template.Color)), match.Rectangle);
                            g.FillRectangle(Brushes.HotPink, target);   // click point

                            if (!template.AutoClick)
                                continue;   // Auto level up?

                            Point location = TranslateScreenCoordinates(target);
                            Task task = new Task
                            {
                                Priority    = template.Priority,
                                Location    = location,
                                MouseEvents = new[] { Mouse.MouseEvent.LeftDown, Mouse.MouseEvent.LeftUp },
                                Type        = template.Name,
                                LastClick   = tracker.ContainsKey(location) ? tracker[location]: DateTime.MinValue,
                                Cost        = template.Cost
                            };

                            lock (locker)
                            {
                                // no existing task on same location
                                if (tasks.All(t => t.Location != task.Location))
                                {
                                    tasks.Add(task);
                                    System.Threading.Monitor.Pulse(locker);
                                }
                            }
                        }
                    }

                    #endregion

                    #region Get application counters

                    int i = 0;
                    BlobCounter counters = new BlobCounter { FilterBlobs = true, MinWidth = 60, MinHeight = 23, MaxWidth = 115, MaxHeight = 25 };
                    ExhaustiveTemplateMatching numberMatching = new ExhaustiveTemplateMatching(0.63f);
                    //new Sharpen().ApplyInPlace(yellowOnly);
                    counters.ProcessImage(yellowOnly);
                    Debug.WriteLine(new string('-', 20));
                    foreach (Blob blob in counters.GetObjectsInformation())
                    {
                        Rectangle adjusted = blob.Rectangle;
                        adjusted.Width += 10;
                        adjusted.X -= 5;

                        using (Bitmap icon = new Bitmap(adjusted.Width, adjusted.Height))
                        using (Graphics g1 = Graphics.FromImage(icon))
                        {
                            icon.SetResolution(current.HorizontalResolution, current.VerticalResolution);
                            g1.DrawImage(current, 0, 0, adjusted, GraphicsUnit.Pixel);
                            //icon.Save(String.Format(@"C:\Users\Rubens\Desktop\simcity\{0}.jpg", ++i), ImageFormat.Jpeg);
                        }
                        g.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.Lime)), blob.Rectangle);

                        Dictionary<int, string> sequence = new Dictionary<int, string>();
                        foreach (var number in numbers)
                        foreach (var match in numberMatching.ProcessImage(current, number.Value, adjusted))
                        {
                            if (!sequence.ContainsKey(match.Rectangle.X))
                                sequence.Add(match.Rectangle.X, number.Key);
                        }

                        Debug.WriteLine(String.Format("{0}{1}", adjusted, String.Join("", sequence.OrderBy(n => n.Key).Select(n => n.Value).ToArray())));
                    }

                    #endregion

                    Refresh();
                }
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }

        private void Execute(object sender, DoWorkEventArgs e)
        {
            while(true)
            {
                Task task;
                lock (locker)
                {
                    // while no new tasks, or not enough clicks for any tasks, wait
                    while (tasks.Count == 0 || !tasks.Any(t => t.Cost <= clicks))
                        System.Threading.Monitor.Wait(locker);

                    tasks.Remove(task = tasks
                        .Where(t => t.Cost <= clicks)
                        .OrderByDescending(t => (int)t.Priority)
                        .ThenBy(t => t.Cost)
                        .ThenBy(t => t.LastClick)
                        .First());
                }

                busy.WaitOne();

                if (Log != null)
                    Log(String.Format("{0:MMM dd, HH:mm:ss} {1} {2}", DateTime.Now, task.Location, task.Type));

                // keep track of last click at given location, avoiding click on same place consecutive times
                if(!tracker.ContainsKey(task.Location))
                    tracker.Add(task.Location, DateTime.MinValue);
                tracker[task.Location] = DateTime.Now;

                Mouse.Click(task.Location, task.MouseEvents);

                Thread.Sleep(TimeSpan.FromSeconds(2));
                Interlocked.Add(ref clicks, -task.Cost);
            }
        }

        private void AddClicks(object sender, DoWorkEventArgs e)
        {
            while(true)
            {
                Thread.Sleep(TimeSpan.FromMinutes(3));
                lock (locker)
                {
                    if (clicks < 35)
                    {
                        Interlocked.Increment(ref clicks);
                        System.Threading.Monitor.Pulse(locker);
                    }
                }
            }
        }

        private Rectangle CalculateOffset(Rectangle rectangle, Point point)
        {
            rectangle.Offset(point);
            rectangle.Width = rectangle.Height = 10;
            return rectangle;
        }

        private Point TranslateScreenCoordinates(Rectangle rectangle)
        {
            return new Point(bounds.X + rectangle.X, bounds.Y + rectangle.Y);
        }

        public void Play()
        {
            busy.Set();
        }

        public void Pause()
        {
            busy.Reset();
        }

        public void Refresh()
        {
            this.Refresh(current.Clone() as Bitmap, this.Preview);
        }

        private void Refresh(Image image, PictureBox pictureBox)
        {
            if (pictureBox.FindForm().WindowState == FormWindowState.Minimized)
                return;

            int sourceWidth = image.Width;
            int sourceHeight = image.Height;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)pictureBox.Size.Width / (float)sourceWidth);
            nPercentH = ((float)pictureBox.Size.Height / (float)sourceHeight);

            nPercent = nPercentH < nPercentW ? nPercentH : nPercentW;

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap b = new Bitmap(destWidth, destHeight);
            Graphics g = Graphics.FromImage(b);
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, destWidth, destHeight);
                pictureBox.Image = b;
            }
        }
    }
}
