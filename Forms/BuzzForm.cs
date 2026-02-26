using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PisonetLockscreenApp.Services;

namespace PisonetLockscreenApp.Forms
{
    public class BuzzForm : Form
    {
        private Label lblTitle;
        private Label lblMessage;
        private System.Windows.Forms.Timer flashTimer;
        private System.Windows.Forms.Timer closeTimer;
        private System.Windows.Forms.Timer progressTimer;
        private Panel pnlHeader;
        private int flashCount = 0;
        private int _duration;
        private int _elapsed = 0;

        // Modern Alert Colors
        private readonly Color bgDark = Color.FromArgb(31, 41, 55); // Gray-800
        private readonly Color alertRed = Color.FromArgb(220, 38, 38); // Red-600
        private readonly Color alertDarkRed = Color.FromArgb(153, 27, 27); // Red-800

        public BuzzForm(string message, int durationSeconds = 5)
        {
            _duration = durationSeconds * 1000;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgDark;
            this.ForeColor = Color.White;
            this.Size = new Size(Screen.PrimaryScreen!.Bounds.Width, 400);
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;

            // Apply rounded corners to form
            this.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, this.Width, this.Height, 12, 12));

            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = alertRed
            };
            this.Controls.Add(pnlHeader);

            lblTitle = new Label
            {
                Text = "⚠ SYSTEM ALERT: ADMIN BUZZ ⚠",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlHeader.Controls.Add(lblTitle);

            lblMessage = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 36, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(20)
            };
            this.Controls.Add(lblMessage);
            lblMessage.BringToFront();

            flashTimer = new System.Windows.Forms.Timer { Interval = 300 };
            flashTimer.Tick += (s, e) =>
            {
                flashCount++;
                if (flashCount % 2 == 0)
                {
                    pnlHeader.BackColor = alertRed;
                    this.BackColor = bgDark;
                }
                else
                {
                    pnlHeader.BackColor = alertDarkRed;
                    this.BackColor = Color.FromArgb(60, 10, 10); // Very dark red bg
                }
                this.Invalidate();
            };
            flashTimer.Start();

            closeTimer = new System.Windows.Forms.Timer { Interval = durationSeconds * 1000 };
            closeTimer.Tick += (s, e) =>
            {
                flashTimer.Stop();
                closeTimer.Stop();
                this.Close();
            };
            closeTimer.Start();

            progressTimer = new System.Windows.Forms.Timer { Interval = 50 };
            progressTimer.Tick += (s, e) =>
            {
                _elapsed += 50;
                if (_elapsed >= _duration) progressTimer.Stop();
                this.Invalidate();
            };
            progressTimer.Start();

            // Paint event for gradient background and modern styling
            this.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                
                // Draw border
                using (Pen borderPen = new Pen(alertRed, 4))
                {
                    g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
                }

                // Progress bar at bottom with gradient
                float percent = (float)_elapsed / _duration;
                int barWidth = (int)(this.Width * (1.0f - percent));
                
                using (SolidBrush progressBrush = new SolidBrush(alertRed))
                {
                    g.FillRectangle(progressBrush, barWidth, this.Height - 10, this.Width - barWidth, 10);
                }
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            flashTimer?.Stop();
            closeTimer?.Stop();
            progressTimer?.Stop();
            base.OnFormClosing(e);
        }
    }
}