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
        private readonly AutoResetEvent capture = new AutoResetEvent(true);
        private readonly BackgroundWorker detectWorker = new BackgroundWorker();
        private readonly BackgroundWorker executeWorker = new BackgroundWorker();
        private readonly BackgroundWorker addClicksWorker = new BackgroundWorker();

        private int clicks;
        private Bitmap current;
        private Rectangle bounds;
        private Random random = new Random();
        private TimeSpan randomDelay = TimeSpan.FromSeconds(5);
        private readonly List<Task> tasks = new List<Task>();
        private readonly Dictionary<Point, DateTime> tracker = new Dictionary<Point, DateTime>();
        private readonly ColorFiltering keepYellowOnly = new ColorFiltering
        {
            Red   = new IntRange(254, 255),
            Green = new IntRange(254, 255),
            Blue  = new IntRange(254, 255),
            FillColor = new RGB(Color.Black),
            FillOutsideRange = true
        };

        private readonly Template[] templates = new[]
        {
            Template.Create("Close1"        , 0, Color.Blue   , Priority.Lowest , TimeSpan.Zero, true, true ),
            Template.Create("Close2"        , 0, Color.Blue   , Priority.Lowest , TimeSpan.Zero, true, true ),
            Template.Create("Maximize"      , 0, Color.Blue   , Priority.Highest, TimeSpan.Zero, true, true ),
            Template.Create("Refresh"       , 0, Color.Blue   , Priority.High   , TimeSpan.Zero, true, true ),
            Template.Create("Diamond"       , 1, Color.Purple , Priority.Highest, TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Ok"            , 0, Color.Blue   , Priority.High   , TimeSpan.FromSeconds(1.0), false, true),    // Auto level up: off
            Template.Create("Help"          , 0, Color.Blue   , Priority.High   , TimeSpan.FromSeconds(1.0), true , true),
            Template.Create("SendBack"      , 0, Color.Blue   , Priority.High   , TimeSpan.FromSeconds(1.0), true , true),
            Template.Create("Accept"        , 0, Color.Blue   , Priority.High   , TimeSpan.FromSeconds(1.0), true , true),

            Template.Create("Burned"        , 1, Color.Red    , Priority.High   , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Quarantined"   , 1, Color.Red    , Priority.High   , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Abandoned"     , 1, Color.Red    , Priority.High   , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("BioHazard"     , 1, Color.Red    , Priority.High   , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Vaporized"     , 1, Color.Red    , Priority.High   , TimeSpan.FromSeconds(2.0), true , false),

            Template.Create("Student1"      , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Student2"      , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),

            Template.Create("Corn1"         , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Corn2"         , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Artichokes1"   , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Artichokes2"   , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Tomatoes1"     , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Carrot1"       , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("SoyBeans1"     , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Eggplant1"     , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),

            Template.Create("Milk2"         , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Fleece1"       , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Fleece2"       , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Cow1"          , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Cow2"          , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Eggs1"         , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Eggs2"         , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Wool1"         , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Wool2"         , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Chicken1"      , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Chicken2"      , 1, Color.Green  , Priority.Medium , TimeSpan.FromSeconds(2.0), true , false),
            
            Template.Create("Coin"          , 1, Color.Yellow , Priority.Low    , TimeSpan.FromSeconds(2.0), true , false),
            Template.Create("Material"      , 1, Color.Brown  , Priority.Lowest    , TimeSpan.FromSeconds(2.0), true , false),
        };

        public event LogHandler Log;
        public delegate void LogHandler(string message);

        public event ClicksChangedHandler ClicksChanged;
        public delegate void ClicksChangedHandler(int clicks);
        
        public void Start()
        {
            clicks = 0;
            bounds = Screen.AllScreens[this.Monitor].Bounds;
            current = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);

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
                bool forced = capture.WaitOne(TimeSpan.FromSeconds(3));
                using (Graphics g = Graphics.FromImage(current))
                {
                    //WriteLog(String.Format("Detect, forced={0}", forced));
                    Mouse.Click(new Point(bounds.Location.X, bounds.Location.Y + 120), new[] { Mouse.MouseEvent.LeftDown, Mouse.MouseEvent.LeftUp });
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                    lock (locker)
                    {
                        tasks.Clear();
                    }

                    ExhaustiveTemplateMatching actionMatching = new ExhaustiveTemplateMatching { SimilarityThreshold = 0.88f };
                    ExhaustiveTemplateMatching buttonMatching = new ExhaustiveTemplateMatching { SimilarityThreshold = 0.90f };
                    BlobCounter actions = new BlobCounter { FilterBlobs = true, MinWidth = 25, MinHeight = 25, MaxWidth =  42, MaxHeight = 42 };
                    BlobCounter buttons = new BlobCounter { FilterBlobs = true, MinWidth = 60, MinHeight = 25, MaxWidth = 175, MaxHeight = 50 };
                    Bitmap yellowOnly = keepYellowOnly.Apply(current);
                    //keepYellowOnly.ApplyInPlace(current); Bitmap yellowOnly = current;
                    actions.ProcessImage(yellowOnly);
                    buttons.ProcessImage(yellowOnly);

                    //int n = 0;
                    ////keepYellowOnly.ApplyInPlace(current);   // output image with color filter applied
                    ////foreach (Blob blob in buttons.GetObjectsInformation().Concat(actions.GetObjectsInformation()))
                    ////    if (!templates.Where(t => t.Icon.Height <= blob.Rectangle.Height && t.Icon.Width <= blob.Rectangle.Width)
                    ////                    .Any(t => exhaustive.ProcessImage(current, t.Icon, blob.Rectangle).Length == 0))
                    //foreach (Blob blob in buttons.GetObjectsInformation().Concat(actions.GetObjectsInformation()))
                    //{
                    //    //if (blob.Rectangle.ToString().StartsWith("{X=1291,Y=406"))
                    //    //{
                    //    //    i += 0;
                    //    //    var x = actionMatching.ProcessImage(current, templates[11].Icon, blob.Rectangle);
                    //    //}
                    //    using (Bitmap icon = new Bitmap(blob.Rectangle.Width, blob.Rectangle.Height))
                    //    using (Graphics g1 = Graphics.FromImage(icon))
                    //    {
                    //        icon.SetResolution(current.HorizontalResolution, current.VerticalResolution);
                    //        g1.DrawImage(current, 0, 0, blob.Rectangle, GraphicsUnit.Pixel);
                    //        //g.DrawString(String.Format("{0}", blob.Rectangle),
                    //        //    new Font("Arial", 10, FontStyle.Bold), Brushes.Black, blob.Rectangle.Location);
                    //        icon.Save(String.Format(@"C:\Users\Rubens\Desktop\simcity\{0}.jpg", ++n), ImageFormat.Jpeg);
                    //    }
                    //    g.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.Yellow)), blob.Rectangle);
                    //}
                    ////current.Save(@"C:\Users\Rubens\Desktop\newIconXXX.png");

                    #region Identify actions and buttons

                    var items = 
                            actions.GetObjectsInformation()
                                   .SelectMany(a => templates.Where (t => t.Icon.Height <= a.Rectangle.Height &&
                                                                          t.Icon.Width  <= a.Rectangle.Width)
                                                             .Select(t => new
                                                             { 
                                                                 Template = t, 
                                                                 Blob = a, 
                                                                 Match = actionMatching.ProcessImage(current, t.Icon, a.Rectangle)
                                                                                       .OrderBy(i => i.Similarity).FirstOrDefault()
                                                             })).Concat(
                            buttons.GetObjectsInformation()
                                   .SelectMany(a => templates.Where (t => t.Icon.Height <= a.Rectangle.Height &&
                                                                          t.Icon.Width  <= a.Rectangle.Width)
                                                             .Select(t => new
                                                             { 
                                                                 Template = t, 
                                                                 Blob = a, 
                                                                 Match = buttonMatching.ProcessImage(current, t.Icon, a.Rectangle)
                                                                                       .OrderBy(i => i.Similarity).FirstOrDefault()
                                                             })))
                            .Where(i => i       != null)
                            .Where(i => i.Match != null)
                            .OrderByDescending(i => i.Match.Similarity);

                    foreach (var item in items)
                    {
                        Rectangle target = CalculateOffset(item.Match.Rectangle, item.Template.Offset);
                        g.FillRectangle(new SolidBrush(Color.FromArgb(128, item.Template.Color)), item.Match.Rectangle);
                        g.FillRectangle(Brushes.HotPink, target);   // click point
                        //g.DrawString(String.Format("{0},{1},{2} ({3:0.00})", target.Location, template.Priority, template.Name, match.Similarity),
                        //    new Font("Arial", 10, FontStyle.Bold), Brushes.Black, target.Location);

                        if (!item.Template.AutoClick)
                            continue;   // Auto level up?

                        Point location = TranslateScreenCoordinates(target);
                        Task task = new Task
                        {
                            Priority    = item.Template.Priority,
                            Location    = location,
                            MouseEvents = new[] { Mouse.MouseEvent.LeftDown, Mouse.MouseEvent.LeftUp },
                            Type        = item.Template.Name,
                            LastClick   = tracker.ContainsKey(location) ? tracker[location]: DateTime.MinValue,
                            Cost        = item.Template.Cost,
                            Delay       = item.Template.Delay,
                            Refresh     = item.Template.Refresh,
                            Similarity  = item.Match.Similarity
                        };

                        lock (locker)
                        {
                            // no existing task on same location
                            if (tasks.All(t => t.Location != task.Location))
                            {
                                tasks.Add(task);
                            }
                        }
                    }

                    lock (locker)
                    {   // Acumular as tarefas antes de executar
                        System.Threading.Monitor.Pulse(locker);
                    }

                    #endregion

                    Refresh();
                }
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

                    busy.WaitOne();

                    tasks.Remove(task = tasks
                        .Where(t => t.Cost <= clicks && t.Priority != Priority.None)
                        .OrderByDescending(t => (int)t.Priority)
                        .ThenBy(t => t.Cost)
                        .ThenBy(t => t.LastClick)
                        .First());

                    if (task.Refresh)
                    {
                        tasks.Clear();
                        capture.Set();
                    }
                }

                WriteLog(String.Format("{0} {1} {2:0.0%}", task.Location, task.Type, task.Similarity));

                // keep track of last click at given location, avoiding click on same place consecutive times
                if(!tracker.ContainsKey(task.Location))
                    tracker.Add(task.Location, DateTime.MinValue);
                tracker[task.Location] = DateTime.Now;

                Thread.Sleep(new TimeSpan((long)(randomDelay.Ticks * random.NextDouble())));

                Mouse.Click(task.Location, task.MouseEvents);

                Interlocked.Add(ref clicks, -task.Cost);

                if (ClicksChanged != null)
                    ClicksChanged(clicks);

                Thread.Sleep(task.Delay);
            }
        }

        private void AddClicks(object sender, DoWorkEventArgs e)
        {
            while(true)
            {
                Thread.Sleep(TimeSpan.FromMinutes(3));
                lock (locker)
                {
                    if (clicks < 39)
                    {
                        Interlocked.Increment(ref clicks);
                        System.Threading.Monitor.Pulse(locker);

                        if (ClicksChanged != null)
                            ClicksChanged(clicks);
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
            capture.Set();
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

        private void WriteLog(string text)
        {
            string message = String.Format("{0:dd/MM HH:mm:ss} {1}", DateTime.Now, text);
            Debug.WriteLine(message);
            if (Log != null)
                Log(message);
        }
    }
}
