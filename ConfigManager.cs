using System;
using System.IO;

namespace PisonetLockscreenApp.Services
{
    public class ConfigManager
    {
        private readonly string _dataFolder;
        private readonly string _serverIpPath;
        private readonly string _pcIdPath;
        private readonly string _adminPassPath;
        private readonly string _ratesPath;
        private readonly string _securityPath;
        private readonly string _idlePath;
        private readonly string _wallpaperPath;
        private readonly string _modePath;
        private readonly string _comPortPath;
        private readonly string _animationPath;
        private readonly string _pcNameSettingsPath;
        private readonly string _uiSettingsPath;
        private readonly string _insertCoinsDurationPath;

        public string AdminPassword { get; set; } = "admin123";
        public string PcId { get; private set; } = "";
        public string ServerIp { get; set; } = "";
        public int Rate1 { get; private set; } = 4;
        public int Rate5 { get; private set; } = 20;
        public int Rate10 { get; private set; } = 40;
        public int Rate20 { get; private set; } = 80;
        public bool AllowTaskManager { get; private set; } = false;
        public bool AppTopMost { get; private set; } = true;
        public bool DisableShutdown { get; private set; } = false;
        public bool DisableRun { get; private set; } = false;
        public bool DisableCmd { get; private set; } = false;
        public bool DisableTerminal { get; private set; } = false;
        public bool DisableCADOptions { get; private set; } = false;
        public bool RunAtStartup { get; private set; } = true;
        public int IdleShutdownMinutes { get; private set; } = 0;
        public string WallpaperPath { get; private set; } = "";
        public int SystemMode { get; private set; } = 0; // 0: Server, 1: Standalone, 2: Server with Local Coinslot
        public bool IsServerMode => SystemMode == 0 || SystemMode == 2;
        public bool UseLocalCoinslot => SystemMode == 1 || SystemMode == 2;
        public string ComPort { get; private set; } = "None";
        public int AnimationDesign { get; private set; } = 0;
        public bool ShowPcName { get; private set; } = true;
        public int PcNamePosition { get; private set; } = 2; // 0: TL, 1: TR, 2: BL, 3: BR
        public int PcNameAnimation { get; private set; } = 0;
        public int PcNameFontSize { get; private set; } = 24;
        public int PcNameOpacity { get; private set; } = 180;
        public string PcNameColor { get; private set; } = "#FFFFFF";
        public int StatusFontSize { get; private set; } = 120;
        public int TimerFontSize { get; private set; } = 60;
        public int InsertCoinsDuration { get; private set; } = 30;

        public ConfigManager()
        {
            _dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PisonetLockscreenApp");
            _serverIpPath = Path.Combine(_dataFolder, "server.txt");
            _pcIdPath = Path.Combine(_dataFolder, "pcid.txt");
            _adminPassPath = Path.Combine(_dataFolder, "adminpass.txt");
            _ratesPath = Path.Combine(_dataFolder, "rates.txt");
            _securityPath = Path.Combine(_dataFolder, "security.txt");
            _idlePath = Path.Combine(_dataFolder, "idle.txt");
            _wallpaperPath = Path.Combine(_dataFolder, "wallpaper.txt");
            _modePath = Path.Combine(_dataFolder, "mode.txt");
            _comPortPath = Path.Combine(_dataFolder, "comport.txt");
            _animationPath = Path.Combine(_dataFolder, "animation.txt");
            _pcNameSettingsPath = Path.Combine(_dataFolder, "pcnamesettings.txt");
            _uiSettingsPath = Path.Combine(_dataFolder, "uisettings.txt");
            _insertCoinsDurationPath = Path.Combine(_dataFolder, "insertcoinsduration.txt");

            EnsureDirectory();
        }

        private void EnsureDirectory()
        {
            try { Directory.CreateDirectory(_dataFolder); } catch { }
        }

        public void LoadAll()
        {
            LoadPcId();
            LoadAdminPassword();
            LoadServerIp();
            LoadRates();
            LoadSecurity();
            LoadIdle();
            LoadWallpaper();
            LoadMode();
            LoadComPort();
            LoadAnimation();
            LoadPcNameSettings();
            LoadUiSettings();
            LoadInsertCoinsDuration();
        }

        public void SaveServerIp(string ip)
        {
            ServerIp = ip;
            try
            {
                if (string.IsNullOrEmpty(ip) && File.Exists(_serverIpPath)) File.Delete(_serverIpPath);
                else File.WriteAllText(_serverIpPath, ip);
            }
            catch { }
        }

        public void SaveAdminPassword(string pass)
        {
            AdminPassword = pass;
            try { File.WriteAllText(_adminPassPath, pass); } catch { }
        }

        public void SaveRates(int r1, int r5, int r10, int r20)
        {
            Rate1 = r1; Rate5 = r5; Rate10 = r10; Rate20 = r20;
            try
            {
                using (StreamWriter writer = new StreamWriter(_ratesPath, false))
                {
                    writer.WriteLine($"1={r1}");
                    writer.WriteLine($"5={r5}");
                    writer.WriteLine($"10={r10}");
                    writer.WriteLine($"20={r20}");
                }
            }
            catch { }
        }

        public void SaveSecurity(bool taskMgr, bool topMost, bool disableShutdown, bool disableRun, bool disableCmd, bool disableTerminal, bool disableCAD, bool runAtStartup)
        {
            AllowTaskManager = taskMgr;
            AppTopMost = topMost;
            DisableShutdown = disableShutdown;
            DisableRun = disableRun;
            DisableCmd = disableCmd;
            DisableTerminal = disableTerminal;
            DisableCADOptions = disableCAD;
            RunAtStartup = runAtStartup;
            try
            {
                File.WriteAllText(_securityPath, $"{taskMgr}|{topMost}|{disableShutdown}|{disableRun}|{disableCmd}|{disableTerminal}|{disableCAD}|{runAtStartup}");
            }
            catch { }
        }

        public void SaveIdle(int minutes)
        {
            IdleShutdownMinutes = minutes;
            try { File.WriteAllText(_idlePath, minutes.ToString()); } catch { }
        }

        public void SaveWallpaper(string path)
        {
            WallpaperPath = path;
            try { File.WriteAllText(_wallpaperPath, path); } catch { }
        }

        public void SaveMode(int mode)
        {
            SystemMode = mode;
            try { File.WriteAllText(_modePath, mode.ToString()); } catch { }
        }

        public void SaveComPort(string port)
        {
            ComPort = port;
            try { File.WriteAllText(_comPortPath, port); } catch { }
        }

        public void SaveAnimation(int design)
        {
            AnimationDesign = design;
            try { File.WriteAllText(_animationPath, design.ToString()); } catch { }
        }

        public void SavePcNameSettings(bool show, int position, int animation, int fontSize, int opacity, string color)
        {
            ShowPcName = show;
            PcNamePosition = position;
            PcNameAnimation = animation;
            PcNameFontSize = fontSize;
            PcNameOpacity = opacity;
            PcNameColor = color;
            try
            {
                File.WriteAllText(_pcNameSettingsPath, $"{show}|{position}|{animation}|{fontSize}|{opacity}|{color}");
            }
            catch { }
        }

        public void SaveUiSettings(int statusSize, int timerSize)
        {
            StatusFontSize = statusSize;
            TimerFontSize = timerSize;
            try
            {
                File.WriteAllText(_uiSettingsPath, $"{statusSize}|{timerSize}");
            }
            catch { }
        }

        public void SaveInsertCoinsDuration(int seconds)
        {
            InsertCoinsDuration = seconds;
            try { File.WriteAllText(_insertCoinsDurationPath, seconds.ToString()); } catch { }
        }

        private void LoadAdminPassword()
        {
            try { if (File.Exists(_adminPassPath)) AdminPassword = File.ReadAllText(_adminPassPath).Trim(); } catch { }
        }

        private void LoadServerIp()
        {
            try 
            { 
                if (File.Exists(_serverIpPath)) 
                {
                    ServerIp = File.ReadAllText(_serverIpPath).Trim();
                    // Use 127.0.0.1 instead of localhost for better compatibility with some network stacks
                    if (ServerIp.Contains("localhost")) 
                    {
                        ServerIp = ServerIp.Replace("localhost", "127.0.0.1");
                    }
                }
            } catch { }
        }

        private void LoadPcId()
        {
            try
            {
                if (File.Exists(_pcIdPath))
                {
                    PcId = File.ReadAllText(_pcIdPath).Trim();
                }

                if (string.IsNullOrEmpty(PcId))
                {
                    PcId = Guid.NewGuid().ToString();
                    File.WriteAllText(_pcIdPath, PcId);
                }
            }
            catch
            {
                if (string.IsNullOrEmpty(PcId)) PcId = Guid.NewGuid().ToString(); // In-memory fallback
            }
        }

        private void LoadRates()
        {
            try
            {
                if (!File.Exists(_ratesPath)) return;
                var lines = File.ReadAllLines(_ratesPath);
                int Parse(string s, int def) => int.TryParse(s.Split('=')[^1].Trim(), out int v) ? v : def;
                
                if (lines.Length > 0) Rate1 = Parse(lines[0], 4);
                if (lines.Length > 1) Rate5 = Parse(lines[1], 20);
                if (lines.Length > 2) Rate10 = Parse(lines[2], 40);
                if (lines.Length > 3) Rate20 = Parse(lines[3], 80);
            }
            catch { }
        }

        private void LoadSecurity()
        {
            try
            {
                if (File.Exists(_securityPath))
                {
                    var parts = File.ReadAllText(_securityPath).Split('|');
                    if (parts.Length >= 2) 
                    { 
                        AllowTaskManager = bool.Parse(parts[0]); 
                        AppTopMost = bool.Parse(parts[1]); 
                        if (parts.Length >= 3) DisableShutdown = bool.Parse(parts[2]);
                        if (parts.Length >= 4) DisableRun = bool.Parse(parts[3]);
                        if (parts.Length >= 5) DisableCmd = bool.Parse(parts[4]);
                        if (parts.Length >= 6) DisableTerminal = bool.Parse(parts[5]);
                        if (parts.Length >= 7) DisableCADOptions = bool.Parse(parts[6]);
                        if (parts.Length >= 8) RunAtStartup = bool.Parse(parts[7]);
                    }
                }
            }
            catch { }
        }

        private void LoadIdle()
        {
            try 
            { 
                if (File.Exists(_idlePath)) 
                {
                    string content = File.ReadAllText(_idlePath).Trim();
                    if (int.TryParse(content, out int mins)) IdleShutdownMinutes = mins;
                }
            } catch { }
        }

        private void LoadWallpaper()
        {
            try { if (File.Exists(_wallpaperPath)) WallpaperPath = File.ReadAllText(_wallpaperPath).Trim(); } catch { }
        }

        private void LoadMode()
        {
            try
            {
                if (File.Exists(_modePath))
                {
                    string content = File.ReadAllText(_modePath).Trim();
                    if (int.TryParse(content, out int mode))
                    {
                        SystemMode = mode;
                    }
                    else if (bool.TryParse(content, out bool isServer))
                    {
                        SystemMode = isServer ? 0 : 1;
                    }
                }
                else
                {
                    SystemMode = 0; // Default to server mode
                }
            }
            catch { SystemMode = 0; }
        }

        private void LoadComPort()
        {
            try { if (File.Exists(_comPortPath)) ComPort = File.ReadAllText(_comPortPath).Trim(); } catch { }
        }

        private void LoadAnimation()
        {
            try 
            { 
                if (File.Exists(_animationPath)) 
                {
                    string content = File.ReadAllText(_animationPath).Trim();
                    if (int.TryParse(content, out int design)) AnimationDesign = design;
                }
            } catch { }
        }

        private void LoadPcNameSettings()
        {
            try
            {
                if (File.Exists(_pcNameSettingsPath))
                {
                    var parts = File.ReadAllText(_pcNameSettingsPath).Split('|');
                    if (parts.Length >= 3)
                    {
                        ShowPcName = bool.Parse(parts[0]);
                        PcNamePosition = int.Parse(parts[1]);
                        PcNameAnimation = int.Parse(parts[2]);
                        if (parts.Length >= 4) PcNameFontSize = int.Parse(parts[3]);
                        if (parts.Length >= 5) PcNameOpacity = int.Parse(parts[4]);
                        if (parts.Length >= 6) PcNameColor = parts[5];
                    }
                }
            }
            catch { }
        }

        private void LoadUiSettings()
        {
            try
            {
                if (File.Exists(_uiSettingsPath))
                {
                    var parts = File.ReadAllText(_uiSettingsPath).Split('|');
                    if (parts.Length >= 2)
                    {
                        StatusFontSize = int.Parse(parts[0]);
                        TimerFontSize = int.Parse(parts[1]);
                    }
                }
            }
            catch { }
        }

        private void LoadInsertCoinsDuration()
        {
            try
            {
                if (File.Exists(_insertCoinsDurationPath))
                {
                    string content = File.ReadAllText(_insertCoinsDurationPath).Trim();
                    if (int.TryParse(content, out int seconds)) InsertCoinsDuration = seconds;
                }
            }
            catch { }
        }
    }
}
