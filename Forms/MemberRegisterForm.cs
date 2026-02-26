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
    public class MemberRegisterForm : Form
    {
        public event Action<string, string>? RegisterRequested;
        private TextBox txtUser;
        private TextBox txtPass;
        private TextBox txtConfirm;
        private Button btnRegister;
        private Label lblFee;
        private Panel pnlHeader;
        private Panel pnlFormContainer;

        // Modern Web Colors
        private readonly Color bgDark = Color.FromArgb(31, 41, 55); // Gray-800
        private readonly Color headerDark = Color.FromArgb(17, 24, 39); // Gray-900
        private readonly Color inputBg = Color.FromArgb(55, 65, 81); // Gray-700
        private readonly Color primaryColor = Color.FromArgb(79, 70, 229); // Indigo-600
        private readonly Color textLight = Color.FromArgb(243, 244, 246); // Gray-100
        private readonly Color textMuted = Color.FromArgb(156, 163, 175); // Gray-400

        public MemberRegisterForm()
        {
            this.Text = "SYSTEM: NEW UNIT REGISTRATION";
            this.Size = new Size(500, 550); // Slightly larger for better spacing
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = true;
            this.KeyPreview = true;
            this.TopMost = true;
            this.BackColor = bgDark;
            this.ForeColor = textLight;
            this.DoubleBuffered = true;
            this.Padding = new Padding(1);

            // Apply rounded corners to form
            this.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, this.Width, this.Height, 12, 12));

            // Header Panel
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = headerDark
            };
            this.Controls.Add(pnlHeader);

            Label lblTitle = new Label
            {
                Text = "CREATE ACCOUNT",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = textLight,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0)
            };
            pnlHeader.Controls.Add(lblTitle);

            // Main form container
            pnlFormContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(40, 30, 40, 20)
            };
            this.Controls.Add(pnlFormContainer);

            int contentWidth = pnlFormContainer.Width - 80; // Account for padding
            int startX = 40;
            int currentY = 20;

            lblFee = new Label
            {
                Text = "Registration Fee: ₱0",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(239, 68, 68), // Red-500
                AutoSize = true,
                Location = new Point(startX, currentY),
                TextAlign = ContentAlignment.MiddleLeft,
                Width = contentWidth
            };
            currentY += 40;

            // --- Fields ---
            Label lblUser = new Label
            {
                Text = "Username",
                Location = new Point(startX, currentY),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = textMuted
            };
            currentY += 25;

            txtUser = new TextBox
            {
                Location = new Point(startX, currentY),
                Width = contentWidth,
                Height = 40,
                Font = new Font("Segoe UI", 12),
                BackColor = inputBg,
                ForeColor = textLight,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Choose a username"
            };
            currentY += 55;

            Label lblPass = new Label
            {
                Text = "Password",
                Location = new Point(startX, currentY),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = textMuted
            };
            currentY += 25;

            txtPass = new TextBox
            {
                Location = new Point(startX, currentY),
                Width = contentWidth,
                Height = 40,
                Font = new Font("Segoe UI", 12),
                BackColor = inputBg,
                ForeColor = textLight,
                BorderStyle = BorderStyle.FixedSingle,
                PasswordChar = '•',
                PlaceholderText = "Enter password"
            };
            currentY += 55;

            Label lblConfirm = new Label
            {
                Text = "Confirm Password",
                Location = new Point(startX, currentY),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = textMuted
            };
            currentY += 25;

            txtConfirm = new TextBox
            {
                Location = new Point(startX, currentY),
                Width = contentWidth,
                Height = 40,
                Font = new Font("Segoe UI", 12),
                BackColor = inputBg,
                ForeColor = textLight,
                BorderStyle = BorderStyle.FixedSingle,
                PasswordChar = '•',
                PlaceholderText = "Re-type password"
            };
            currentY += 70;

            btnRegister = new Button
            {
                Text = "REGISTER & PAY FEE",
                Location = new Point(startX, currentY),
                Width = contentWidth,
                Height = 55,
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRegister.FlatAppearance.BorderSize = 0;

            // Focus effects for textboxes
            Action<TextBox> addFocusEffect = (tb) =>
            {
                tb.Enter += (s, e) =>
                {
                    tb.BackColor = Color.FromArgb(75, 85, 99); // Gray-600
                };
                tb.Leave += (s, e) =>
                {
                    tb.BackColor = inputBg;
                };
            };
            addFocusEffect(txtUser);
            addFocusEffect(txtPass);
            addFocusEffect(txtConfirm);

            // Button hover effects
            btnRegister.MouseEnter += (s, e) =>
            {
                btnRegister.BackColor = Color.FromArgb(67, 56, 202); // Indigo-700
            };
            btnRegister.MouseLeave += (s, e) =>
            {
                btnRegister.BackColor = primaryColor;
            };

            // Rounded corners for button
            btnRegister.Paint += (sender, e) =>
            {
                Rectangle rect = new Rectangle(0, 0, btnRegister.Width, btnRegister.Height);
                using (GraphicsPath path = new GraphicsPath())
                {
                    int radius = 6;
                    path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
                    path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
                    path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
                    path.CloseFigure();
                    btnRegister.Region = new Region(path);
                }
            };

            btnRegister.Click += (s, e) =>
            {
                string user = txtUser.Text.Trim();
                string pass = txtPass.Text.Trim();
                string confirm = txtConfirm.Text.Trim();

                if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                {
                    MessageBox.Show("Please fill in all fields.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (pass != confirm)
                {
                    MessageBox.Show("Passwords do not match.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                RegisterRequested?.Invoke(user, pass);
            };

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };

            pnlFormContainer.Controls.AddRange(new Control[]
            {
                lblFee, lblUser, txtUser, lblPass, txtPass,
                lblConfirm, txtConfirm, btnRegister
            });
            this.AcceptButton = btnRegister;

            // Add close button for borderless form
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
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
            btnClose.BringToFront();

            // Paint events for custom styling
            this.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(75, 85, 99), 1))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };

            // Header separator
            pnlHeader.Paint += (s, e) =>
            {
                using (Pen borderPen = new Pen(Color.FromArgb(75, 85, 99), 1))
                {
                    e.Graphics.DrawLine(borderPen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                }
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Additional painting if needed
        }

        public void SetFee(int fee)
        {
            lblFee.Text = $"Registration Fee: ₱{fee}";
        }

        public void SetLoading(bool loading)
        {
            this.Enabled = !loading;
            btnRegister.Text = loading ? "PROCESSING..." : "REGISTER & PAY FEE";
            btnRegister.Invalidate();
        }
    }
}