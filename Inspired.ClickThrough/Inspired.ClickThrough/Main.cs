using System;
using System.Windows.Forms;
using Inspired.ClickThrough.Business;

namespace Inspired.ClickThrough
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
        }

        Game game = null;

        private void Form1_Load(object sender, EventArgs e)
        {
            game = new Game
            {
                Monitor = 1,
                Preview = this.pictureBox,
                Spawn = TimeSpan.FromMinutes(3),
                Interval = TimeSpan.FromSeconds(5),
            };
            game.Log += message => {
                if (this.log.InvokeRequired)
                    this.Invoke(new MethodInvoker(delegate { this.log.Text = message + Environment.NewLine + this.log.Text; }));
            };
            game.Start();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                notifyIcon.Icon = this.Icon;
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(500);
                this.Hide(); 
            }
            else
            {
                this.Show();
                if (game != null)
                    game.Refresh();
            }
        }

        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void control_Click(object sender, EventArgs e)
        {
            if(this.control.Text == "Pause")
            {
                game.Pause();
                this.control.Text = "Play";
            }
            else
            {
                game.Play();
                this.control.Text = "Pause";
            }
        }
    }
}
