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
            if (game != null)
                game.Refresh();
        }
    }
}
