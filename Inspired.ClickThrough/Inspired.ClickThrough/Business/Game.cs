using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private readonly BackgroundWorker detectWorker = new BackgroundWorker();
        private readonly BackgroundWorker executeWorker = new BackgroundWorker();
        private readonly BackgroundWorker addClicksWorker = new BackgroundWorker();

        private int clicks;
        private Bitmap current;
        private Rectangle bounds;
        private readonly List<Task> tasks = new List<Task>();
        private readonly ExhaustiveTemplateMatching exhaustive = new ExhaustiveTemplateMatching(0.8f);
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
            Template.Create("Ok"       , 0, Color.Green  , Priority.High   , false),
            Template.Create("BioHazard", 1, Color.Red    , Priority.High   , true ),
            Template.Create("Coin"     , 1, Color.Yellow , Priority.Low    , true ),
            Template.Create("Material" , 1, Color.Brown  , Priority.Low    , true )
        };

        public delegate void LogHandler(string message);
        public event LogHandler Log;
        
        public void Start()
        {
            clicks = 0;
            bounds = Screen.AllScreens[this.Monitor].Bounds;
            current = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            //current = new Bitmap(@"C:\Users\Rubens\Desktop\levelUp.png");

            detectWorker.DoWork += Detect;
            executeWorker.DoWork += Execute;
            addClicksWorker.DoWork += AddClicks;

            detectWorker.RunWorkerAsync();
            executeWorker.RunWorkerAsync();
            addClicksWorker.RunWorkerAsync();
        }

        private void Detect(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                using (Graphics g = Graphics.FromImage(current))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                    lock (locker)
                    {
                        tasks.Clear();
                    }

                    BlobCounter actions = new BlobCounter { FilterBlobs = true, MinWidth =  31, MinHeight = 31, MaxWidth =  42, MaxHeight = 42 };
                    BlobCounter buttons = new BlobCounter { FilterBlobs = true, MinWidth = 140, MinHeight = 40, MaxWidth = 160, MaxHeight = 50 };

                    //int i = 0;
                    ////keepYellowOnly.ApplyInPlace(current);
                    //buttons.ProcessImage(keepYellowOnly.Apply(current));
                    //foreach (Blob blob in buttons.GetObjectsInformation())
                    //{
                    //    using (Bitmap icon = new Bitmap(blob.Rectangle.Width, blob.Rectangle.Height))
                    //    using (Graphics g1 = Graphics.FromImage(icon))
                    //    {
                    //        icon.SetResolution(current.HorizontalResolution, current.VerticalResolution);
                    //        g1.DrawImage(current, 0, 0, blob.Rectangle, GraphicsUnit.Pixel);
                    //        icon.Save(String.Format(@"C:\Users\Rubens\Desktop\{0}.jpg", ++i), ImageFormat.Jpeg);
                    //    }
                    //    g.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.Yellow)), blob.Rectangle);
                    //}
                    //current.Save(@"C:\Users\Rubens\Desktop\levelUpXXX.png");

                    Bitmap yellowOnly = keepYellowOnly.Apply(current);
                    actions.ProcessImage(yellowOnly);
                    buttons.ProcessImage(yellowOnly);

                    IEnumerable<Blob> blobs = actions.GetObjectsInformation().Concat(
                                              buttons.GetObjectsInformation());
                    foreach (Blob blob in blobs)
                    {
                        foreach (var template in templates.Where(t => t.Icon.Height <= blob.Rectangle.Height && t.Icon.Width <= blob.Rectangle.Width))
                        foreach (var match in exhaustive.ProcessImage(current, template.Icon, blob.Rectangle))
                        {
                            Rectangle target = CalculateOffset(match.Rectangle, template.Offset);
                            g.FillRectangle(new SolidBrush(Color.FromArgb(128, template.Color)), match.Rectangle);
                            g.FillRectangle(Brushes.HotPink, target);   // click point

                            if (!template.AutoClick)
                                continue;   // Auto level up?

                            Task task = new Task
                            {
                                Priority    = template.Priority,
                                Location    = TranslateScreenCoordinates(target),
                                MouseEvents = new[] { Mouse.MouseEvent.LeftDown, Mouse.MouseEvent.LeftUp },
                                RewardType  = (RewardType)Enum.Parse(typeof(RewardType), template.Name),
                                Cost        = template.Cost
                            };

                            lock (locker)
                            {
                                if (!tasks.Any(t => t.Location == task.Location))
                                {
                                    tasks.Add(task);
                                    System.Threading.Monitor.Pulse(locker);
                                }
                            }
                        }
                    }
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
                    // while no new tasks, or not enough clicks
                    while(tasks.Count == 0 || tasks.OrderByDescending(t => (int)t.Priority).First().Cost > clicks)
                        System.Threading.Monitor.Wait(locker);

                    tasks.Remove(task = tasks.OrderByDescending(t => (int)t.Priority).First());
                }

                if (Log != null)
                    Log(String.Format("{0:MMM dd, HH:mm:ss} {1} {2}", DateTime.Now, task.Location, task.RewardType));

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
            return new Point(Screen.AllScreens[this.Monitor].Bounds.X + rectangle.X, rectangle.Y - 85);
        }

        private void Refresh(Image image, PictureBox pictureBox)
        {
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

        public void Refresh()
        {
            this.Refresh(current.Clone() as Bitmap, this.Preview);
        }
    }
}
