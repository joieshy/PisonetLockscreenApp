using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PisonetLockscreenApp.Services;

namespace PisonetLockscreenApp.Forms
{
    public class RobotMessageBox : Form
    {
        private Label lblMessage;
        private Button btnOk;
        private Button? btnCancel;
        private Panel pnlButtons;
        private Panel pnlHeader;
        private Label lblTitle;

        // Modern Web-like Dark Theme Colors (Tailwind-inspired)
        private readonly Color modalBackground = Color.FromArgb(31, 41, 55); // Gray-800
        private readonly Color headerBackground = Color.FromArgb(17, 24, 39); // Gray-900
        private readonly Color primaryButtonColor = Color.FromArgb(79, 70, 229); // Indigo-600
        private readonly Color primaryButtonHover = Color.FromArgb(67, 56, 202); // Indigo-700
        private readonly Color cancelButtonColor = Color.FromArgb(107, 114, 128); // Gray-500
        private readonly Color cancelButtonHover = Color.FromArgb(75, 85, 99); // Gray-600
        private readonly Color textColor = Color.FromArgb(243, 244, 246); // Gray-100
        private readonly Color borderColor = Color.FromArgb(55, 65, 81); // Gray-700

        public RobotMessageBox(string message, string title = "SYSTEM MESSAGE", bool showCancel = false)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = modalBackground;
            this.ForeColor = textColor;
            this.Size = new Size(450, 240);
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.Padding = new Padding(1); // For border

            // Apply rounded corners to form
            this.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, this.Width, this.Height, 12, 12));

            // Header Panel
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = headerBackground,
                Padding = new Padding(20, 0, 20, 0)
            };
            this.Controls.Add(pnlHeader);

            lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = textColor,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlHeader.Controls.Add(lblTitle);

            // Main message panel
            Panel pnlMessage = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = modalBackground,
                Padding = new Padding(25, 20, 25, 10)
            };
            this.Controls.Add(pnlMessage);

            lblMessage = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(209, 213, 219), // Gray-300
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                AutoSize = false
            };
            pnlMessage.Controls.Add(lblMessage);

            // Button panel
            pnlButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 65,
                BackColor = modalBackground,
                Padding = new Padding(0, 0, 20, 0)
            };
            this.Controls.Add(pnlButtons);

            // Calculate button positions based on whether cancel button is shown
            int btnWidth = 100;
            int btnHeight = 38;
            int spacing = 12;

            if (showCancel)
            {
                btnCancel = CreateWebButton("Cancel", cancelButtonColor, cancelButtonHover);
                btnCancel.Size = new Size(btnWidth, btnHeight);
                btnCancel.Location = new Point(pnlButtons.Width - (btnWidth * 2) - spacing - 20, 13);
                btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
                pnlButtons.Controls.Add(btnCancel);

                btnOk = CreateWebButton("Confirm", primaryButtonColor, primaryButtonHover);
                btnOk.Size = new Size(btnWidth, btnHeight);
                btnOk.Location = new Point(pnlButtons.Width - btnWidth - 20, 13);
                btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
                pnlButtons.Controls.Add(btnOk);
            }
            else
            {
                btnOk = CreateWebButton("OK", primaryButtonColor, primaryButtonHover);
                btnOk.Size = new Size(btnWidth, btnHeight);
                btnOk.Location = new Point(pnlButtons.Width - btnWidth - 20, 13);
                btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
                pnlButtons.Controls.Add(btnOk);
            }

            // Draw border
            this.Paint += (s, e) =>
            {
                using (Pen p = new Pen(borderColor, 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
                }
            };

            // Draw separator line under header
            pnlHeader.Paint += (s, e) =>
            {
                using (Pen p = new Pen(borderColor, 1))
                {
                    e.Graphics.DrawLine(p, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                }
            };
        }

        private Button CreateWebButton(string text, Color bg, Color hover)
        {
            Button btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = bg,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize = 0;
            
            // Rounded corners for button (CSS border-radius: 4px)
            btn.Paint += (s, e) =>
            {
                Rectangle r = new Rectangle(0, 0, btn.Width, btn.Height);
                using (GraphicsPath path = new GraphicsPath())
                {
                    int radius = 4;
                    path.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
                    path.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
                    path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                    path.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                    path.CloseFigure();
                    btn.Region = new Region(path);
                }
            };

            btn.MouseEnter += (s, e) => btn.BackColor = hover;
            btn.MouseLeave += (s, e) => btn.BackColor = bg;

            return btn;
        }

        public static DialogResult Show(string message, string title = "SYSTEM MESSAGE", bool showCancel = false)
        {
            using (var msgBox = new RobotMessageBox(message, title, showCancel))
            {
                return msgBox.ShowDialog();
            }
        }
    }
}