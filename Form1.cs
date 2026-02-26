﻿using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;
using System.IO;
using System.Drawing.Imaging;
using SocketIOClient;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Windows.Forms.Integration;
using System.Windows.Media.Imaging;
using PisonetLockscreenApp.Services;
using PisonetLockscreenApp.Forms;

namespace PisonetLockscreenApp
{
    // Resolve ambiguities between WinForms and WPF
    using Color = System.Drawing.Color;
    using Font = System.Drawing.Font;
    using Image = System.Drawing.Image;
    using Label = System.Windows.Forms.Label;
    using Button = System.Windows.Forms.Button;
    using TextBox = System.Windows.Forms.TextBox;
    using Control = System.Windows.Forms.Control;
    using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

    public class Form1 : Form
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static Form1? _instance;
        private static NativeMethods.LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static bool isLocked = true;
        private static bool isAdminPromptOpen = false;
        private static bool isMemberLoginOpen = false;
        private static bool isMemberRegisterOpen = false;
        private static bool isInsertCoinsOpen = false;
        private static bool _isEscDown = false;

        private int remainingSeconds = 0;
        private Dictionary<string, int> _userSuccessCounts = new Dictionary<string, int>();
        private Dictionary<string, DateTime> _userLockouts = new Dictionary<string, DateTime>();
        private int _idleSecondsRemaining = -1;
        private int currentDesign = 0;
        private Color _shopNameColor = Color.Yellow;
        private readonly string _colorConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shopcolor.txt");
        private SerialPort? _serialPort;
        
        // Services
        private static ConfigManager _config = null!;
        private readonly HardwareMonitor _hardware;
        private readonly AudioService _audio;
        private SocketService _socketService;

        // WPF Wallpaper Controls
        private ElementHost _wpfHost = null!;
        private System.Windows.Controls.MediaElement _videoPlayer = null!;
        private System.Windows.Controls.Image _imageBackground = null!;

        // initialize reference types to suppress definite-assignment / nullable warnings
        private System.Windows.Forms.Timer animationTimer = null!;
        private System.Windows.Forms.Timer statusTimer = null!;
        private System.Windows.Forms.Timer spectateTimer = null!;
        private System.Windows.Forms.Timer idleTimer = null!;
        private System.Windows.Forms.Timer watchdogTimer = null!;
        private bool _isStreaming = false;
        private bool _isSpectating = false;
        private bool _wasActiveBeforeDisconnect = false;

        // --- PAUSE / RESUME STATE ---
        private string currentUsername = "Guest";
        private string currentUserRole = "Guest";
        private int currentUserPoints = 0;
        private int currentUserNextBonus = 0;

        private Label lblTimerDisplay = null!;
        private Label lblStatus = null!;
        private Label lblConnStatus = null!;

        // WPF Labels for transparency support
        private System.Windows.Controls.TextBlock _wpfStatus = null!;
        private System.Windows.Controls.TextBlock _wpfTimer = null!;
        private System.Windows.Controls.TextBlock _wpfConnStatus = null!;
        private System.Windows.Controls.TextBlock _wpfPcName = null!;
        private System.Windows.Controls.TextBlock _wpfAnnouncement = null!;
        private double _announcementX = 0;

        private Button btnMemberLogin = null!;
        private Button btnInsertCoins = null!;
        private float angle = 0;
        private Random rnd = new Random();

        private readonly string pcName = Environment.MachineName;
        private static readonly HttpClient _httpClient = new HttpClient();

        private TimerOverlayForm? overlayTimer;

        private void SafeInvoke(Action action)
        {
            try
            {
                if (this.IsDisposed || this.Disposing) return;
                if (this.InvokeRequired)
                {
                    if (this.IsHandleCreated)
                    {
                        this.BeginInvoke(action);
                    }
                    else
                    {
                        // Wait for handle creation if not ready
                        this.HandleCreated += (s, e) => {
                            try { if (!this.IsDisposed) this.BeginInvoke(action); } catch { }
                        };
                    }
                }
                else
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                LogLocalError("SafeInvoke Error: " + ex.Message);
            }
        }

        private void LogLocalError(string msg)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {msg}\n");
            }
            catch { }
        }

        public Form1()
        {
            _instance = this;

            _config = new ConfigManager();
            _config.LoadAll(); // Load config first to get PcId and ServerIp
            currentDesign = _config.AnimationDesign;

            _hardware = new HardwareMonitor();
            _audio = new AudioService();
            
            // Ensure ServerIp has http:// prefix
            string serverUrl = EnsureHttpPrefix(_config.ServerIp);
            _socketService = new SocketService(serverUrl, pcName, _config.PcId);

            LoadShopColor();

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = _config.AppTopMost;
            this.BackColor = Color.Black;
            this.DoubleBuffered = true;

            InitializeWpfWallpaper();

            lblStatus = new Label { 
                Text = "PLEASE INSERT COIN", 
                ForeColor = Color.Cyan, 
                Font = new Font("Consolas", _config.StatusFontSize, FontStyle.Bold), 
                AutoSize = true, 
                BackColor = Color.Transparent, 
                Enabled = true,
                Visible = true,
                TabStop = false
            };
            this.Controls.Add(lblStatus);

            lblTimerDisplay = new Label { 
                Text = "0:00", 
                ForeColor = Color.White, 
                Font = new Font("Consolas", _config.TimerFontSize, FontStyle.Bold), 
                AutoSize = true, 
                BackColor = Color.Transparent, 
                Enabled = true,
                Visible = true,
                TabStop = false
            };
            this.Controls.Add(lblTimerDisplay);

            lblConnStatus = new Label { 
                Text = "Connecting...", 
                ForeColor = Color.Cyan, 
                Font = new Font("Consolas", 18, FontStyle.Bold), 
                AutoSize = true, 
                BackColor = Color.FromArgb(150, 20, 20, 25), 
                Enabled = true,
                Visible = true,
                TabStop = false
            };
            this.Controls.Add(lblConnStatus);

            // Member Login Button - Server Theme with Gradient and Rounded Corners
            btnMemberLogin = new Button
            {
                Text = "MEMBER LOGIN",
                Font = new Font("Consolas", 18, FontStyle.Bold),
                Size = new Size(320, 70),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                TabStop = false,
                ForeColor = Color.White
            };
            
            // Apply flat background
            btnMemberLogin.Paint += (sender, e) =>
            {
                Rectangle rect = new Rectangle(0, 0, btnMemberLogin.Width, btnMemberLogin.Height);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(79, 70, 229))) // Indigo-600
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
                
                // Draw rounded corners using Region
                btnMemberLogin.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, btnMemberLogin.Width, btnMemberLogin.Height, 8, 8));
                
                // Draw text
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    e.Graphics.DrawString(btnMemberLogin.Text, btnMemberLogin.Font, Brushes.White, rect, sf);
                }
            };
            
            btnMemberLogin.FlatAppearance.BorderSize = 0;
            btnMemberLogin.Click += (s, e) => ShowMemberLogin();
            btnMemberLogin.MouseEnter += (s, e) => {
                // Hover effect handled by paint if needed, or simple backcolor change if not custom painted
            };
            btnMemberLogin.MouseLeave += (s, e) => {
            };
            
            this.Controls.Add(btnMemberLogin);

            // Insert Coins Button - Server Theme with Gradient and Rounded Corners
            btnInsertCoins = new Button
            {
                Text = "INSERT COINS",
                Font = new Font("Consolas", 18, FontStyle.Bold),
                Size = new Size(320, 70),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                TabStop = false,
                ForeColor = Color.White
            };
            
            // Apply flat background
            btnInsertCoins.Paint += (sender, e) =>
            {
                Rectangle rect = new Rectangle(0, 0, btnInsertCoins.Width, btnInsertCoins.Height);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(16, 185, 129))) // Emerald-500
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
                
                // Draw rounded corners using Region
                btnInsertCoins.Region = Region.FromHrgn(NativeMethods.CreateRoundRectRgn(0, 0, btnInsertCoins.Width, btnInsertCoins.Height, 8, 8));
                
                // Draw text
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    e.Graphics.DrawString(btnInsertCoins.Text, btnInsertCoins.Font, Brushes.White, rect, sf);
                }
            };
            
            btnInsertCoins.FlatAppearance.BorderSize = 0;
            btnInsertCoins.Click += (s, e) => ShowInsertCoinsPopup();
            btnInsertCoins.MouseEnter += (s, e) => {
            };
            btnInsertCoins.MouseLeave += (s, e) => {
            };
            
            this.Controls.Add(btnInsertCoins);

            animationTimer = new System.Windows.Forms.Timer { Interval = 30 };
            animationTimer.Tick += AnimationTimer_Tick!;

            watchdogTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            watchdogTimer.Tick += (s, e) => EnsureWatchdogRunning();

            statusTimer = new System.Windows.Forms.Timer { Interval = 5000 }; // Hardware stats every 5s is enough
            statusTimer.Tick += async (s, e) => await SendHardwareStatus();

            spectateTimer = new System.Windows.Forms.Timer { Interval = 40 }; // ~25 FPS for smoother monitoring
            spectateTimer.Tick += async (s, e) => await StreamScreenFrame();

            idleTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            idleTimer.Tick += IdleTimer_Tick!;

            // Socket Events
            _socketService.OnConnected += () => {
                SafeInvoke(() => {
                    lblConnStatus.Text = "Connected";
                    lblConnStatus.ForeColor = Color.Lime;
                    _wpfConnStatus.Text = "Connected";
                    _wpfConnStatus.Foreground = System.Windows.Media.Brushes.Lime;
                    LogLocalError("Socket Connected Successfully");
                });
                _ = SendHardwareStatus(); // Send initial status immediately
            };

            _socketService.OnError += (err) => {
                SafeInvoke(() => {
                    lblConnStatus.Text = "Error";
                    lblConnStatus.ForeColor = Color.Orange;
                    _wpfConnStatus.Text = "Error";
                    _wpfConnStatus.Foreground = System.Windows.Media.Brushes.Orange;
                    LogLocalError($"Socket Error: {err}");
                });
            };

            _socketService.OnDisconnected += () => {
                SafeInvoke(() => {
                    lblConnStatus.Text = "Disconnected";
                    lblConnStatus.ForeColor = Color.Red;
                    _wpfConnStatus.Text = "Disconnected";
                    _wpfConnStatus.Foreground = System.Windows.Media.Brushes.Red;
                    LogLocalError("Socket Disconnected");
                    
                    _isSpectating = false;
                    if (spectateTimer.Enabled)
                    {
                        spectateTimer.Stop();
                    }

                    // Automatically lock the system if connection is lost in Server Mode
                    if (_config.IsServerMode && !isLocked)
                    {
                        _wasActiveBeforeDisconnect = true;
                        LockSystem();
                    }
                    else
                    {
                        _wasActiveBeforeDisconnect = false;
                    }

                    // Revert to local wallpaper when disconnected
                    ApplyLocalWallpaper();
                });
            };

            _socketService.OnRemoteLock += () => {
                SafeInvoke(() => {
                    _wasActiveBeforeDisconnect = false;
                    LockSystem();
                });
            };

            _socketService.OnRemoteResume += (timeLeft, username, points, nextBonus, isVip) => {
                SafeInvoke(() => {
                    _wasActiveBeforeDisconnect = false;
                    if (timeLeft > 0)
                    {
                        remainingSeconds = timeLeft;
                        currentUsername = username;
                        currentUserPoints = points;
                        currentUserNextBonus = nextBonus;
                        overlayTimer?.SetVipStatus(isVip);
                        UpdateTimerUI();
                        UnlockSystem();
                    }
                    else if (timeLeft == 0)
                    {
                        remainingSeconds = 0;
                        currentUsername = username;
                        currentUserPoints = points;
                        currentUserNextBonus = nextBonus;
                        overlayTimer?.SetVipStatus(isVip);
                        LockSystem();
                    }
                    else // timeLeft < 0, admin resume
                    {
                        UnlockSystem();
                    }
                });
            };

            _socketService.OnRemoteMessage += (msg) => {
                SafeInvoke(() => RobotMessageBox.Show(msg, "Admin Message")); // Keep custom box for admin messages
            };

            _socketService.OnRemoteBuzz += (msg) => {
                SafeInvoke(() => {
                    // 1. Unmute temporarily if locked
                    bool wasLocked = isLocked;
                    if (wasLocked) _audio.SetMute(false);

                    // 2. Play loud sound multiple times for "buzz" effect
                    Task.Run(() => {
                        for (int i = 0; i < 3; i++)
                        {
                            try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
                            Thread.Sleep(400);
                            try { System.Media.SystemSounds.Beep.Play(); } catch { }
                            Thread.Sleep(400);
                        }
                    });
                    
                    // 3. Show a very prominent message
                    BuzzForm buzzForm = new BuzzForm(msg, 5);
                    buzzForm.FormClosed += (s, e) => {
                        // Re-mute if still locked
                        if (isLocked) _audio.SetMute(true);
                    };
                    buzzForm.Show();
                    buzzForm.BringToFront();
                });
            };

            _socketService.OnWallpaperUpdate += (data) => {
                SafeInvoke(() => HandleWallpaperUpdate(data));
            };

            _socketService.OnSystemCommand += (cmd) => {
                SafeInvoke(() => {
                    if (cmd.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                    {
                        PerformShutdown();
                    }
                });
            };

            _socketService.OnAnimationUpdate += (animIndex) => {
                SafeInvoke(() => {
                    currentDesign = animIndex;
                    _config.SaveAnimation(animIndex);
                });
            };

            _socketService.OnStartSpectate += () => {
                SafeInvoke(() => {
                    _isSpectating = true;
                    if (!spectateTimer.Enabled)
                    {
                        spectateTimer.Start();
                    }
                });
            };

            _socketService.OnStopSpectate += () => {
                SafeInvoke(() => {
                    _isSpectating = false;
                    if (spectateTimer.Enabled)
                    {
                        spectateTimer.Stop();
                    }
                });
            };

            _socketService.OnAnnouncementUpdate += (text, enabled) => {
                SafeInvoke(() => {
                    if (enabled && !string.IsNullOrEmpty(text))
                    {
                        _wpfAnnouncement.Text = text.ToUpper();
                        _wpfAnnouncement.Visibility = System.Windows.Visibility.Visible;
                        _announcementX = this.Width; // Start from right side
                    }
                    else
                    {
                        _wpfAnnouncement.Visibility = System.Windows.Visibility.Collapsed;
                    }
                });
            };

            this.Resize += (s, e) => CenterLabels();
            this.Shown += (s, e) =>
            {
                CenterLabels();
                lblConnStatus.Location = new Point(20, 20);
                
                // Start timers only after form is shown
                animationTimer.Start();
                statusTimer.Start();
                idleTimer.Start();
                
                // Create watchdog flag and start monitoring
#if !DEBUG
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "watchdog_active.tmp"), "1"); } catch { }
                watchdogTimer.Start();
                EnsureWatchdogRunning();
#endif

                if (isLocked)
                {
                    btnMemberLogin.Visible = true;
                    btnMemberLogin.BringToFront();
                    if (btnInsertCoins != null)
                    {
                        btnInsertCoins.Visible = (_config.SystemMode == 2);
                        if (btnInsertCoins.Visible) btnInsertCoins.BringToFront();
                    }
                    _audio.SetMute(true);
                }
                SetHook();
                ApplySecurityPolicies(isLocked);

                // if server IP was loaded and in Server Mode, try connecting immediately (non-blocking)
                if (_config.IsServerMode && !string.IsNullOrWhiteSpace(_config.ServerIp))
                {
                    LogLocalError($"Attempting connection to: {_config.ServerIp}");
                    _ = Task.Run(async () => {
                        await Task.Delay(1000);
                        await InitializeNetwork();
                    });
                }

                // Initialize overlay timer
                overlayTimer = new TimerOverlayForm(_socketService);
                overlayTimer.TopMost = false; // Always bottom as requested
                overlayTimer.RegisterRequested += (s, e) => ShowMemberRegister();
                overlayTimer.LogoutRequested += async (s, e) =>
                {
                    if (MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                    {
                        if (_config.IsServerMode)
                        {
                            await _socketService.LogoutAsync(currentUsername, _hardware.GetMacAddress());
                        }
                        
                        remainingSeconds = 0;
                        currentUsername = "Guest";
                        currentUserRole = "Guest";
                        currentUserPoints = 0;
                        currentUserNextBonus = 0;
                        _wasActiveBeforeDisconnect = false;
                        LockSystem();
                    }
                };
                overlayTimer.VoucherSubmitted += async (code) =>
                {
                    if (_config.IsServerMode)
                    {
                        await _socketService.UseVoucherAsync(code, _hardware.GetMacAddress());
                    }
                };

                // Apply local wallpaper if exists
                ApplyLocalWallpaper();

                // Ensure startup task is created (Fix for Admin apps not starting via Registry/Startup folder)
                // We now rely on the installer to create the Scheduled Task for better reliability.
                EnsureStartupTask();

                // Initialize Serial Port if needed
                if (_config.UseLocalCoinslot) InitializeSerialPort();
            };
        }

        private void InitializeSerialPort()
        {
            if (_config.ComPort == "None" || string.IsNullOrEmpty(_config.ComPort)) return;

            try
            {
                _serialPort?.Close();
                _serialPort?.Dispose();

                _serialPort = new SerialPort(_config.ComPort, 9600, Parity.None, 8, StopBits.One);
                _serialPort.DtrEnable = true;
                _serialPort.DataReceived += (s, e) =>
                {
                    try
                    {
                        string data = _serialPort.ReadExisting();
                        SafeInvoke(() => AddTimeFromPulse());
                    }
                    catch { }
                };
                _serialPort.Open();
            }
            catch (Exception ex)
            {
                LogLocalError($"Failed to open Serial Port: {ex.Message}");
            }
        }

        private void AddTimeFromPulse()
        {
            if (_config.SystemMode == 2) // Server with Local Coin
            {
                // Send to server instead of adding locally
                _ = _socketService.SendCoinInsertedAsync(1); // Assuming 1 pulse = 1 peso
            }
            else
            {
                // Default to 1 peso rate if pulse detected
                remainingSeconds += _config.Rate1 * 60;
                UpdateTimerUI();
                if (isLocked && remainingSeconds > 0) UnlockSystem();
            }
        }

        private void ApplyLocalWallpaper()
        {
            if (!string.IsNullOrEmpty(_config.WallpaperPath) && File.Exists(_config.WallpaperPath))
            {
                try
                {
                    string path = _config.WallpaperPath;
                    string ext = Path.GetExtension(path).ToLower();
                    bool isVideo = ext == ".mp4" || ext == ".webm" || ext == ".mov" || ext == ".wmv" || ext == ".avi";

                    if (isVideo)
                    {
                        PlayVideoWallpaper(path);
                    }
                    else
                    {
                        _imageBackground.Source = new BitmapImage(new Uri(path));
                        _imageBackground.Visibility = System.Windows.Visibility.Visible;
                        _videoPlayer.Visibility = System.Windows.Visibility.Collapsed;
                        _videoPlayer.Stop();
                        this.BackgroundImage = null;
                    }
                }
                catch { }
            }
        }

        private void IdleTimer_Tick(object sender, EventArgs e)
        {
            // Pause shutdown timer if any popup is open
            if (isInsertCoinsOpen || isMemberLoginOpen || isMemberRegisterOpen || isAdminPromptOpen)
            {
                return;
            }

            // 1. If we have paid time or are unlocked, we are not in auto-shutdown mode
            if (remainingSeconds > 0 || !isLocked)
            {
                if (!isLocked && remainingSeconds > 0 && !_config.IsServerMode)
                {
                    remainingSeconds--;
                    UpdateTimerUI();
                    if (remainingSeconds <= 0)
                    {
                        LockSystem();
                    }
                }

                _idleSecondsRemaining = -1;
                return;
            }

            // 2. We are locked and have no paid time.
            if (_config.IdleShutdownMinutes <= 0)
            {
                _idleSecondsRemaining = -1;
                if (lblTimerDisplay.Text != "0:00")
                {
                    lblTimerDisplay.Text = "0:00";
                    lblTimerDisplay.ForeColor = Color.Lime;
                    CenterLabels();
                }
                return;
            }

            // 3. Initialize countdown if needed
            if (_idleSecondsRemaining == -1)
            {
                _idleSecondsRemaining = _config.IdleShutdownMinutes * 60;
            }

            // 4. Decrement countdown
            if (_idleSecondsRemaining > 0)
            {
                _idleSecondsRemaining--;
                if (_idleSecondsRemaining <= 10)
                {
                    try { SystemSounds.Hand.Play(); } catch { }
                }
            }

            // 6. Update Display
            TimeSpan t = TimeSpan.FromSeconds(_idleSecondsRemaining);
            string s = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
            
            if (lblTimerDisplay.Text != s)
            {
                lblTimerDisplay.Text = s;
                if (_idleSecondsRemaining <= 60) lblTimerDisplay.ForeColor = Color.Red;
                else if (_idleSecondsRemaining <= 300) lblTimerDisplay.ForeColor = Color.Orange;
                else lblTimerDisplay.ForeColor = Color.Lime;

                // Update WPF Timer
                _wpfTimer.Text = s;
                if (_idleSecondsRemaining <= 60) _wpfTimer.Foreground = System.Windows.Media.Brushes.Red;
                else if (_idleSecondsRemaining <= 300) _wpfTimer.Foreground = System.Windows.Media.Brushes.Orange;
                else _wpfTimer.Foreground = System.Windows.Media.Brushes.Lime;

                CenterLabels();
            }

            // 7. Shutdown when countdown reaches zero
            if (_idleSecondsRemaining == 0)
            {
                if (!_config.DisableShutdown)
                {
                    PerformShutdown();
                }
            }
        }

        private string EnsureHttpPrefix(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "http://" + url;
            }
            return url;
        }

        private async Task StreamScreenFrame()
        {
            if (!_isSpectating)
            {
                if (spectateTimer.Enabled) spectateTimer.Stop();
                return;
            }
            if (_isStreaming || !_socketService.IsConnected) return;
            _isStreaming = true;

            try
            {
                // Reduce quality to 25L for faster transmission and lower CPU usage
                byte[] screenshot = CaptureScreenJpeg(25L);
                if (screenshot != null && screenshot.Length > 0)
                {
                    string base64 = Convert.ToBase64String(screenshot);
                    await _socketService.SendScreenFrameAsync(base64);
                }
            }
            catch { }
            finally
            {
                _isStreaming = false;
            }
        }

        private async Task SendHardwareStatus()
        {
            if (!_socketService.IsConnected) return;

            try
            {
                var status = await Task.Run(() => new
                {
                    pc_id = _config.PcId,
                    pc_name = pcName,
                    mac = _hardware.GetMacAddress(), // Added for WOL support
                    username = currentUsername,
                    time_left = remainingSeconds,
                    was_active = _wasActiveBeforeDisconnect,
                    temp = _hardware.GetCpuTemp(),
                    cpu = _hardware.GetCpuUsage(),
                    cpu_model = _hardware.GetCpuModel(),
                    gpu = _hardware.GetGpuInfo(),
                    ram = _hardware.GetRamUsage(),
                    disk = _hardware.GetDiskUsage(),
                    net = _hardware.GetNetworkSpeed(),
                    ip_address = _hardware.GetIpAddress(),
                    mac_address = _hardware.GetMacAddress(),
                    os_version = _hardware.GetOsVersion(),
                    motherboard = _hardware.GetMotherboard(),
                    status = isLocked ? "Locked" : "Active"
                });

                await _socketService.SendStatusAsync(status);
            }
            catch { }
        }


        private void ShowMemberLogin()
        {
            if (isMemberLoginOpen) return;

            if (!_socketService.IsConnected)
            {
                bool wasTopMost = this.TopMost;
                this.TopMost = false; 
                MessageBox.Show("Cannot login: Not connected to server.", "Network Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.TopMost = wasTopMost;
                return;
            }

            isMemberLoginOpen = true;
            bool originalTopMost = this.TopMost;
            this.TopMost = false;

            using (MemberLoginForm loginForm = new MemberLoginForm())
            {
                loginForm.LoginRequested += async (user, pass, voucher) =>
                {
                    string identifier = !string.IsNullOrWhiteSpace(user) ? user : voucher;
                    if (!string.IsNullOrEmpty(identifier) && _userLockouts.ContainsKey(identifier))
                    {
                        if (DateTime.Now < _userLockouts[identifier])
                        {
                            TimeSpan remaining = _userLockouts[identifier] - DateTime.Now;
                            int mins = (int)Math.Ceiling(remaining.TotalMinutes);
                            RobotMessageBox.Show($"ACCOUNT DISABLED: This account is temporarily disabled for {mins} minute(s) due to multiple logins.", "Security Alert");
                            return;
                        }
                    }

                    loginForm.SetLoading(true);
                    _ = Task.Delay(10000).ContinueWith(t =>
                    {
                        SafeInvoke(() =>
                        {
                            if (loginForm != null && !loginForm.IsDisposed && !loginForm.Enabled)
                            {
                                loginForm.SetLoading(false);
                            }
                        });
                    });

                    try 
                    { 
                        if (!_socketService.IsConnected)
                        {
                            loginForm.SetLoading(false);
                            MessageBox.Show("Lost connection to server. Please wait for reconnection.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        await _socketService.LoginAsync(user, pass, voucher, _hardware.GetMacAddress()); 
                    }
                    catch (Exception ex) 
                    {
                        loginForm.SetLoading(false);
                        MessageBox.Show("Error sending login: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                loginForm.ShowDialog(this);
            }

            isMemberLoginOpen = false;
            if (isLocked) this.TopMost = originalTopMost;
        }

        private void ShowMemberRegister()
        {
            isMemberRegisterOpen = true;
            using (MemberRegisterForm regForm = new MemberRegisterForm())
            {
                int fee = int.Parse(_socketService.GetSetting("register_fee", "0"));
                regForm.SetFee(fee);
                regForm.RegisterRequested += async (user, pass) =>
                {
                    regForm.SetLoading(true);
                    try
                    {
                        await _socketService.RegisterMemberAsync(user, pass, pcName);
                    }
                    catch (Exception ex)
                    {
                        regForm.SetLoading(false);
                        MessageBox.Show("Error sending registration: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                regForm.ShowDialog(this);
            }
            isMemberRegisterOpen = false;
        }
        

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (!isLocked) 
            {
                if (_wpfStatus.Visibility != System.Windows.Visibility.Collapsed) _wpfStatus.Visibility = System.Windows.Visibility.Collapsed;
                if (_wpfTimer.Visibility != System.Windows.Visibility.Collapsed) _wpfTimer.Visibility = System.Windows.Visibility.Collapsed;
                if (_wpfConnStatus.Visibility != System.Windows.Visibility.Collapsed) _wpfConnStatus.Visibility = System.Windows.Visibility.Collapsed;
                if (_wpfPcName.Visibility != System.Windows.Visibility.Collapsed) _wpfPcName.Visibility = System.Windows.Visibility.Collapsed;
                
                if (lblStatus.Visible) lblStatus.Visible = false;
                if (lblTimerDisplay.Visible) lblTimerDisplay.Visible = false;
                if (lblConnStatus.Visible) lblConnStatus.Visible = false;
                return;
            }
            
            if (_wpfStatus.Visibility != System.Windows.Visibility.Visible) _wpfStatus.Visibility = System.Windows.Visibility.Visible;
            if (_wpfTimer.Visibility != System.Windows.Visibility.Visible) _wpfTimer.Visibility = System.Windows.Visibility.Visible;
            if (_wpfConnStatus.Visibility != System.Windows.Visibility.Visible) _wpfConnStatus.Visibility = System.Windows.Visibility.Visible;
            
            if (_config.ShowPcName)
            {
                if (_wpfPcName.Visibility != System.Windows.Visibility.Visible) _wpfPcName.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                if (_wpfPcName.Visibility != System.Windows.Visibility.Collapsed) _wpfPcName.Visibility = System.Windows.Visibility.Collapsed;
            }
            
            // Hide WinForms labels to avoid black background issue over WPF wallpaper
            if (lblStatus.Visible) lblStatus.Visible = false;
            if (lblTimerDisplay.Visible) lblTimerDisplay.Visible = false;
            if (lblConnStatus.Visible) lblConnStatus.Visible = false;

            angle += 0.12f;
            double offsetX = 0;
            double offsetY = 0;

            float baseStatusSize = _config.StatusFontSize;
            _wpfStatus.FontSize = baseStatusSize;

            switch (currentDesign)
            {
                case 0: // Rainbow
                    Color c0 = ColorFromHSL((int)((angle * 50) % 360), 1, 0.5);
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(c0.R, c0.G, c0.B));
                    break;
                case 1: // Floating
                    offsetY = Math.Sin(angle * 0.8) * 15;
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(_shopNameColor.R, _shopNameColor.G, _shopNameColor.B));
                    break;
                case 2: // Pulse Size
                    float sz = baseStatusSize + (float)(Math.Sin(angle * 1.2) * (baseStatusSize * 0.15));
                    _wpfStatus.FontSize = sz;
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(_shopNameColor.R, _shopNameColor.G, _shopNameColor.B));
                    break;
                case 3: // Shake
                    offsetX = rnd.Next(-8, 9);
                    offsetY = rnd.Next(-8, 9);
                    Color c3 = (DateTime.Now.Millisecond % 500 < 250) ? Color.Red : _shopNameColor;
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(c3.R, c3.G, c3.B));
                    break;
                case 4: // Slide
                    offsetX = Math.Cos(angle) * 60;
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(_shopNameColor.R, _shopNameColor.G, _shopNameColor.B));
                    break;
                case 5: // Disco
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256)));
                    break;
                case 6: // Classic
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(_shopNameColor.R, _shopNameColor.G, _shopNameColor.B));
                    break;
                case 7: // Wave
                    offsetX = Math.Sin(angle * 1.5) * 120;
                    offsetY = Math.Sin(angle * 0.8) * 18;
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(_shopNameColor.R, _shopNameColor.G, _shopNameColor.B));
                    break;
                case 8: // Zoom Pulse
                    float sz8 = (baseStatusSize * 0.8f) + (float)(Math.Abs(Math.Sin(angle * 1.6)) * (baseStatusSize * 0.4));
                    _wpfStatus.FontSize = sz8;
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(_shopNameColor.R, _shopNameColor.G, _shopNameColor.B));
                    break;
                case 9: // Spin/Circle
                    int radius = 90;
                    offsetX = Math.Cos(angle * 1.4) * radius;
                    offsetY = Math.Sin(angle * 1.4) * radius;
                    Color c9 = ColorFromHSL((int)((angle * 80) % 360), 0.9, 0.55);
                    _wpfStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(c9.R, c9.G, c9.B));
                    break;
            }

            _wpfStatus.Margin = new System.Windows.Thickness(offsetX, offsetY - 150, 0, 0);

            // Announcement Animation
            if (_wpfAnnouncement.Visibility == System.Windows.Visibility.Visible)
            {
                _announcementX -= 3; // Scroll speed
                if (_announcementX < -2000) // Safety reset if it goes too far
                {
                    _announcementX = this.Width;
                }
                
                // Reset if it's likely off-screen (ActualWidth is only available after render)
                if (_wpfAnnouncement.ActualWidth > 0 && _announcementX < -_wpfAnnouncement.ActualWidth)
                {
                    _announcementX = this.Width;
                }

                _wpfAnnouncement.Margin = new System.Windows.Thickness(_announcementX, 20, 0, 0);
            }

            // Timer Animation (Idle Shutdown)
            double timerOffsetX = 0;
            double timerOffsetY = 0;
            float baseTimerSize = _config.TimerFontSize;
            if (_idleSecondsRemaining > 0 && _idleSecondsRemaining <= 10)
            {
                float szTimer = baseTimerSize + (float)(Math.Abs(Math.Sin(angle * 2)) * (baseTimerSize * 0.6));
                _wpfTimer.FontSize = szTimer;
                timerOffsetX = rnd.Next(-15, 16);
                timerOffsetY = rnd.Next(-15, 16);
            }
            else
            {
                _wpfTimer.FontSize = baseTimerSize;
            }
            _wpfTimer.Margin = new System.Windows.Thickness(timerOffsetX, timerOffsetY + 150, 0, 0);

            // PC Name Animation and Positioning
            if (_config.ShowPcName)
            {
                double pcOffsetX = 0;
                double pcOffsetY = 0;
                float basePcSize = _config.PcNameFontSize;
                _wpfPcName.FontSize = basePcSize;
                
                // Animation logic (similar to insert coin)
                byte pcOpacity = (byte)_config.PcNameOpacity;
                Color pcBaseColor = ColorTranslator.FromHtml(_config.PcNameColor);
                System.Windows.Media.Color wpfPcBaseColor = System.Windows.Media.Color.FromArgb(pcOpacity, pcBaseColor.R, pcBaseColor.G, pcBaseColor.B);
                
                switch (_config.PcNameAnimation)
                {
                    case 0: // None
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 1: // Rainbow
                        Color cPc = ColorFromHSL((int)((angle * 40) % 360), 0.8, 0.6);
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(pcOpacity, cPc.R, cPc.G, cPc.B));
                        break;
                    case 2: // Floating
                        pcOffsetY = Math.Sin(angle * 0.8) * 15;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 3: // Pulse
                        float szPc = basePcSize + (float)(Math.Sin(angle) * (basePcSize * 0.15));
                        _wpfPcName.FontSize = szPc;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 4: // Shake
                        pcOffsetX = rnd.Next(-3, 4);
                        pcOffsetY = rnd.Next(-3, 4);
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 5: // Slide
                        pcOffsetX = Math.Cos(angle * 0.7) * 30;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 6: // Disco
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(pcOpacity, (byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256)));
                        break;
                    case 7: // Wave
                        pcOffsetX = Math.Sin(angle * 1.2) * 20;
                        pcOffsetY = Math.Cos(angle * 0.8) * 10;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 8: // Zoom Pulse
                        float szPc7 = basePcSize + (float)(Math.Abs(Math.Sin(angle * 1.5)) * (basePcSize * 0.3));
                        _wpfPcName.FontSize = szPc7;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 9: // Spin
                        pcOffsetX = Math.Cos(angle * 2) * 15;
                        pcOffsetY = Math.Sin(angle * 2) * 15;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 10: // Glitch
                        if (rnd.Next(100) < 10) {
                            pcOffsetX = rnd.Next(-10, 11);
                            pcOffsetY = rnd.Next(-10, 11);
                        }
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 11: // Blink
                        byte blinkAlpha = (byte)(pcOpacity * (0.5 + Math.Sin(angle * 3) * 0.5));
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(blinkAlpha, pcBaseColor.R, pcBaseColor.G, pcBaseColor.B));
                        break;
                    case 12: // Swing
                        pcOffsetX = Math.Sin(angle) * 20;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 13: // Bounce
                        pcOffsetY = Math.Abs(Math.Sin(angle * 2)) * -30;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 14: // Color Cycle
                        Color c13 = ColorFromHSL((int)((angle * 30) % 360), 0.7, 0.5);
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(pcOpacity, c13.R, c13.G, c13.B));
                        break;
                    case 15: // Vibrate
                        pcOffsetX = rnd.Next(-2, 3);
                        pcOffsetY = rnd.Next(-2, 3);
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 16: // Orbit
                        pcOffsetX = Math.Cos(angle) * 40;
                        pcOffsetY = Math.Sin(angle * 0.5) * 20;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 17: // Flash
                        byte flashAlpha = (byte)(pcOpacity * (rnd.Next(2) == 0 ? 0 : 1));
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(flashAlpha, pcBaseColor.R, pcBaseColor.G, pcBaseColor.B));
                        break;
                    case 18: // Rotate
                        pcOffsetX = Math.Sin(angle * 3) * 10;
                        pcOffsetY = Math.Cos(angle * 3) * 10;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 19: // Skew
                        pcOffsetX = Math.Tan(Math.Sin(angle)) * 10;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 20: // Drop
                        pcOffsetY = (angle * 50) % 200 - 100;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 21: // Spiral
                        double spiralRadius = (angle * 10) % 50;
                        pcOffsetX = Math.Cos(angle * 5) * spiralRadius;
                        pcOffsetY = Math.Sin(angle * 5) * spiralRadius;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 22: // Heartbeat
                        float hbSz = basePcSize * (1.0f + (float)Math.Pow(Math.Sin(angle * 2), 4) * 0.4f);
                        _wpfPcName.FontSize = hbSz;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 23: // Heartbeat
                        float hbSz2 = basePcSize * (1.0f + (float)Math.Pow(Math.Sin(angle * 2), 4) * 0.4f);
                        _wpfPcName.FontSize = hbSz2;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 24: // Rubber Band
                        pcOffsetX = Math.Sin(angle * 4) * 15;
                        _wpfPcName.FontSize = basePcSize * (1.0f + (float)Math.Abs(Math.Cos(angle * 4)) * 0.2f);
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 25: // Jello
                        pcOffsetX = Math.Sin(angle * 5) * (10 * (float)Math.Exp(-0.1 * (angle % 10)));
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 26: // Wobble
                        pcOffsetX = (float)Math.Sin(angle * 2) * 25;
                        pcOffsetY = (float)Math.Abs(Math.Cos(angle * 2)) * 10;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                    case 27: // Tada
                        float tadaSz = basePcSize * (1.0f + (float)Math.Abs(Math.Sin(angle * 3)) * 0.1f);
                        pcOffsetX = rnd.Next(-2, 3);
                        _wpfPcName.FontSize = tadaSz;
                        _wpfPcName.Foreground = new System.Windows.Media.SolidColorBrush(wpfPcBaseColor);
                        break;
                }

                // Positioning logic
                // 0: TL, 1: TR, 2: BL, 3: BR
                switch (_config.PcNamePosition)
                {
                    case 0: // Top Left
                        _wpfPcName.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                        _wpfPcName.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                        _wpfPcName.Margin = new System.Windows.Thickness(20 + pcOffsetX, 60 + pcOffsetY, 0, 0);
                        break;
                    case 1: // Top Right
                        _wpfPcName.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                        _wpfPcName.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                        _wpfPcName.Margin = new System.Windows.Thickness(0, 60 + pcOffsetY, 20 - pcOffsetX, 0);
                        break;
                    case 2: // Bottom Left
                        _wpfPcName.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                        _wpfPcName.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                        _wpfPcName.Margin = new System.Windows.Thickness(20 + pcOffsetX, 0, 0, 20 - pcOffsetY);
                        break;
                    case 3: // Bottom Right
                        _wpfPcName.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                        _wpfPcName.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                        _wpfPcName.Margin = new System.Windows.Thickness(0, 0, 20 - pcOffsetX, 20 - pcOffsetY);
                        break;
                }
            }
        }

        public void ShowAdminPanel()
        {
            if (isAdminPromptOpen) return;
            isAdminPromptOpen = true;
            this.TopMost = false;

            using (Form adminForm = BuildAdminForm())
            {
                adminForm.ShowDialog();
            }

            if (isLocked) this.TopMost = _config.AppTopMost;
            isAdminPromptOpen = false;
        }

        private Form BuildAdminForm()
        {
            // Modern Web Theme Colors
            Color backColor = Color.FromArgb(31, 41, 55); // Gray-800
            Color foreColor = Color.FromArgb(243, 244, 246); // Gray-100
            Color primaryColor = Color.FromArgb(79, 70, 229); // Indigo-600
            Color inputBack = Color.FromArgb(55, 65, 81); // Gray-700
            Color borderColor = Color.FromArgb(75, 85, 99); // Gray-600
            
            Font headerFont = new Font("Segoe UI", 18, FontStyle.Bold);
            Font regularFont = new Font("Segoe UI", 10, FontStyle.Regular);
            Font btnFont = new Font("Segoe UI", 10, FontStyle.Bold);

            int padding = 40;
            int contentWidth = 520;
            
            Form adminForm = new Form
            {
                Text = "SYSTEM: ADMIN CONFIGURATION",
                Size = new Size(650, 700),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.None,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = backColor,
                ForeColor = foreColor,
                TopMost = true
            };

            // Dragging functionality
            Point dragStart = Point.Empty;
            adminForm.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) dragStart = e.Location; };
            adminForm.MouseMove += (s, e) => {
                if (e.Button == MouseButtons.Left) {
                    adminForm.Left += e.X - dragStart.X;
                    adminForm.Top += e.Y - dragStart.Y;
                }
            };

            // Add close button for borderless form
            Button btnCloseTop = new Button
            {
                Text = "×",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Size = new Size(30, 30),
                Location = new Point(adminForm.Width - 40, 10),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Gray,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnCloseTop.FlatAppearance.BorderSize = 0;
            btnCloseTop.Click += (s, e) => adminForm.Close();
            adminForm.Controls.Add(btnCloseTop);
            btnCloseTop.BringToFront();

            // Main Scrollable Area
            Panel scrollPanel = new Panel {
                Location = new Point(10, 50),
                Size = new Size(adminForm.Width - 20, adminForm.Height - 60),
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            adminForm.Controls.Add(scrollPanel);

            Panel container = new Panel { 
                Dock = DockStyle.Top, 
                AutoSize = true, 
                AutoSizeMode = AutoSizeMode.GrowAndShrink, 
                Padding = new Padding(padding),
                BackColor = Color.Transparent
            };
            scrollPanel.Controls.Add(container);

            adminForm.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                
                Pen p = new Pen(borderColor, 1);
                g.DrawRectangle(p, 1, 1, adminForm.Width - 3, adminForm.Height - 3);
                g.DrawString("ADMIN CONFIGURATION", new Font("Segoe UI", 9, FontStyle.Bold), new SolidBrush(Color.Gray), 50, 15);
            };

            int currentY = padding;

            Label lblTitle = new Label { 
                Text = "System Settings", 
                Font = headerFont, 
                AutoSize = true, 
                Location = new Point(padding, currentY), 
                ForeColor = primaryColor 
            };
            container.Controls.Add(lblTitle);
            currentY += 60;

            Action<string, int> addSectionHeader = (title, y) => {
                Label lbl = new Label { 
                    Text = title.ToUpper(), 
                    Font = new Font("Segoe UI", 9, FontStyle.Bold), 
                    AutoSize = true, 
                    Location = new Point(padding, y), 
                    ForeColor = Color.FromArgb(156, 163, 175) // Gray-400
                };
                container.Controls.Add(lbl);
                
                Panel line = new Panel {
                    Location = new Point(padding, y + 20),
                    Size = new Size(contentWidth, 1),
                    BackColor = borderColor
                };
                container.Controls.Add(line);
            };

            addSectionHeader("Authentication", currentY);
            currentY += 40;

            Label lblPass = new Label { Text = "Current Password", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblPass);
            currentY += 25;
            TextBox txtPass = CreateModernTextBox(padding, currentY, contentWidth, "Enter Current Password", inputBack, foreColor, regularFont);
            txtPass.PasswordChar = '•';
            container.Controls.Add(txtPass);
            currentY += 50;

            Label lblNewPass = new Label { Text = "New Password", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblNewPass);
            currentY += 25;
            TextBox txtNewPass = CreateModernTextBox(padding, currentY, contentWidth, "Leave blank to keep current", inputBack, foreColor, regularFont);
            txtNewPass.PasswordChar = '•'; txtNewPass.Enabled = false;
            container.Controls.Add(txtNewPass);
            currentY += 50;

            Label lblConfirmPass = new Label { Text = "Confirm New Password", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblConfirmPass);
            currentY += 25;
            TextBox txtConfirmPass = CreateModernTextBox(padding, currentY, contentWidth, "Confirm New Password", inputBack, foreColor, regularFont);
            txtConfirmPass.PasswordChar = '•'; txtConfirmPass.Enabled = false;
            container.Controls.Add(txtConfirmPass);
            currentY += 70;

            addSectionHeader("System Mode", currentY);
            currentY += 40;

            Panel modePanel = new Panel { Location = new Point(padding, currentY), Size = new Size(contentWidth, 50), BackColor = inputBack };
            container.Controls.Add(modePanel);

            RadioButton rbServer = new RadioButton { Text = "Server", Location = new Point(10, 12), AutoSize = true, Font = regularFont, Checked = _config.SystemMode == 0, Enabled = false };
            RadioButton rbStandalone = new RadioButton { Text = "Standalone", Location = new Point(100, 12), AutoSize = true, Font = regularFont, Checked = _config.SystemMode == 1, Enabled = false };
            RadioButton rbServerLocal = new RadioButton { Text = "Server + Local Coin", Location = new Point(220, 12), AutoSize = true, Font = regularFont, Checked = _config.SystemMode == 2, Enabled = false };
            modePanel.Controls.Add(rbServer);
            modePanel.Controls.Add(rbStandalone);
            modePanel.Controls.Add(rbServerLocal);
            currentY += 70;

            addSectionHeader("Server Connection", currentY);
            currentY += 40;

            Label lblUrl = new Label { Text = "Server URL / IP Address", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblUrl);
            currentY += 25;
            TextBox txtServerIp = CreateModernTextBox(padding, currentY, contentWidth, "e.g. http://192.168.1.100:3000", inputBack, foreColor, regularFont);
            txtServerIp.Text = _config.ServerIp; txtServerIp.Enabled = false;
            container.Controls.Add(txtServerIp);
            currentY += 70;

            addSectionHeader("Coin Rates (Minutes)", currentY);
            currentY += 40;

            int boxW = (contentWidth - 45) / 4;
            var t1 = CreateRateInput(padding, currentY, boxW, "₱1", _config.Rate1, inputBack, foreColor, regularFont, container);
            var t5 = CreateRateInput(padding + boxW + 15, currentY, boxW, "₱5", _config.Rate5, inputBack, foreColor, regularFont, container);
            var t10 = CreateRateInput(padding + (boxW + 15) * 2, currentY, boxW, "₱10", _config.Rate10, inputBack, foreColor, regularFont, container);
            var t20 = CreateRateInput(padding + (boxW + 15) * 3, currentY, boxW, "₱20", _config.Rate20, inputBack, foreColor, regularFont, container);
            currentY += 90;

            addSectionHeader("General Configuration", currentY);
            currentY += 40;

            Label lblAnim = new Label { Text = "Lockscreen Animation Style", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblAnim);
            currentY += 25;
            ComboBox cmbAnim = new ComboBox { 
                Location = new Point(padding, currentY), 
                Width = contentWidth, 
                FlatStyle = FlatStyle.Flat, 
                BackColor = inputBack, 
                ForeColor = foreColor, 
                DropDownStyle = ComboBoxStyle.DropDownList, 
                Font = regularFont, 
                Enabled = false 
            };
            cmbAnim.Items.AddRange(new string[] { "Rainbow", "Floating", "Pulse", "Shake", "Slide", "Disco", "Classic", "Wave", "Zoom", "Spin" });
            cmbAnim.SelectedIndex = Math.Max(0, Math.Min(currentDesign, cmbAnim.Items.Count - 1));
            container.Controls.Add(cmbAnim);
            currentY += 60;

            CheckBox chkTaskMgr = CreateModernCheckBox("Enable Task Manager", padding, currentY, _config.AllowTaskManager, regularFont, primaryColor, foreColor, container);
            currentY += 35;

            CheckBox chkTopMost = CreateModernCheckBox("Always Top Most", padding, currentY, _config.AppTopMost, regularFont, primaryColor, foreColor, container);
            currentY += 35;

            CheckBox chkDisableShutdown = CreateModernCheckBox("Disable Auto-Shutdown", padding, currentY, _config.DisableShutdown, regularFont, primaryColor, foreColor, container);
            currentY += 35;

            CheckBox chkDisableRun = CreateModernCheckBox("Disable Run (Win+R)", padding, currentY, _config.DisableRun, regularFont, primaryColor, foreColor, container);
            currentY += 35;

            CheckBox chkDisableCmd = CreateModernCheckBox("Disable Command Prompt", padding, currentY, _config.DisableCmd, regularFont, primaryColor, foreColor, container);
            currentY += 35;

            CheckBox chkDisableTerminal = CreateModernCheckBox("Disable Windows Terminal", padding, currentY, _config.DisableTerminal, regularFont, primaryColor, foreColor, container);
            currentY += 35;

            CheckBox chkDisableCAD = CreateModernCheckBox("Disable Ctrl+Alt+Del Options (Lock, Sign out, etc.)", padding, currentY, _config.DisableCADOptions, regularFont, primaryColor, foreColor, container);
            currentY += 35;

            CheckBox chkRunAtStartup = CreateModernCheckBox("Run Application at Startup", padding, currentY, _config.RunAtStartup, regularFont, primaryColor, foreColor, container);
            currentY += 35;

            currentY += 15;

            Label lblIdle = new Label { Text = "Auto Shutdown (Minutes, 0 to disable)", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblIdle);
            currentY += 25;
            TextBox txtIdle = CreateModernTextBox(padding, currentY, contentWidth, "0", inputBack, foreColor, regularFont);
            txtIdle.Text = _config.IdleShutdownMinutes.ToString(); txtIdle.Enabled = false;
            container.Controls.Add(txtIdle);
            currentY += 70;

            addSectionHeader("Member Settings", currentY);
            currentY += 40;

            Label lblRegFee = new Label { Text = "Registration Fee (Pesos)", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblRegFee);
            currentY += 25;
            TextBox txtRegFee = CreateModernTextBox(padding, currentY, contentWidth, "0", inputBack, foreColor, regularFont);
            txtRegFee.Text = _socketService.GetSetting("register_fee", "0");
            txtRegFee.Enabled = false;
            container.Controls.Add(txtRegFee);
            currentY += 70;

            addSectionHeader("PC Name Display Settings", currentY);
            currentY += 40;

            CheckBox chkShowPcName = CreateModernCheckBox("Show PC Name on Lockscreen", padding, currentY, _config.ShowPcName, regularFont, primaryColor, foreColor, container);
            currentY += 40;

            Label lblPcPos = new Label { Text = "PC Name Position", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblPcPos);
            currentY += 25;
            ComboBox cmbPcPos = new ComboBox { 
                Location = new Point(padding, currentY), 
                Width = contentWidth, 
                FlatStyle = FlatStyle.Flat, 
                BackColor = inputBack, 
                ForeColor = foreColor, 
                DropDownStyle = ComboBoxStyle.DropDownList, 
                Font = regularFont, 
                Enabled = false 
            };
            cmbPcPos.Items.AddRange(new string[] { "Top Left", "Top Right", "Bottom Left", "Bottom Right" });
            cmbPcPos.SelectedIndex = Math.Max(0, Math.Min(_config.PcNamePosition, cmbPcPos.Items.Count - 1));
            container.Controls.Add(cmbPcPos);
            currentY += 60;

            Label lblPcAnim = new Label { Text = "PC Name Animation Style", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblPcAnim);
            currentY += 25;
            ComboBox cmbPcAnim = new ComboBox { 
                Location = new Point(padding, currentY), 
                Width = contentWidth, 
                FlatStyle = FlatStyle.Flat, 
                BackColor = inputBack, 
                ForeColor = foreColor, 
                DropDownStyle = ComboBoxStyle.DropDownList, 
                Font = regularFont, 
                Enabled = false 
            };
            cmbPcAnim.Items.AddRange(new string[] { "None", "Rainbow", "Floating", "Pulse", "Shake", "Slide", "Disco", "Wave", "Zoom Pulse", "Spin", "Glitch", "Blink", "Swing", "Bounce", "Color Cycle", "Vibrate", "Orbit", "Flash", "Rotate", "Skew", "Drop", "Spiral", "Heartbeat", "Rubber Band", "Jello", "Wobble", "Tada" });
            cmbPcAnim.SelectedIndex = Math.Max(0, Math.Min(_config.PcNameAnimation, cmbPcAnim.Items.Count - 1));
            container.Controls.Add(cmbPcAnim);
            currentY += 60;

            Label lblPcSize = new Label { Text = "PC Name Font Size", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblPcSize);
            currentY += 25;
            TextBox txtPcSize = CreateModernTextBox(padding, currentY, contentWidth, "24", inputBack, foreColor, regularFont);
            txtPcSize.Text = _config.PcNameFontSize.ToString(); txtPcSize.Enabled = false;
            container.Controls.Add(txtPcSize);
            currentY += 70;

            Label lblPcOpacity = new Label { Text = "PC Name Opacity (0-255)", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblPcOpacity);
            currentY += 25;
            TextBox txtPcOpacity = CreateModernTextBox(padding, currentY, contentWidth, "180", inputBack, foreColor, regularFont);
            txtPcOpacity.Text = _config.PcNameOpacity.ToString(); txtPcOpacity.Enabled = false;
            container.Controls.Add(txtPcOpacity);
            currentY += 70;

            Label lblPcColor = new Label { Text = "PC Name Color (Hex, e.g. #FFFFFF)", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblPcColor);
            currentY += 25;
            TextBox txtPcColor = CreateModernTextBox(padding, currentY, contentWidth, "#FFFFFF", inputBack, foreColor, regularFont);
            txtPcColor.Text = _config.PcNameColor; txtPcColor.Enabled = false;
            container.Controls.Add(txtPcColor);
            currentY += 70;

            addSectionHeader("UI Font Settings", currentY);
            currentY += 40;

            Label lblStatusSize = new Label { Text = "Insert Coin Font Size", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblStatusSize);
            currentY += 25;
            TextBox txtStatusSize = CreateModernTextBox(padding, currentY, contentWidth, "120", inputBack, foreColor, regularFont);
            txtStatusSize.Text = _config.StatusFontSize.ToString(); txtStatusSize.Enabled = false;
            container.Controls.Add(txtStatusSize);
            currentY += 70;

            Label lblTimerSize = new Label { Text = "Shutdown Timer Font Size", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblTimerSize);
            currentY += 25;
            TextBox txtTimerSize = CreateModernTextBox(padding, currentY, contentWidth, "60", inputBack, foreColor, regularFont);
            txtTimerSize.Text = _config.TimerFontSize.ToString(); txtTimerSize.Enabled = false;
            container.Controls.Add(txtTimerSize);
            currentY += 70;

            Label lblInsertDuration = new Label { Text = "Insert Coins Popup Duration (Seconds)", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblInsertDuration);
            currentY += 25;
            TextBox txtInsertDuration = CreateModernTextBox(padding, currentY, contentWidth, "30", inputBack, foreColor, regularFont);
            txtInsertDuration.Text = _config.InsertCoinsDuration.ToString(); txtInsertDuration.Enabled = false;
            container.Controls.Add(txtInsertDuration);
            currentY += 70;

            addSectionHeader("Standalone Settings", currentY);
            currentY += 40;

            Label lblWall = new Label { Text = "Local Wallpaper Path", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblWall);
            currentY += 25;
            TextBox txtWallpaper = CreateModernTextBox(padding, currentY, contentWidth - 110, "No wallpaper selected", inputBack, foreColor, regularFont);
            txtWallpaper.Text = _config.WallpaperPath; txtWallpaper.Enabled = false;
            container.Controls.Add(txtWallpaper);

            Button btnBrowse = CreateModernButton("Browse", padding + contentWidth - 100, currentY, 100, primaryColor, btnFont);
            btnBrowse.Height = txtWallpaper.Height; btnBrowse.Enabled = false;
            btnBrowse.Click += (s, e) => {
                using (System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" }) {
                    if (ofd.ShowDialog() == DialogResult.OK) txtWallpaper.Text = ofd.FileName;
                }
            };
            container.Controls.Add(btnBrowse);
            currentY += 70;

            Label lblCom = new Label { Text = "Coin Slot COM Port", Location = new Point(padding, currentY), AutoSize = true, Font = regularFont };
            container.Controls.Add(lblCom);
            currentY += 25;
            ComboBox cmbCom = new ComboBox { 
                Location = new Point(padding, currentY), 
                Width = contentWidth, 
                FlatStyle = FlatStyle.Flat, 
                BackColor = inputBack, 
                ForeColor = foreColor, 
                DropDownStyle = ComboBoxStyle.DropDownList, 
                Font = regularFont, 
                Enabled = false 
            };
            cmbCom.Items.Add("None");
            try { cmbCom.Items.AddRange(SerialPort.GetPortNames()); } catch { }
            if (cmbCom.Items.Contains(_config.ComPort)) cmbCom.SelectedItem = _config.ComPort;
            else cmbCom.SelectedIndex = 0;
            container.Controls.Add(cmbCom);
            currentY += 70;

            addSectionHeader("System Actions", currentY);
            currentY += 40;

            Button btnExit = CreateModernButton("Shutdown Application", padding, currentY, contentWidth, Color.FromArgb(220, 38, 38), btnFont);
            container.Controls.Add(btnExit);
            currentY += 60;

            Button btnSave = CreateModernButton("SAVE CHANGES", padding, currentY, contentWidth, primaryColor, new Font("Segoe UI", 11, FontStyle.Bold));
            btnSave.Height = 55;
            btnSave.ForeColor = Color.White;
            btnSave.FlatAppearance.BorderSize = 0;
            container.Controls.Add(btnSave);
            currentY += 100;

            Action updateControlStates = () =>
            {
                bool unlocked = (txtPass.Text == _config.AdminPassword);
                bool isStandalone = rbStandalone.Checked;
                bool isServerLocal = rbServerLocal.Checked;
                bool isServer = rbServer.Checked;

                btnExit.Enabled = unlocked;
                btnSave.Enabled = unlocked;

                rbServer.Enabled = unlocked;
                rbStandalone.Enabled = unlocked;
                rbServerLocal.Enabled = unlocked;
                txtNewPass.Enabled = unlocked;
                txtConfirmPass.Enabled = unlocked;
                txtServerIp.Enabled = unlocked && (isServer || isServerLocal);
                
                bool localCoinEnabled = unlocked && (isStandalone || isServerLocal);
                t1.Enabled = localCoinEnabled;
                t5.Enabled = localCoinEnabled;
                t10.Enabled = localCoinEnabled;
                t20.Enabled = localCoinEnabled;
                txtWallpaper.Enabled = localCoinEnabled;
                btnBrowse.Enabled = localCoinEnabled;
                cmbCom.Enabled = localCoinEnabled;

                cmbAnim.Enabled = unlocked;
                chkTaskMgr.Enabled = unlocked;
                chkTopMost.Enabled = unlocked;
                chkDisableShutdown.Enabled = unlocked;
                chkDisableRun.Enabled = unlocked;
                chkDisableCmd.Enabled = unlocked;
                chkDisableTerminal.Enabled = unlocked;
                chkDisableCAD.Enabled = unlocked;
                chkRunAtStartup.Enabled = unlocked;
                txtIdle.Enabled = unlocked;
                txtRegFee.Enabled = unlocked;

                chkShowPcName.Enabled = unlocked;
                cmbPcPos.Enabled = unlocked;
                cmbPcAnim.Enabled = unlocked;
                txtPcSize.Enabled = unlocked;
                txtPcOpacity.Enabled = unlocked;
                txtPcColor.Enabled = unlocked;
                txtStatusSize.Enabled = unlocked;
                txtTimerSize.Enabled = unlocked;
                txtInsertDuration.Enabled = unlocked;
            };

            txtPass.TextChanged += (s, e) => updateControlStates();
            rbServer.CheckedChanged += (s, e) => updateControlStates();
            rbStandalone.CheckedChanged += (s, e) => updateControlStates();
            rbServerLocal.CheckedChanged += (s, e) => updateControlStates();
            updateControlStates();

            btnExit.Click += (s, e) => 
            {
                if (MessageBox.Show("Are you sure you want to shutdown the application? This will disable the lockscreen.", "Shutdown", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
                {
                    try 
                    { 
                        // Remove flag so watchdog doesn't restart us
                        string flagPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "watchdog_active.tmp");
                        if (File.Exists(flagPath)) File.Delete(flagPath);
                        
                        // Kill watchdog process
                        var watchdogs = Process.GetProcessesByName("PisonetWatchdog");
                        foreach (var w in watchdogs) try { w.Kill(); } catch { }
                    } 
                    catch { }
                    Environment.Exit(0);
                }
            };

            btnSave.Click += async (s, e) =>
            {
                string newPass = txtNewPass.Text.Trim();
                string confirm = txtConfirmPass.Text.Trim();
                if (!string.IsNullOrEmpty(newPass) || !string.IsNullOrEmpty(confirm))
                {
                    if (newPass != confirm)
                    {
                        MessageBox.Show("New password and confirm password do not match.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    _config.SaveAdminPassword(newPass);
                }

                currentDesign = cmbAnim.SelectedIndex;
                _config.SaveAnimation(currentDesign);

                string entered = txtServerIp.Text.Trim();
                if (string.IsNullOrEmpty(entered))
                {
                    _config.SaveServerIp("");
                    await _socketService.DisconnectAsync();
                }
                else
                {
                    entered = EnsureHttpPrefix(entered);
                    if (!string.Equals(_config.ServerIp, entered, StringComparison.OrdinalIgnoreCase))
                    {
                        _config.SaveServerIp(entered);
                        _socketService.UpdateServerUrl(entered);
                        await _socketService.ConnectAsync();
                    }
                }

                if (int.TryParse(t1.Text, out int r1) && int.TryParse(t5.Text, out int r5) &&
                    int.TryParse(t10.Text, out int r10) && int.TryParse(t20.Text, out int r20))
                {
                    _config.SaveRates(r1, r5, r10, r20);
                }

                _config.SaveSecurity(chkTaskMgr.Checked, chkTopMost.Checked, chkDisableShutdown.Checked, chkDisableRun.Checked, chkDisableCmd.Checked, chkDisableTerminal.Checked, chkDisableCAD.Checked, chkRunAtStartup.Checked);
                this.TopMost = _config.AppTopMost;
                ApplySecurityPolicies(isLocked);
                EnsureStartupTask();

                bool wasServer = _config.IsServerMode;
                bool wasLocalCoin = _config.UseLocalCoinslot;
                
                int newMode = 0;
                if (rbStandalone.Checked) newMode = 1;
                else if (rbServerLocal.Checked) newMode = 2;
                
                _config.SaveMode(newMode);
                bool isServer = _config.IsServerMode;
                bool isLocalCoin = _config.UseLocalCoinslot;

                if (int.TryParse(txtIdle.Text, out int idleMins)) _config.SaveIdle(idleMins);
                if (int.TryParse(txtRegFee.Text, out int regFee))
                {
                    await _socketService.SaveShopSettingsAsync(new { registerFee = regFee });
                }
                int pcSize = 24; int.TryParse(txtPcSize.Text, out pcSize);
                int pcOpacity = 180; int.TryParse(txtPcOpacity.Text, out pcOpacity);
                string pcColor = txtPcColor.Text.Trim();
                if (!pcColor.StartsWith("#")) pcColor = "#" + pcColor;
                _config.SavePcNameSettings(chkShowPcName.Checked, cmbPcPos.SelectedIndex, cmbPcAnim.SelectedIndex, pcSize, pcOpacity, pcColor);
                
                int statusSize = 120; int.TryParse(txtStatusSize.Text, out statusSize);
                int timerSize = 60; int.TryParse(txtTimerSize.Text, out timerSize);
                _config.SaveUiSettings(statusSize, timerSize);

                if (int.TryParse(txtInsertDuration.Text, out int insertDuration))
                {
                    _config.SaveInsertCoinsDuration(insertDuration);
                }

                // Update button visibility immediately
                if (btnInsertCoins != null)
                {
                    btnInsertCoins.Visible = (_config.SystemMode == 2);
                }

                // Apply UI changes immediately
                _wpfStatus.FontSize = statusSize;
                _wpfTimer.FontSize = timerSize;
                _wpfPcName.FontSize = pcSize;
                lblStatus.Font = new Font("Impact", statusSize, FontStyle.Bold);
                lblTimerDisplay.Font = new Font("Impact", timerSize, FontStyle.Bold);
                CenterLabels();

                _config.SaveWallpaper(txtWallpaper.Text);
                string oldPort = _config.ComPort;
                _config.SaveComPort(cmbCom.SelectedItem?.ToString() ?? "None");
                ApplyLocalWallpaper();

                if (!isServer && wasServer)
                {
                    await _socketService.DisconnectAsync();
                    lblConnStatus.Text = "Standalone Mode";
                    lblConnStatus.ForeColor = Color.Gray;
                    _wpfConnStatus.Text = "Standalone Mode";
                    _wpfConnStatus.Foreground = System.Windows.Media.Brushes.Gray;
                }
                else if (isServer && (!wasServer || !string.Equals(_config.ServerIp, entered, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!string.IsNullOrWhiteSpace(_config.ServerIp))
                    {
                        _socketService.UpdateServerUrl(_config.ServerIp);
                        await _socketService.ConnectAsync();
                    }
                }

                if (isLocalCoin)
                {
                    if (!wasLocalCoin || oldPort != _config.ComPort)
                    {
                        InitializeSerialPort();
                    }
                }
                else if (wasLocalCoin)
                {
                    _serialPort?.Close();
                    _serialPort?.Dispose();
                    _serialPort = null;
                }

                adminForm.Close();
                Application.Restart();
                Environment.Exit(0);
            };

            return adminForm;
        }

        private TextBox CreateModernTextBox(int x, int y, int w, string ph, Color bg, Color fg, Font f) {
            TextBox tb = new TextBox { 
                Location = new Point(x, y), 
                Width = w, 
                BackColor = bg, 
                ForeColor = fg, 
                Font = f, 
                BorderStyle = BorderStyle.FixedSingle, 
                PlaceholderText = ph 
            };
            tb.Enter += (s, e) => { tb.BackColor = Color.FromArgb(75, 85, 99); };
            tb.Leave += (s, e) => { tb.BackColor = bg; tb.ForeColor = fg; };
            return tb;
        }

        private TextBox CreateRateInput(int x, int y, int w, string lbl, int val, Color bg, Color fg, Font f, Control p) {
            Label l = new Label { 
                Text = lbl, 
                Location = new Point(x, y), 
                AutoSize = true, 
                Font = new Font(f.FontFamily, 9, FontStyle.Bold), 
                ForeColor = Color.FromArgb(156, 163, 175) 
            };
            p.Controls.Add(l);
            TextBox t = new TextBox { 
                Location = new Point(x, y + 22), 
                Width = w, 
                BackColor = bg, 
                ForeColor = fg, 
                Font = f, 
                BorderStyle = BorderStyle.FixedSingle, 
                Text = val.ToString(), 
                Enabled = false, 
                TextAlign = HorizontalAlignment.Center 
            };
            t.Enter += (s, e) => { t.BackColor = Color.FromArgb(75, 85, 99); };
            t.Leave += (s, e) => { t.BackColor = bg; t.ForeColor = fg; };
            p.Controls.Add(t);
            return t;
        }

        private Button CreateModernButton(string t, int x, int y, int w, Color bg, Font f) {
            Button b = new Button { 
                Text = t, 
                Location = new Point(x, y), 
                Width = w, 
                Height = 45, 
                BackColor = bg, 
                ForeColor = Color.White, 
                Font = f, 
                FlatStyle = FlatStyle.Flat, 
                Cursor = Cursors.Hand 
            };
            b.FlatAppearance.BorderSize = 0;
            Color originalColor = bg;
            b.MouseEnter += (s, e) => {
                b.BackColor = ControlPaint.Light(originalColor, 0.2f);
            };
            b.MouseLeave += (s, e) => {
                b.BackColor = originalColor;
            };
            return b;
        }

        private CheckBox CreateModernCheckBox(string text, int x, int y, bool isChecked, Font font, Color primaryColor, Color foreColor, Control container)
        {
            CheckBox chk = new CheckBox
            {
                Location = new Point(x, y),
                Size = new Size(18, 18),
                Checked = isChecked,
                Enabled = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = primaryColor,
                ForeColor = Color.Black,
                TabStop = false
            };
            
            Label lbl = new Label
            {
                Text = text,
                Location = new Point(x + 25, y),
                AutoSize = true,
                Font = font,
                ForeColor = foreColor,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            lbl.Click += (s, e) => { if (chk.Enabled) chk.Checked = !chk.Checked; };
            
            container.Controls.Add(chk);
            container.Controls.Add(lbl);
            
            return chk;
        }

        private byte[] CaptureScreenJpeg(long quality = 80L)
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
                using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    ImageCodecInfo? jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                    if (jpgEncoder == null)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Jpeg);
                            return ms.ToArray();
                        }
                    }

                    EncoderParameters encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, jpgEncoder, encoderParams);
                        return ms.ToArray();
                    }
                }
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (var codec in codecs)
                if (codec.FormatID == format.Guid) return codec;
            return null;
        }

        private void PerformShutdown()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/s /f /t 0",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        
        private void UpdateTimerUI()
        {
            if (remainingSeconds > 0)
            {
                _idleSecondsRemaining = -1;
                TimeSpan t = TimeSpan.FromSeconds(remainingSeconds);
                string s = t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
                
                if (lblTimerDisplay.Text != s)
                {
                    lblTimerDisplay.Text = s;
                    lblTimerDisplay.ForeColor = remainingSeconds <= 60 ? Color.Red : Color.Lime;

                    // Update WPF Timer
                    _wpfTimer.Text = s;
                    _wpfTimer.Foreground = remainingSeconds <= 60 ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Lime;

                    CenterLabels();
                }
            }
            else if (!isLocked)
            {
                if (lblTimerDisplay.Text != "ADMIN")
                {
                    lblTimerDisplay.Text = "ADMIN";
                    lblTimerDisplay.ForeColor = Color.White;
                    CenterLabels();
                }
            }
            else
            {
                if (_config.IdleShutdownMinutes <= 0)
                {
                    if (lblTimerDisplay.Text != "0:00")
                    {
                        lblTimerDisplay.Text = "0:00";
                        lblTimerDisplay.ForeColor = Color.Lime;

                        // Update WPF Timer
                        _wpfTimer.Text = "0:00";
                        _wpfTimer.Foreground = System.Windows.Media.Brushes.Lime;

                        CenterLabels();
                    }
                }
            }
            overlayTimer?.UpdateTime(remainingSeconds);
        }

        private void CenterLabels()
        {
            if (this.ClientSize.Width == 0 || this.ClientSize.Height == 0) return;
            int centerX = this.ClientSize.Width / 2;
            int centerY = this.ClientSize.Height / 2;

            lblStatus.Location = new Point(centerX - (lblStatus.Width / 2), centerY - 150);
            lblTimerDisplay.Location = new Point(centerX - (lblTimerDisplay.Width / 2), lblStatus.Bottom + 10);
            
            if (btnMemberLogin != null && btnInsertCoins != null)
            {
                if (btnMemberLogin.Visible && btnInsertCoins.Visible)
                {
                    int spacing = 20;
                    int totalWidth = btnMemberLogin.Width + spacing + btnInsertCoins.Width;
                    int startX = centerX - (totalWidth / 2);

                    btnMemberLogin.Location = new Point(startX, this.ClientSize.Height - 250);
                    btnInsertCoins.Location = new Point(startX + btnMemberLogin.Width + spacing, this.ClientSize.Height - 250);
                }
                else
                {
                    btnMemberLogin.Location = new Point(centerX - (btnMemberLogin.Width / 2), this.ClientSize.Height - 250);
                    btnInsertCoins.Location = new Point(centerX - (btnInsertCoins.Width / 2), this.ClientSize.Height - 250);
                }
            }
        }

        private void LockSystem()
        {
            isLocked = true;
            this.TopMost = _config.AppTopMost;
            this.Show();
            
            if (!animationTimer.Enabled) animationTimer.Start();
            
            IdleTimer_Tick(null!, null!);
            
            // Show/Hide Insert Coins based on mode
            // Mode 2 is Server with Local Coins
            if (btnInsertCoins != null)
            {
                btnInsertCoins.Visible = (_config.SystemMode == 2);
            }

            CenterLabels();
            overlayTimer?.Hide();
            overlayTimer?.SetUser("Guest", 0, 0);
            
            ApplySecurityPolicies(true);

            // Ensure UI elements are on top once when locking
            SafeInvoke(() => {
                if (btnMemberLogin != null)
                {
                    btnMemberLogin.Visible = true;
                    btnMemberLogin.BringToFront();
                }
                if (btnInsertCoins != null && btnInsertCoins.Visible)
                {
                    btnInsertCoins.BringToFront();
                }
                // WinForms labels are hidden in AnimationTimer_Tick to avoid transparency issues
                
                _wpfStatus.Visibility = System.Windows.Visibility.Visible;
                _wpfTimer.Visibility = System.Windows.Visibility.Visible;
                _wpfConnStatus.Visibility = System.Windows.Visibility.Visible;
            });

            _audio.SetMute(true);
            _ = SendHardwareStatus();
        }

        private void UnlockSystem()
        {
            isLocked = false;
            _idleSecondsRemaining = -1;
            
            if (animationTimer.Enabled) animationTimer.Stop();
            
            this.Hide();

            // Ensure overlay timer exists and is shown
            if (overlayTimer == null || overlayTimer.IsDisposed)
            {
                overlayTimer = new TimerOverlayForm(_socketService);
                overlayTimer.RegisterRequested += (s, e) => ShowMemberRegister(); 
                overlayTimer.LogoutRequested += async (s, e) =>
                {
                    if (MessageBox.Show("Are you sure you want to logout?", "Logout", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                    {
                        if (_config.IsServerMode)
                        {
                            await _socketService.LogoutAsync(currentUsername, _hardware.GetMacAddress());
                        }
                        remainingSeconds = 0;
                        currentUsername = "Guest";
                        currentUserRole = "Guest";
                        currentUserPoints = 0;
                        currentUserNextBonus = 0;
                        LockSystem();
                    }
                };
                overlayTimer.VoucherSubmitted += async (code) =>
                {
                    if (_config.IsServerMode)
                    {
                        await _socketService.UseVoucherAsync(code, _hardware.GetMacAddress());
                    }
                };
            }

            overlayTimer.SetUser(currentUsername, currentUserPoints, currentUserNextBonus);
            overlayTimer.Show();
            
            ApplySecurityPolicies(false);

            if (btnMemberLogin != null) btnMemberLogin.Visible = false;
            if (btnInsertCoins != null) btnInsertCoins.Visible = false;
            
            _wpfStatus.Visibility = System.Windows.Visibility.Collapsed;
            _wpfTimer.Visibility = System.Windows.Visibility.Collapsed;
            _wpfConnStatus.Visibility = System.Windows.Visibility.Collapsed;

            _audio.SetMute(false);
            _ = SendHardwareStatus();
        }

        private void ApplySecurityPolicies(bool lockActive)
        {
            // Run registry changes in background to avoid UI lag, 
            // but NotifyRegistryUpdate is the main bottleneck.
            Task.Run(() =>
            {
                try
                {
                    ToggleTaskManager(!_config.AllowTaskManager);
                    ToggleRunRestriction(_config.DisableRun);
                    ToggleCmdRestriction(_config.DisableCmd);
                    ToggleCADOptions(_config.DisableCADOptions);
                    UpdateDisallowRun();
                    
                    // Only notify once after all changes
                    NotifyRegistryUpdate();
                }
                catch (Exception ex)
                {
                    LogLocalError($"ApplySecurityPolicies Error: {ex.Message}");
                }
            });
        }

        private static void ToggleTaskManager(bool disable)
        {
            if (OperatingSystem.IsWindows())
            {
                try 
                { 
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System")) 
                    { 
                        if (disable) k.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                        else k.DeleteValue("DisableTaskMgr", false); 
                    } 
                } 
                catch { }
            }
        }

        private static void ToggleRunRestriction(bool disable)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                    {
                        if (disable) k.SetValue("NoRun", 1, RegistryValueKind.DWord);
                        else k.DeleteValue("NoRun", false);
                    }
                }
                catch { }
            }
        }

        private static void ToggleCmdRestriction(bool disable)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\System"))
                    {
                        if (disable) k.SetValue("DisableCMD", 2, RegistryValueKind.DWord); // 2 blocks batch files too
                        else k.DeleteValue("DisableCMD", false);
                    }
                }
                catch { }
            }
        }

        private static void ToggleCADOptions(bool disable)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // Explorer Policies (Logoff)
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                    {
                        if (disable) k.SetValue("NoLogoff", 1, RegistryValueKind.DWord);
                        else k.DeleteValue("NoLogoff", false);
                    }

                    // System Policies (Lock, Change Password)
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                    {
                        if (disable)
                        {
                            k.SetValue("DisableLock", 1, RegistryValueKind.DWord);
                            k.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord);
                            k.SetValue("DisableChangePassword", 1, RegistryValueKind.DWord);
                        }
                        else
                        {
                            k.DeleteValue("DisableLock", false);
                            k.DeleteValue("DisableLockWorkstation", false);
                            k.DeleteValue("DisableChangePassword", false);
                        }
                    }

                    // Fast User Switching (Switch User) - Requires Admin, skip if fails
                    try
                    {
                        using (RegistryKey k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                        {
                            if (disable) k.SetValue("HideFastUserSwitching", 1, RegistryValueKind.DWord);
                            else k.DeleteValue("HideFastUserSwitching", false);
                        }
                    }
                    catch { /* Skip if no admin rights */ }
                }
                catch { }
            }
        }

        private static void ToggleTerminalRestriction(bool disable)
        {
            // Handled by UpdateDisallowRun
        }

        private static void NotifyRegistryUpdate()
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    NativeMethods.SendMessageTimeout(new IntPtr(0xffff), NativeMethods.WM_SETTINGCHANGE, IntPtr.Zero, "Policy", NativeMethods.SMTO_ABORTIFHUNG, 5000, out _);
                }
                catch { }
            }
        }

        private static void UpdateDisallowRun()
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    bool shouldDisallow = _config.DisableCmd || _config.DisableTerminal;
                    if (shouldDisallow)
                    {
                        k.SetValue("DisallowRun", 1, RegistryValueKind.DWord);
                        using (RegistryKey sub = k.CreateSubKey("DisallowRun"))
                        {
                            // Clear existing values first to avoid leftovers
                            foreach (string valName in sub.GetValueNames()) sub.DeleteValue(valName);

                            int i = 1;
                            if (_config.DisableCmd) {
                                sub.SetValue((i++).ToString(), "cmd.exe");
                            }
                            if (_config.DisableTerminal) {
                                sub.SetValue((i++).ToString(), "wt.exe");
                                sub.SetValue((i++).ToString(), "WindowsTerminal.exe");
                            }
                            // Always block powershell if either is disabled as it's a common bypass
                            if (_config.DisableCmd || _config.DisableTerminal) {
                                sub.SetValue((i++).ToString(), "powershell.exe");
                                sub.SetValue((i++).ToString(), "powershell_ise.exe");
                                sub.SetValue((i++).ToString(), "pwsh.exe");
                            }
                        }
                    }
                    else
                    {
                        k.SetValue("DisallowRun", 0, RegistryValueKind.DWord);
                        try { k.DeleteSubKeyTree("DisallowRun", false); } catch { }
                    }
                }
            }
            catch { }
        }

        private static IntPtr HookCallback(int n, IntPtr w, IntPtr l)
        {
            if (n >= 0)
            {
                int vk = Marshal.ReadInt32(l);
                
                // Track Escape key state (VK_ESCAPE = 0x1B)
                if (w == (IntPtr)0x0100 || w == (IntPtr)0x0104)
                {
                    if (vk == 0x1B) _isEscDown = true;
                }
                else if (w == (IntPtr)0x0101 || w == (IntPtr)0x0105)
                {
                    if (vk == 0x1B) _isEscDown = false;
                }

                if (w == (IntPtr)0x0100 || w == (IntPtr)0x0104)
                {
                    // Admin Panel Hotkey: Esc + F12 (VK_F12 = 0x7B)
                    bool escIsDown = _isEscDown || (GetAsyncKeyState(0x1B) & 0x8000) != 0;
                    if (vk == 0x7B && escIsDown)
                    {
                        if (_instance != null && !_instance.IsDisposed)
                        {
                            // Use BeginInvoke to ensure the hook returns immediately
                            _instance.BeginInvoke(new Action(() => _instance.ShowAdminPanel()));
                            return (IntPtr)1;
                        }
                    }

                    bool ctrlPressed = (GetAsyncKeyState(0x11) & 0x8000) != 0; // VK_CONTROL
                    bool shiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0; // VK_SHIFT
                    bool lWinPressed = (GetAsyncKeyState(0x5B) & 0x8000) != 0; // VK_LWIN
                    bool rWinPressed = (GetAsyncKeyState(0x5C) & 0x8000) != 0; // VK_RWIN
                    bool winPressed = lWinPressed || rWinPressed;

                    if (winPressed)
                    {
                        if (vk == (int)Keys.R && _config.DisableRun) return (IntPtr)1;
                        if (vk == (int)Keys.X && (_config.DisableCmd || _config.DisableTerminal)) return (IntPtr)1;
                        if (vk == (int)Keys.Enter && _config.DisableTerminal) return (IntPtr)1;
                        if (isLocked) return (IntPtr)1;
                    }

                    if (vk == 0x1B && ctrlPressed && shiftPressed) // Esc
                    {
                        if (isLocked) return (IntPtr)1;
                        if (!_config.AllowTaskManager)
                        {
                            if (_instance != null && !_instance.IsDisposed)
                            {
                                _instance.BeginInvoke(new Action(() => _instance.PromptForTaskManager()));
                            }
                            return (IntPtr)1;
                        }
                        return NativeMethods.CallNextHookEx(_hookID, n, w, l);
                    }

                    if (isLocked)
                    {
                        if (w == (IntPtr)0x0104 && (vk == (int)Keys.Tab || vk == 0x1B)) return (IntPtr)1;
                        if (ctrlPressed && vk == 0x1B) return (IntPtr)1;
                        if (vk == 0x1B) return (IntPtr)1;
                    }
                }

                if (!isLocked || isAdminPromptOpen || isMemberLoginOpen)
                    return NativeMethods.CallNextHookEx(_hookID, n, w, l);

                return (IntPtr)1;
            }
            return NativeMethods.CallNextHookEx(_hookID, n, w, l);
        }

        public void PromptForTaskManager()
        {
            using (Form prompt = new Form())
            {
                prompt.Width = 350;
                prompt.Height = 180;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MinimizeBox = false;
                prompt.MaximizeBox = false;
                prompt.Text = "Task Manager Access";
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.TopMost = true;
                prompt.BackColor = Color.White;

                Label textLabel = new Label() { Left = 20, Top = 20, Text = "Task Manager is restricted.\nEnter Admin Password to open:", AutoSize = true, Font = new Font("Segoe UI", 10) };
                TextBox textBox = new TextBox() { Left = 20, Top = 55, Width = 290, PasswordChar = '•', Font = new Font("Segoe UI", 10) };
                Button confirmation = new Button() { 
                    Text = "Open Task Manager", 
                    Left = 160, Width = 150, Top = 95, Height = 35,
                    DialogResult = DialogResult.OK, 
                    BackColor = Color.FromArgb(79, 70, 229), 
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                };
                confirmation.FlatAppearance.BorderSize = 0;
                
                prompt.Controls.Add(textBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(textLabel);
                prompt.AcceptButton = confirmation;

                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    if (textBox.Text == _config.AdminPassword)
                    {
                        Task.Run(() => {
                            ToggleTaskManager(false);
                            try 
                            { 
                                Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); 
                            } 
                            catch { }
                            Task.Delay(3000).Wait();
                            ToggleTaskManager(true);
                        });
                    }
                    else
                    {
                        MessageBox.Show("Invalid Password", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }


        private void SetHook()
        {
            using (Process p = Process.GetCurrentProcess())
            {
                ProcessModule? m = p.MainModule;
                if (m != null)
                {
                    _hookID = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(m.ModuleName), 0);
                }
            }
        }

        private void InitializeWpfWallpaper()
        {
            _wpfHost = new ElementHost { Dock = DockStyle.Fill };
            
            var grid = new System.Windows.Controls.Grid();
            grid.Background = System.Windows.Media.Brushes.Black; // Fallback background
            
            _videoPlayer = new System.Windows.Controls.MediaElement
            {
                LoadedBehavior = System.Windows.Controls.MediaState.Manual,
                UnloadedBehavior = System.Windows.Controls.MediaState.Stop,
                Stretch = System.Windows.Media.Stretch.UniformToFill,
                IsMuted = true, // Background wallpaper usually muted
                Visibility = System.Windows.Visibility.Collapsed
            };
            
            // Loop video
            _videoPlayer.MediaEnded += (s, e) => {
                SafeInvoke(() => {
                    _videoPlayer.Position = TimeSpan.FromMilliseconds(1);
                    _videoPlayer.Play();
                });
            };

            _videoPlayer.MediaOpened += (s, e) => {
                SafeInvoke(() => _videoPlayer.Play());
            };

            _videoPlayer.MediaFailed += (s, e) => {
                LogLocalError("WPF MediaElement Error: " + e.ErrorException?.Message);
                SafeInvoke(() => {
                    _videoPlayer.Visibility = System.Windows.Visibility.Collapsed;
                });
            };

            _imageBackground = new System.Windows.Controls.Image
            {
                Stretch = System.Windows.Media.Stretch.UniformToFill,
                Visibility = System.Windows.Visibility.Collapsed
            };

            grid.Children.Add(_imageBackground);
            grid.Children.Add(_videoPlayer);

            // Add WPF TextBlocks for transparent text over wallpaper
            _wpfStatus = new System.Windows.Controls.TextBlock
            {
                Text = "PLEASE INSERT COIN",
                FontSize = _config.StatusFontSize,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Cyan,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                TextAlignment = System.Windows.TextAlignment.Center,
                Visibility = System.Windows.Visibility.Collapsed
            };

            _wpfTimer = new System.Windows.Controls.TextBlock
            {
                Text = "0:00",
                FontSize = _config.TimerFontSize,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                TextAlignment = System.Windows.TextAlignment.Center,
                Visibility = System.Windows.Visibility.Collapsed,
                Margin = new System.Windows.Thickness(0, 250, 0, 0) // Position below status
            };

            _wpfConnStatus = new System.Windows.Controls.TextBlock
            {
                Text = "Connecting...",
                FontSize = 18,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Cyan,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new System.Windows.Thickness(20, 20, 0, 0),
                Visibility = System.Windows.Visibility.Visible
            };

            _wpfPcName = new System.Windows.Controls.TextBlock
            {
                Text = pcName,
                FontSize = _config.PcNameFontSize,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)_config.PcNameOpacity, 0, 255, 255)),
                Visibility = System.Windows.Visibility.Collapsed
            };

            _wpfAnnouncement = new System.Windows.Controls.TextBlock
            {
                Text = "",
                FontSize = 28,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Yellow,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Visibility = System.Windows.Visibility.Collapsed
            };

            grid.Children.Add(_wpfStatus);
            grid.Children.Add(_wpfTimer);
            grid.Children.Add(_wpfConnStatus);
            grid.Children.Add(_wpfPcName);
            grid.Children.Add(_wpfAnnouncement);
            
            _wpfHost.Child = grid;
            this.Controls.Add(_wpfHost);
            _wpfHost.SendToBack();
        }

        private void LoadShopColor()
        {
            if (File.Exists(_colorConfigPath))
            {
                try { _shopNameColor = ColorTranslator.FromHtml(File.ReadAllText(_colorConfigPath)); } catch { }
            }
        }

        private void HandleWallpaperUpdate(string data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data)) return;

                // Check if it's a Data URI or Base64 (Uri too long error fix)
                if (data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    int commaIndex = data.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        string header = data.Substring(0, commaIndex);
                        string base64Data = data.Substring(commaIndex + 1);
                        byte[] bytes = Convert.FromBase64String(base64Data);

                        if (header.Contains("video/"))
                        {
                            // Save to temp file because MediaElement cannot play from stream
                            string ext = header.Contains("video/mp4") ? ".mp4" : 
                                         header.Contains("video/webm") ? ".webm" : 
                                         header.Contains("video/quicktime") ? ".mov" : ".avi";
                            
                            // Use a unique name to avoid "file in use" errors
                            string tempFile = Path.Combine(Path.GetTempPath(), $"v_wall_{DateTime.Now.Ticks}{ext}");
                            
                            // Cleanup old temp files starting with v_wall_
                            try {
                                foreach (var oldFile in Directory.GetFiles(Path.GetTempPath(), "v_wall_*")) {
                                    try { File.Delete(oldFile); } catch { }
                                }
                            } catch { }

                            File.WriteAllBytes(tempFile, bytes);
                            PlayVideoWallpaper(tempFile);
                        }
                        else if (header.Contains("image/"))
                        {
                            LoadImageWallpaper(bytes);
                        }
                        return;
                    }
                }

                // Normal URL or File Path logic
                string fullUrl = data;
                bool isLocalFile = File.Exists(data);

                if (!isLocalFile && !data.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !data.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    string baseUrl = EnsureHttpPrefix(_config.ServerIp);
                    fullUrl = baseUrl.TrimEnd('/') + "/" + data.TrimStart('/');
                }

                string extUrl = Path.GetExtension(data).ToLower();
                bool isVideo = extUrl == ".mp4" || extUrl == ".webm" || extUrl == ".mov" || extUrl == ".wmv" || extUrl == ".avi";

                if (isVideo)
                {
                    PlayVideoWallpaper(fullUrl);
                }
                else
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(fullUrl);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        _imageBackground.Source = bitmap;
                        _imageBackground.Visibility = System.Windows.Visibility.Visible;
                        _videoPlayer.Visibility = System.Windows.Visibility.Collapsed;
                        _videoPlayer.Stop();
                        this.BackgroundImage = null;
                    }
                    catch (Exception ex)
                    {
                        LogLocalError("Image Wallpaper Load Error: " + ex.Message);
                        // Fallback: Hide both to show Grid background (Black)
                        _imageBackground.Visibility = System.Windows.Visibility.Collapsed;
                        _videoPlayer.Visibility = System.Windows.Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                LogLocalError("Wallpaper Update Error: " + ex.Message);
            }
        }

        private void EnsureWatchdogRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName("PisonetWatchdog");
                if (processes.Length == 0)
                {
                    string watchdogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PisonetWatchdog.exe");
                    if (File.Exists(watchdogPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = watchdogPath,
                            UseShellExecute = true,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        });
                    }
                }
            }
            catch { }
        }

        private void PlayVideoWallpaper(string source)
        {
            try
            {
                _videoPlayer.Stop();
                _videoPlayer.Source = new Uri(source);
                _videoPlayer.Position = TimeSpan.Zero;
                _videoPlayer.Play();
                _videoPlayer.Visibility = System.Windows.Visibility.Visible;
                _imageBackground.Visibility = System.Windows.Visibility.Collapsed;
                this.BackgroundImage = null;
            }
            catch (Exception ex)
            {
                LogLocalError("PlayVideoWallpaper Error: " + ex.Message);
                _videoPlayer.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void LoadImageWallpaper(byte[] bytes)
        {
            try
            {
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    _imageBackground.Source = bitmap;
                    _imageBackground.Visibility = System.Windows.Visibility.Visible;
                    _videoPlayer.Visibility = System.Windows.Visibility.Collapsed;
                    _videoPlayer.Stop();
                    this.BackgroundImage = null;
                }
            }
            catch (Exception ex)
            {
                LogLocalError("LoadImageWallpaper Error: " + ex.Message);
                _imageBackground.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void EnsureStartupTask()
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;

                // For non-admin apps, the Registry Run key is a fallback.
                // The primary startup method is now the Scheduled Task created by the installer.
                try
                {
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            if (_config.RunAtStartup)
                                key.SetValue("PisonetLockscreen", $"\"{exePath}\"");
                            else
                                key.DeleteValue("PisonetLockscreen", false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogLocalError($"Registry Startup Error: {ex.Message}");
                }

                // Clean up old startup shortcuts
                string[] startupFolders = {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup)
                };

                foreach (string folder in startupFolders)
                {
                    string shortcutPath = Path.Combine(folder, "Pisonet Lockscreen.lnk");
                    if (File.Exists(shortcutPath))
                    {
                        try { File.Delete(shortcutPath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                LogLocalError($"EnsureStartupTask Error: {ex.Message}");
            }
        }

        private void ShowInsertCoinsPopup()
        {
            isInsertCoinsOpen = true;
            using (InsertCoinsPopupForm popup = new InsertCoinsPopupForm(_config.InsertCoinsDuration))
            {
                popup.ShowDialog(this);
            }
            isInsertCoinsOpen = false;
        }

        public static Color ColorFromHSL(int h, double s, double l) 
        { 
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s; 
            double p = 2 * l - q; 
            Func<double, double, double, double> f = (p1, q1, t) => 
            { 
                if (t < 0) t += 1; 
                if (t > 1) t -= 1; 
                if (t < 1.0 / 6) return p1 + (q1 - p1) * 6 * t; 
                if (t < 1.0 / 2) return q1; 
                if (t < 2.0 / 3) return p1 + (q1 - p1) * (2.0 / 3 - t) * 6; 
                return p1; 
            }; 
            return Color.FromArgb((int)(f(p, q, h / 360.0 + 1.0 / 3) * 255), (int)(f(p, q, h / 360.0) * 255), (int)(f(p, q, h / 360.0 - 1.0 / 3) * 255)); 
        }

        private async Task InitializeNetwork()
        {
            _socketService.OnLoginSuccess += (timeLeftSeconds, username, role, points, nextBonus, isVip) =>
            {
                SafeInvoke(() =>
                {
                    MemberLoginForm? loginForm = null;
                    foreach (Form f in Application.OpenForms) { if (f is MemberLoginForm mlf) { loginForm = mlf; break; } }
                    loginForm?.Close();

                    if (!string.IsNullOrEmpty(username))
                    {
                        if (!_userSuccessCounts.ContainsKey(username)) _userSuccessCounts[username] = 0;
                        _userSuccessCounts[username]++;

                        if (_userSuccessCounts[username] >= 4)
                        {
                            _userLockouts[username] = DateTime.Now.AddMinutes(5);
                            _userSuccessCounts[username] = 0;
                        }
                    }

                    currentUsername = username;
                    currentUserRole = role;
                    currentUserPoints = points;
                    currentUserNextBonus = nextBonus;
                    overlayTimer?.SetVipStatus(isVip);

                    if (timeLeftSeconds > 0)
                    {
                        remainingSeconds = timeLeftSeconds;
                        UpdateTimerUI();
                        UnlockSystem();
                    }
                    else
                    {
                        UnlockSystem();
                    }
                    _ = SendHardwareStatus();
                });
            };

            _socketService.OnLoginFailed += (reason) =>
            {
                SafeInvoke(() =>
                {
                    MemberLoginForm? loginForm = null;
                    foreach (Form f in Application.OpenForms) { if (f is MemberLoginForm mlf) { loginForm = mlf; break; } }
                    loginForm?.SetLoading(false); 
                    MessageBox.Show($"Login Failed: {reason}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            };

            _socketService.OnOperationError += (reason) =>
            {
                SafeInvoke(() =>
                {
                    MemberLoginForm? loginForm = null;
                    foreach (Form f in Application.OpenForms) { if (f is MemberLoginForm mlf) { loginForm = mlf; break; } }
                    loginForm?.SetLoading(false); 
                    MessageBox.Show(reason, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            };

            _socketService.OnOperationSuccess += (msg) =>
            {
                SafeInvoke(() =>
                {
                    MemberLoginForm? loginForm = null;
                    foreach (Form f in Application.OpenForms) { if (f is MemberLoginForm mlf) { loginForm = mlf; break; } }
                    loginForm?.Close(); 
                    MessageBox.Show(msg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            };

            _socketService.OnTimeUpdate += (seconds, username, points, nextBonus, isVip) =>
            {
                SafeInvoke(() =>
                {
                    remainingSeconds = seconds;
                    currentUsername = username;
                    currentUserPoints = points;
                    currentUserNextBonus = nextBonus;
                    UpdateTimerUI();
                    
                    if (!isLocked)
                    {
                        overlayTimer?.SetVipStatus(isVip);
                        overlayTimer?.SetUser(currentUsername, currentUserPoints, currentUserNextBonus);
                    }

                    if (remainingSeconds > 0 && isLocked)
                    {
                        UnlockSystem();
                    }
                    else if (remainingSeconds <= 0 && !isLocked)
                    {
                        LockSystem();
                    }
                });
            };

            _socketService.OnPointsUpdate += (points, nextBonus) =>
            {
                SafeInvoke(() =>
                {
                    currentUserPoints = points;
                    currentUserNextBonus = nextBonus;
                    if (!isLocked)
                    {
                        overlayTimer?.UpdatePoints(currentUserPoints, currentUserNextBonus);
                    }
                });
            };

            _socketService.OnRegisterSuccess += (msg) =>
            {
                SafeInvoke(() =>
                {
                    MemberRegisterForm? regForm = null;
                    foreach (Form f in Application.OpenForms) { if (f is MemberRegisterForm mrf) { regForm = mrf; break; } }
                    regForm?.Close(); 
                    MessageBox.Show(msg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            };

            _socketService.OnRegisterFailed += (reason) =>
            {
                SafeInvoke(() =>
                {
                    MemberRegisterForm? regForm = null;
                    foreach (Form f in Application.OpenForms) { if (f is MemberRegisterForm mrf) { regForm = mrf; break; } }
                    regForm?.SetLoading(false); 
                    MessageBox.Show(reason, "Registration Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            };

            _socketService.OnRemoteResume += (timeLeft, username, points, nextBonus, isVip) => {
                SafeInvoke(() => {
                    _wasActiveBeforeDisconnect = false;
                    if (timeLeft > 0)
                    {
                        remainingSeconds = timeLeft;
                        currentUsername = username;
                        currentUserPoints = points;
                        currentUserNextBonus = nextBonus;
                        overlayTimer?.SetVipStatus(isVip);
                        UpdateTimerUI();
                        UnlockSystem();
                    }
                    else if (timeLeft == 0)
                    {
                        remainingSeconds = 0;
                        currentUsername = username;
                        currentUserPoints = points;
                        currentUserNextBonus = nextBonus;
                        overlayTimer?.SetVipStatus(isVip);
                        LockSystem();
                    }
                    else // timeLeft < 0, admin resume
                    {
                        UnlockSystem();
                    }
                });
            };

            try
            {
                await _socketService.ConnectAsync();
            }
            catch (Exception ex)
            {
                LogLocalError($"Failed to connect to socket server: {ex.Message}");
            }
        }
    }
}
