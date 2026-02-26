using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    public class MemberLoginForm : Form
    {
        public event Action<string, string, string>? LoginRequested;
        private TextBox txtUser;
        private TextBox txtPass;
        private TextBox txtVoucher;
        private Button btnLogin;
        
        // Modern Web Colors matching TimerOverlayForm
        private readonly Color bgDark = Color.FromArgb(31, 41, 55); // Gray-800
        private readonly Color borderDark = Color.FromArgb(55, 65, 81); // Gray-700
        private readonly Color textLight = Color.FromArgb(243, 244, 246); // Gray-100
        private readonly Color primaryColor = Color.FromArgb(79, 70, 229); // Indigo-600

        public MemberLoginForm()
        {
            Color inputBack = Color.FromArgb(55, 65, 81); // Gray-700
            
            this.Text = "SYSTEM: MEMBER ACCESS";
            this.Size = new Size(450, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None; // Custom border
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false; // Hide title bar buttons like TimerOverlayForm
            this.KeyPreview = true;
            this.TopMost = true;
            this.BackColor = bgDark;
            this.ForeColor = textLight;
            this.DoubleBuffered = true;

            // Create rounded corners for the form
            this.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, this.Width, this.Height, 15, 15));

            int padding = 45;
            int contentWidth = 345;

            Label lblTitle = new Label { 
                Text = "Member Login", 
                Font = new Font("Consolas", 18, FontStyle.Bold), 
                AutoSize = true, 
                Location = new Point(padding, 25), 
                ForeColor = primaryColor 
            };

            // --- Login Section ---
            Label lblUser = new Label { Text = "Username", Left = padding, Top = 85, AutoSize = true, Font = new Font("Consolas", 10) };
            txtUser = new TextBox { 
                Left = padding, 
                Top = 110, 
                Width = contentWidth, 
                Font = new Font("Consolas", 12), 
                BackColor = inputBack, 
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Enter your username"
            };
            txtUser.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, txtUser.Width, txtUser.Height, 8, 8));

            Label lblPass = new Label { Text = "Password", Left = padding, Top = 160, AutoSize = true, Font = new Font("Consolas", 10) };
            txtPass = new TextBox { 
                Left = padding, 
                Top = 185, 
                Width = contentWidth, 
                Font = new Font("Consolas", 12), 
                BackColor = inputBack, 
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, 
                PasswordChar = '•',
                PlaceholderText = "Enter your password"
            };
            txtPass.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, txtPass.Width, txtPass.Height, 8, 8));

            // --- Voucher Section ---
            Label lblVoucher = new Label { 
                Text = "Voucher Code (Optional)", 
                Left = padding, 
                Top = 235, 
                AutoSize = true, 
                Font = new Font("Consolas", 10) 
            };
            txtVoucher = new TextBox { 
                Left = padding, 
                Top = 260, 
                Width = contentWidth, 
                Font = new Font("Consolas", 12), 
                BackColor = inputBack, 
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Enter voucher code"
            };
            txtVoucher.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, txtVoucher.Width, txtVoucher.Height, 8, 8));

            btnLogin = new Button { 
                Text = "LOGIN TO ACCOUNT", 
                Left = padding, 
                Top = 310, 
                Width = contentWidth, 
                Height = 50, 
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 11, FontStyle.Bold),
                Cursor = Cursors.Hand,
                ForeColor = Color.White
            };
            
            // Apply solid background to button like TimerOverlayForm
            btnLogin.Paint += (sender, e) =>
            {
                Rectangle rect = new Rectangle(0, 0, btnLogin.Width, btnLogin.Height);
                
                // Solid background like TimerOverlayForm buttons
                using (SolidBrush brush = new SolidBrush(primaryColor))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
                
                // Draw rounded corners using Region
                btnLogin.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, btnLogin.Width, btnLogin.Height, 12, 12));
                
                // Draw text
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    e.Graphics.DrawString(btnLogin.Text, btnLogin.Font, Brushes.White, rect, sf);
                }
                
                // Simple border like TimerOverlayForm
                using (Pen borderPen = new Pen(borderDark, 1))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, btnLogin.Width - 1, btnLogin.Height - 1);
                }
            };
            
            btnLogin.FlatAppearance.BorderSize = 0;

            // JS-like effects for textboxes
            Action<TextBox> addFocusEffect = (tb) => {
                tb.Enter += (s, e) => { 
                    tb.BackColor = Color.FromArgb(75, 85, 99); // Gray-600
                    tb.ForeColor = Color.White; // Keep font white when focused
                    tb.Invalidate();
                };
                tb.Leave += (s, e) => { 
                    tb.BackColor = inputBack; 
                    tb.ForeColor = Color.White; 
                    tb.Invalidate();
                };
            };
            addFocusEffect(txtUser);
            addFocusEffect(txtPass);
            addFocusEffect(txtVoucher);
            
            btnLogin.MouseEnter += (s, e) => {
                btnLogin.Invalidate(); // Redraw for hover effect
            };
            btnLogin.MouseLeave += (s, e) => {
                btnLogin.Invalidate(); // Redraw for normal state
            };
            
            btnLogin.Click += (s, e) => { 
                string user = txtUser.Text.Trim();
                string pass = txtPass.Text.Trim();
                string voucher = txtVoucher.Text.Trim();

                if ((!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass)) || !string.IsNullOrWhiteSpace(voucher))
                {
                    LoginRequested?.Invoke(user, pass, voucher); 
                }
            };
            
            txtVoucher.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter)
                {
                    btnLogin.PerformClick();
                    e.SuppressKeyPress = true;
                }
            };

            this.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };

            // Add custom close button for borderless form (top-right corner)
            Button btnClose = new Button
            {
                Text = "×",
                Font = new Font("Arial", 18, FontStyle.Bold),
                Size = new Size(35, 35),
                Location = new Point(this.Width - 45, 10),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Gray,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            
            // Hover effects for close button
            btnClose.MouseEnter += (s, e) => {
                btnClose.ForeColor = Color.White;
                btnClose.BackColor = Color.FromArgb(239, 68, 68); // Red-500
            };
            btnClose.MouseLeave += (s, e) => {
                btnClose.ForeColor = Color.Gray;
                btnClose.BackColor = Color.Transparent;
            };
            
            btnClose.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { 
                lblTitle, lblUser, txtUser, lblPass, txtPass, 
                lblVoucher, txtVoucher, btnLogin, btnClose 
            });
            this.AcceptButton = btnLogin;
            
            // Ensure close button is on top
            btnClose.BringToFront();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            // Draw solid background like TimerOverlayForm
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

        public void SetLoading(bool loading)
        {
            this.Enabled = !loading;
            btnLogin.Text = loading ? "Wait..." : "LOGIN TO ACCOUNT";
            btnLogin.Invalidate();
        }
    }
}
