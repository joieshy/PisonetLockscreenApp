using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PisonetLockscreenApp.Services;

// Resolve ambiguities between WinForms and WPF
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using Label = System.Windows.Forms.Label;
using Button = System.Windows.Forms.Button;
using TextBox = System.Windows.Forms.TextBox;
using Control = System.Windows.Forms.Control;

namespace PisonetLockscreenApp.Forms
{
    public class TimerOverlayForm : Form
    {
        private SocketService? _socketService;
        private bool _isVip = false;
        private Label lblTime;
        private Button btnLogout;
        private Label lblUser;
        private Button btnVoucher;
        private Button btnRegister;
        private Button btnMinimize;
        private bool _isMinimized = false;
        private Size _normalSize;
        
        // Modern Web Colors
        private readonly Color bgDark = Color.FromArgb(31, 41, 55); // Gray-800
        private readonly Color borderDark = Color.FromArgb(55, 65, 81); // Gray-700
        private readonly Color textLight = Color.FromArgb(243, 244, 246); // Gray-100
        private readonly Color primaryColor = Color.FromArgb(79, 70, 229); // Indigo-600

        public event EventHandler? LogoutRequested;
        public event EventHandler? RegisterRequested;
        public event Action<string>? VoucherSubmitted;

        public TimerOverlayForm(SocketService? socketService = null)
        {
            _socketService = socketService;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(300, 60);
            this.TopMost = false; // Always bottom (not top most)
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;
            
            lblTime = new Label();
            lblTime.Size = new Size(150,50);
            lblTime.Location = new Point(5, 5);
            lblTime.TextAlign = ContentAlignment.MiddleCenter;
            lblTime.Font = new Font("Consolas", 20, FontStyle.Bold);
            lblTime.ForeColor = textLight;
            lblTime.BackColor = Color.Transparent;
            lblTime.Text = "00:00:00";
            this.Controls.Add(lblTime);

            btnMinimize = new Button();
            btnMinimize.Text = "—";
            btnMinimize.Size = new Size(25, 25);
            btnMinimize.Location = new Point(270, 5);
            btnMinimize.BackColor = Color.FromArgb(55, 65, 81); // Gray-700
            btnMinimize.ForeColor = textLight;
            btnMinimize.FlatStyle = FlatStyle.Flat;
            btnMinimize.FlatAppearance.BorderSize = 1;
            btnMinimize.FlatAppearance.BorderColor = borderDark;
            btnMinimize.Font = new Font("Arial", 10, FontStyle.Bold);
            btnMinimize.Cursor = Cursors.Hand;
            btnMinimize.Click += (s, e) => ToggleMinimize();
            this.Controls.Add(btnMinimize);

            btnLogout = new Button();
            btnLogout.Text = "LOGOUT";
            btnLogout.Size = new Size(80, 40);
            btnLogout.Location = new Point(185, 10);
            btnLogout.BackColor = Color.FromArgb(185, 28, 28); // Red-700
            btnLogout.ForeColor = Color.White;
            btnLogout.FlatStyle = FlatStyle.Flat;
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnLogout.Cursor = Cursors.Hand;
            btnLogout.Click += (s, e) => LogoutRequested?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(btnLogout);

            lblUser = new Label();
            lblUser.Size = new Size(290, 65);
            lblUser.Location = new Point(5, 60);
            lblUser.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            lblUser.ForeColor = Color.FromArgb(209, 213, 219); // Gray-300
            lblUser.BackColor = Color.Transparent;
            lblUser.Text = "User: Guest";
            lblUser.Visible = false;
            this.Controls.Add(lblUser);

            btnVoucher = new Button();
            btnVoucher.Text = "INPUT VOUCHER";
            btnVoucher.Size = new Size(287, 35);
            btnVoucher.Location = new Point(10, 130);
            btnVoucher.BackColor = primaryColor;
            btnVoucher.ForeColor = Color.White;
            btnVoucher.FlatStyle = FlatStyle.Flat;
            btnVoucher.FlatAppearance.BorderSize = 0;
            btnVoucher.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnVoucher.Cursor = Cursors.Hand;
            btnVoucher.Visible = false;
            btnVoucher.Click += (s, e) => ShowVoucherPopup();
            this.Controls.Add(btnVoucher);

            btnRegister = new Button();
            btnRegister.Text = "REGISTER ACCOUNT";
            btnRegister.Size = new Size(290, 35);
            btnRegister.Location = new Point(5, 60);
            btnRegister.BackColor = Color.FromArgb(16, 185, 129); // Emerald-500
            btnRegister.ForeColor = Color.White;
            btnRegister.FlatStyle = FlatStyle.Flat;
            btnRegister.FlatAppearance.BorderSize = 0;
            btnRegister.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnRegister.Cursor = Cursors.Hand;
            btnRegister.Visible = false;
            btnRegister.Click += (s, e) => RegisterRequested?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(btnRegister);

            CenterToScreen();

            // Make label draggable too
            lblTime.MouseDown += (s, e) =>
            {
                if (_isMinimized)
                {
                    ToggleMinimize();
                    return;
                }

                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
        }

        private void ToggleMinimize()
        {
            _isMinimized = !_isMinimized;

            if (_isMinimized)
            {
                _normalSize = this.Size;
                
                // Hide everything except time
                btnLogout.Visible = false;
                lblUser.Visible = false;
                btnVoucher.Visible = false;
                btnRegister.Visible = false;
                btnMinimize.Visible = false;

                // Compact size for time only
                this.Size = new Size(120, 45);
                lblTime.Size = new Size(110, 35);
                lblTime.Location = new Point(5, 5);
                lblTime.Font = new Font("Consolas", 16, FontStyle.Bold);

                // Move to bottom right
                Rectangle r = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens[0].WorkingArea;
                this.Location = new Point(r.Right - this.Width - 10, r.Bottom - this.Height - 10);
            }
            else
            {
                // Restore size and font
                this.Size = _normalSize;
                lblTime.Size = new Size(150, 50);
                lblTime.Location = new Point(5, 5);
                lblTime.Font = new Font("Consolas", 20, FontStyle.Bold);

                // Restore visibility based on user state
                btnMinimize.Visible = true;
                btnLogout.Visible = true;
                SetUser(_currentUsername, _currentPoints, _nextBonus);
                
                // Center or keep current? Let's just keep it where it was before minimize if possible, 
                // but since we moved it to bottom right, maybe centering is safer or just leave it.
                // The user said "balik sa timer form", usually implies original state.
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            // Draw solid background
            using (SolidBrush brush = new SolidBrush(bgDark))
            {
                g.FillRectangle(brush, 0, 0, this.Width, this.Height);
            }
            
            // Draw border that matches form color (slightly darker for subtle contrast)
            using (Pen borderPen = new Pen(borderDark, 1))
            {
                g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        private string _currentUsername = "Guest";
        private int _currentPoints = 0;
        private int _nextBonus = 0;

        public void SetVipStatus(bool isVip)
        {
            _isVip = isVip;
            SetUser(_currentUsername, _currentPoints, _nextBonus);
        }

        private bool IsPromoEnabled()
        {
            if (_socketService == null) return true; // Default to true if no service
            string key = _isVip ? "vip_promo_enabled" : "promo_enabled";
            return _socketService.GetSetting(key, "false") == "true";
        }

        public void SetUser(string username, int points = 0, int nextBonus = 0)
        {
            // Check if this is a transition from Guest to a Member (Login)
            bool isLoggingIn = string.Equals(_currentUsername, "Guest", StringComparison.OrdinalIgnoreCase) && 
                               !string.Equals(username, "Guest", StringComparison.OrdinalIgnoreCase);
            
            _currentUsername = username;
            _currentPoints = points;
            _nextBonus = nextBonus;

            if (_isMinimized) return;

            bool isMember = !string.Equals(username, "Guest", StringComparison.OrdinalIgnoreCase);
            
            if (isMember)
            {
                // Ensure points are displayed clearly
                string bonusTimeStr = "";
                if (_nextBonus > 0)
                {
                    int hours = _nextBonus / 60;
                    int minutes = _nextBonus % 60;
                    bonusTimeStr = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
                }

                if (IsPromoEnabled())
                {
                    string bonusText = _nextBonus > 0 ? $"\nBonus in: {bonusTimeStr}" : "";
                    lblUser.Text = $"User: {username}\nPoints: {points} pts{bonusText}";
                    lblUser.Height = 65;
                }
                else
                {
                    lblUser.Text = $"User: {username}";
                    lblUser.Height = 35;
                }

                lblUser.Visible = true;
                btnVoucher.Visible = true;
                btnVoucher.Location = new Point(5, 130);
                btnRegister.Visible = false;
                this.Size = new Size(300, 175);
            }
            else
            {
                this.Size = new Size(300, 105);
                lblUser.Visible = false;
                btnVoucher.Visible = false;
                btnRegister.Visible = true;
                btnRegister.Location = new Point(5, 65);
            }

            // Only center automatically during login. 
            // This allows the user to move the timer and have it stay there 
            // without it "snapping back" (bubble gum effect) during updates.
            if (isLoggingIn)
            {
                CenterToScreen();
            }
        }

        public new void CenterToScreen()
        {
            Rectangle r = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens[0].WorkingArea;
            this.Location = new Point(r.Left + (r.Width - this.Width) / 2, r.Top + (r.Height - this.Height) / 2);
        }

        private void ShowVoucherPopup()
        {
            using (Form popup = new Form())
            {
                popup.Text = "Input Voucher";
                popup.Size = new Size(320, 200);
                popup.StartPosition = FormStartPosition.CenterScreen;
                popup.FormBorderStyle = FormBorderStyle.None;
                popup.TopMost = true;
                popup.BackColor = bgDark;
                popup.ForeColor = textLight;
                popup.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, popup.Width, popup.Height, 12, 12));

                Label lbl = new Label { 
                    Text = "Enter Voucher Code:", 
                    Location = new Point(25, 30), 
                    AutoSize = true, 
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = textLight
                };
                
                TextBox txt = new TextBox { 
                    Location = new Point(25, 60), 
                    Width = 270, 
                    Font = new Font("Segoe UI", 12),
                    BackColor = Color.FromArgb(55, 65, 81), // Gray-700
                    ForeColor = textLight,
                    BorderStyle = BorderStyle.FixedSingle
                };
                
                Button btn = new Button 
                { 
                    Text = "SUBMIT", 
                    Location = new Point(25, 110), 
                    Size = new Size(270, 45), 
                    BackColor = primaryColor, 
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;

                btn.MouseEnter += (s, e) => { btn.BackColor = Color.FromArgb(67, 56, 202); };
                btn.MouseLeave += (s, e) => { btn.BackColor = primaryColor; };

                Button btnClose = new Button
                {
                    Text = "×",
                    Font = new Font("Arial", 14, FontStyle.Bold),
                    Size = new Size(25, 25),
                    Location = new Point(290, 5),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.Gray,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Click += (s, e) => popup.Close();

                popup.Paint += (s, e) => {
                    Graphics g = e.Graphics;
                    Pen p = new Pen(borderDark, 1);
                    g.DrawRectangle(p, 1, 1, popup.Width - 3, popup.Height - 3);
                };

                btn.Click += (s, e) => {
                    if (!string.IsNullOrWhiteSpace(txt.Text))
                    {
                        VoucherSubmitted?.Invoke(txt.Text.Trim());
                        popup.Close();
                    }
                };

                popup.Controls.AddRange(new Control[] { lbl, txt, btn, btnClose });
                popup.AcceptButton = btn;
                popup.ShowDialog();
            }
        }

        public void UpdateTime(int seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            lblTime.Text = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
            
            if (seconds <= 60)
            {
                lblTime.ForeColor = Color.FromArgb(239, 68, 68); // Red-500
                if (seconds % 2 == 0) lblTime.ForeColor = Color.White; // Blinking effect
            }
            else
            {
                lblTime.ForeColor = textLight;
            }

            this.Invalidate(); // Redraw for any animations or state changes

            // Keep at bottom of Z-order
            NativeMethods.SetWindowPos(this.Handle, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0, 
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        public void UpdatePoints(int points, int nextBonus = 0)
        {
            _currentPoints = points;
            _nextBonus = nextBonus;
            if (!string.Equals(_currentUsername, "Guest", StringComparison.OrdinalIgnoreCase))
            {
                if (IsPromoEnabled())
                {
                    string bonusTimeStr = "";
                    if (_nextBonus > 0)
                    {
                        int hours = _nextBonus / 60;
                        int minutes = _nextBonus % 60;
                        bonusTimeStr = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
                    }
                    string bonusText = _nextBonus > 0 ? $"\nBonus in: {bonusTimeStr}" : "";
                    lblUser.Text = $"User: {_currentUsername}\nPoints: {_currentPoints} pts{bonusText}";
                    lblUser.Height = 65;
                }
                else
                {
                    lblUser.Text = $"User: {_currentUsername}";
                    lblUser.Height = 35;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Prevent user from closing the timer (e.g. Alt+F4)
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            if (_isMinimized)
            {
                ToggleMinimize();
                return;
            }
            if (e.Button == MouseButtons.Left) { NativeMethods.ReleaseCapture(); NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0); }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_MOVING && Marshal.PtrToStructure<NativeMethods.RECT>(m.LParam) is NativeMethods.RECT rc)
            {
                Rectangle workingArea = Screen.FromHandle(this.Handle).WorkingArea;

                int width = rc.Right - rc.Left;
                int height = rc.Bottom - rc.Top;

                bool adjusted = false;

                if (rc.Left < workingArea.Left)
                {
                    rc.Left = workingArea.Left;
                    rc.Right = rc.Left + width;
                    adjusted = true;
                }
                if (rc.Top < workingArea.Top)
                {
                    rc.Top = workingArea.Top;
                    rc.Bottom = rc.Top + height;
                    adjusted = true;
                }
                if (rc.Right > workingArea.Right)
                {
                    rc.Right = workingArea.Right;
                    rc.Left = rc.Right - width;
                    adjusted = true;
                }
                if (rc.Bottom > workingArea.Bottom)
                {
                    rc.Bottom = workingArea.Bottom;
                    rc.Top = rc.Bottom - height;
                    adjusted = true;
                }

                if (adjusted)
                {
                    Marshal.StructureToPtr(rc, m.LParam, false);
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            IntPtr handle = NativeMethods.CreateRoundRectRgn(0, 0, this.Width, this.Height, 12, 12);
            this.Region = Region.FromHrgn(handle);
            NativeMethods.DeleteObject(handle);
        }
    }
}
