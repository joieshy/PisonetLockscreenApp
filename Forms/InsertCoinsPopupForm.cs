using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PisonetLockscreenApp.Services;

namespace PisonetLockscreenApp.Forms
{
    public class InsertCoinsPopupForm : Form
    {
        private int _secondsRemaining = 30;
        private int _totalSeconds;
        private System.Windows.Forms.Timer _timer;
        private System.Media.SoundPlayer? _tingPlayer;
        private Label _lblCountdown;
        private Label _lblInstruction;
        private Panel _mainPanel;
        
        private readonly Color bgDark = Color.FromArgb(31, 41, 55); // Gray-800
        private readonly Color primaryColor = Color.FromArgb(79, 70, 229); // Indigo-600

        public InsertCoinsPopupForm(int durationSeconds = 30)
        {
            _secondsRemaining = durationSeconds;
            _totalSeconds = durationSeconds;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(450, 300);
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            
            // Apply rounded corners to the form
            this.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, this.Width, this.Height, 12, 12));

            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(30),
                BackColor = bgDark
            };
            this.Controls.Add(_mainPanel);

            // Draw Circular Progress Bar on the panel
            _mainPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                if (_lblCountdown != null)
                {
                    // Center the circle around the countdown label
                    Rectangle r = _lblCountdown.Bounds;
                    int diameter = Math.Min(r.Width, r.Height) - 20;
                    if (diameter <= 0) return;

                    int cx = r.Left + r.Width / 2;
                    int cy = r.Top + r.Height / 2;
                    Rectangle arcRect = new Rectangle(cx - diameter / 2, cy - diameter / 2, diameter, diameter);

                    // Draw Track
                    using (Pen trackPen = new Pen(Color.FromArgb(55, 65, 81), 8)) // Gray-700
                    {
                        e.Graphics.DrawEllipse(trackPen, arcRect);
                    }

                    // Draw Progress
                    if (_totalSeconds > 0)
                    {
                        float sweepAngle = 360f * ((float)_secondsRemaining / _totalSeconds);
                        using (Pen progressPen = new Pen(primaryColor, 8))
                        {
                            progressPen.StartCap = LineCap.Round;
                            progressPen.EndCap = LineCap.Round;
                            // Start from top (-90 degrees)
                            e.Graphics.DrawArc(progressPen, arcRect, -90, sweepAngle);
                        }
                    }
                }
            };

            _lblCountdown = new Label
            {
                Text = _secondsRemaining.ToString(),
                Font = new Font("Segoe UI", 72, FontStyle.Bold),
                ForeColor = primaryColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            _mainPanel.Controls.Add(_lblCountdown);

            _lblInstruction = new Label
            {
                Text = "Please drop your coins now...",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(209, 213, 219), // Gray-300
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.Transparent
            };
            _mainPanel.Controls.Add(_lblInstruction);

            try
            {
                string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "media", "TING.wav");
                if (!System.IO.File.Exists(soundPath))
                {
                    // Fallback to relative path if base directory doesn't work as expected in dev
                    soundPath = "media/TING.wav";
                }
                _tingPlayer = new System.Media.SoundPlayer(soundPath);
                _tingPlayer.Load();
            }
            catch { }

            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += (s, e) => {
                // Play TING sound every second
                try { _tingPlayer?.Play(); } catch { }

                _secondsRemaining--;
                _lblCountdown.Text = _secondsRemaining.ToString();
                _mainPanel.Invalidate(); // Redraw progress bar

                if (_secondsRemaining <= 5)
                {
                    _lblCountdown.ForeColor = Color.FromArgb(255, 100, 100);
                }

                if (_secondsRemaining <= 0)
                {
                    _timer.Stop();
                    this.Close();
                }
            };
            _timer.Start();

            // Add a close button (optional, but good for UX)
            Button btnClose = new Button
            {
                Text = "×",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Size = new Size(30, 30),
                Location = new Point(this.Width - 40, 10),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Gray,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
            btnClose.BringToFront();

            this.FormClosing += (s, e) => {
                _timer?.Stop();
                try { _tingPlayer?.Stop(); } catch { }
                _tingPlayer?.Dispose();
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            // Draw border
            using (Pen borderPen = new Pen(primaryColor, 2))
            {
                g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }
    }
}
