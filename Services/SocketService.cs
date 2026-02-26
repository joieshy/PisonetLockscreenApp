using System;
using System.Threading.Tasks;
using SocketIOClient;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Collections.Generic;

namespace PisonetLockscreenApp.Services
{
    public class SocketService
    {
        private void LogToFile(string msg)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_socket_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {msg}\n");
            }
            catch { }
        }

        private SocketIOClient.SocketIO? _client;
        private string _serverUrl;
        private readonly string _pcName;
        private readonly string _pcId;
        private Dictionary<string, string> _settings = new Dictionary<string, string>();

        public bool IsConnected => _client?.Connected ?? false;

        public event Action<int, string, string, int, int, bool>? OnLoginSuccess;
        public event Action<string>? OnLoginFailed;
        public event Action<string>? OnRegisterSuccess;
        public event Action<string>? OnRegisterFailed;
        public event Action<int, string, int, int, bool>? OnTimeUpdate;
        public event Action<int, int>? OnPointsUpdate;
        public event Action<string>? OnWallpaperUpdate;
        public event Action<int>? OnAnimationUpdate;
        public event Action<string, bool>? OnAnnouncementUpdate;
        public event Action? OnLogout;
        public event Action? OnConnected;
        public event Action? OnDisconnected;
        public event Action? OnRemoteLock;
        public event Action<int, string, int, int, bool>? OnRemoteResume;
        public event Action<string>? OnRemoteMessage;
        public event Action<string>? OnRemoteBuzz;
        public event Action? OnStartSpectate;
        public event Action? OnStopSpectate;
        public event Action<string>? OnSystemCommand;
        public event Action<string>? OnOperationSuccess;
        public event Action<string>? OnOperationError;
        public event Action<string>? OnError;

        public SocketService(string serverUrl, string pcName, string pcId)
        {
            _serverUrl = serverUrl;
            _pcName = pcName;
            _pcId = pcId;
            InitializeClient();
        }

        public void UpdateServerUrl(string newUrl)
        {
            if (_serverUrl == newUrl) return;
            _serverUrl = newUrl;
            InitializeClient();
        }

        private void InitializeClient()
        {
            if (_client != null)
            {
                try { _client.DisconnectAsync(); _client.Dispose(); } catch { }
            }

            if (string.IsNullOrWhiteSpace(_serverUrl)) return;

            System.Diagnostics.Debug.WriteLine($"SocketIO: Initializing client for {_serverUrl}");

            _client = new SocketIOClient.SocketIO(_serverUrl, new SocketIOOptions
            {
                Reconnection = true,
                ReconnectionAttempts = int.MaxValue,
                ReconnectionDelay = 2000,
                EIO = SocketIO.Core.EngineIO.V4,
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket // Force WebSocket
            });

            InitializeEvents();
        }

        private void InitializeEvents()
        {
            if (_client == null) return;

            _client.OnConnected += (sender, e) => 
            {
                LogToFile($"Connected to {_serverUrl}");
                System.Diagnostics.Debug.WriteLine($"SocketIO: Connected to {_serverUrl}");
                OnConnected?.Invoke();
            };

            _client.OnDisconnected += (sender, e) => 
            {
                LogToFile($"Disconnected from {_serverUrl}. Reason: {e}");
                System.Diagnostics.Debug.WriteLine($"SocketIO: Disconnected from {_serverUrl}. Reason: {e}");
                OnDisconnected?.Invoke();
            };

            _client.OnReconnectAttempt += (sender, e) => System.Diagnostics.Debug.WriteLine($"SocketIO: Reconnecting... Attempt {e}");

            _client.OnError += (sender, e) => 
            {
                System.Diagnostics.Debug.WriteLine($"SocketIO Error: {e}");
                OnError?.Invoke(e);
            };

            _client.On("login_success", response =>
            {
                System.Diagnostics.Debug.WriteLine($"Raw login_success: {response}");
                try
                {
                    var element = response.GetValue<JsonElement>();
                    string user = "Admin";
                    string role = "Member";
                    int timeLeft = 0;
                    int points = 0;
                    int nextBonus = 0;
                    bool isVip = false;

                    if (element.TryGetProperty("username", out var userProp))
                    {
                        user = userProp.GetString() ?? "Admin";
                    }

                    if (element.TryGetProperty("role", out var roleProp))
                    {
                        role = roleProp.GetString() ?? "Member";
                    }

                    if (element.TryGetProperty("time_left", out var timeProp))
                    {
                        if (timeProp.ValueKind == JsonValueKind.Number) timeLeft = (int)timeProp.GetDouble();
                        else if (timeProp.ValueKind == JsonValueKind.String && int.TryParse(timeProp.GetString(), out int tl)) timeLeft = tl;
                    }

                    if (element.TryGetProperty("points", out var pointsProp))
                    {
                        if (pointsProp.ValueKind == JsonValueKind.Number) points = (int)pointsProp.GetDouble();
                        else if (pointsProp.ValueKind == JsonValueKind.String && int.TryParse(pointsProp.GetString(), out int pts)) points = pts;
                    }

                    if (element.TryGetProperty("next_bonus", out var bonusProp))
                    {
                        if (bonusProp.ValueKind == JsonValueKind.Number) nextBonus = (int)bonusProp.GetDouble();
                        else if (bonusProp.ValueKind == JsonValueKind.String && int.TryParse(bonusProp.GetString(), out int nb)) nextBonus = nb;
                    }

                    if (element.TryGetProperty("is_vip", out var vipProp))
                    {
                        if (vipProp.ValueKind == JsonValueKind.True) isVip = true;
                        else if (vipProp.ValueKind == JsonValueKind.False) isVip = false;
                        else if (vipProp.ValueKind == JsonValueKind.Number) isVip = vipProp.GetDouble() > 0;
                    }

                    OnLoginSuccess?.Invoke(timeLeft, user, role, points, nextBonus, isVip);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing login_success: {ex.Message}");
                }
            });

            _client.On("login_error", response =>
            {
                try
                {
                    var reason = response.GetValue<string>();
                    OnLoginFailed?.Invoke(reason ?? "Invalid username or password.");
                }
                catch { OnLoginFailed?.Invoke("Login Error"); }
            });

            _client.On("timer_update", response =>
            {
                try
                {
                    var element = response.GetValue<JsonElement>();
                    int timeLeft = 0;
                    string username = "Guest";
                    int points = 0;
                    int nextBonus = 0;
                    bool isVip = false;

                    if (element.TryGetProperty("time_left", out var timeProp))
                    {
                        if (timeProp.ValueKind == JsonValueKind.Number) timeLeft = (int)timeProp.GetDouble();
                        else if (timeProp.ValueKind == JsonValueKind.String && int.TryParse(timeProp.GetString(), out int tl)) timeLeft = tl;
                    }
                    if (element.TryGetProperty("username", out var userProp))
                    {
                        username = userProp.GetString() ?? "Guest";
                    }
                    if (element.TryGetProperty("points", out var pointsProp))
                    {
                        if (pointsProp.ValueKind == JsonValueKind.Number) points = (int)pointsProp.GetDouble();
                        else if (pointsProp.ValueKind == JsonValueKind.String && int.TryParse(pointsProp.GetString(), out int pts)) points = pts;
                    }
                    if (element.TryGetProperty("next_bonus", out var bonusProp))
                    {
                        if (bonusProp.ValueKind == JsonValueKind.Number) nextBonus = (int)bonusProp.GetDouble();
                        else if (bonusProp.ValueKind == JsonValueKind.String && int.TryParse(bonusProp.GetString(), out int nb)) nextBonus = nb;
                    }
                    if (element.TryGetProperty("is_vip", out var vipProp))
                    {
                        if (vipProp.ValueKind == JsonValueKind.True) isVip = true;
                        else if (vipProp.ValueKind == JsonValueKind.False) isVip = false;
                        else if (vipProp.ValueKind == JsonValueKind.Number) isVip = vipProp.GetDouble() > 0;
                    }
                    OnTimeUpdate?.Invoke(timeLeft, username, points, nextBonus, isVip);
                }
                catch { }
            });

            _client.On("points_update", response =>
            {
                try
                {
                    var element = response.GetValue<JsonElement>();
                    int points = 0;
                    int nextBonus = 0;
                    if (element.ValueKind == JsonValueKind.Number) 
                    {
                        points = (int)element.GetDouble();
                    }
                    else
                    {
                        if (element.TryGetProperty("points", out var pointsProp))
                        {
                            if (pointsProp.ValueKind == JsonValueKind.Number) points = (int)pointsProp.GetDouble();
                            else if (pointsProp.ValueKind == JsonValueKind.String && int.TryParse(pointsProp.GetString(), out int pts)) points = pts;
                        }
                        if (element.TryGetProperty("next_bonus", out var bonusProp))
                        {
                            if (bonusProp.ValueKind == JsonValueKind.Number) nextBonus = (int)bonusProp.GetDouble();
                            else if (bonusProp.ValueKind == JsonValueKind.String && int.TryParse(bonusProp.GetString(), out int nb)) nextBonus = nb;
                        }
                    }
                    OnPointsUpdate?.Invoke(points, nextBonus);
                }
                catch { }
            });

            _client.On("update_wallpaper", response =>
            {
                try
                {
                    var url = response.GetValue<string>();
                    if (!string.IsNullOrEmpty(url)) OnWallpaperUpdate?.Invoke(url);
                }
                catch { }
            });

            _client.On("update_settings_display", response =>
            {
                try
                {
                    var element = response.GetValue<JsonElement>();
                    
                    foreach (var prop in element.EnumerateObject())
                    {
                        _settings[prop.Name] = prop.Value.ToString();
                    }

                    // Check for client_wallpaper or shop_wallpaper
                    string? wallpaperPath = null;
                    if (element.TryGetProperty("client_wallpaper", out var clientWallProp))
                    {
                        wallpaperPath = clientWallProp.GetString();
                    }
                    else if (element.TryGetProperty("shop_wallpaper", out var shopWallProp))
                    {
                        wallpaperPath = shopWallProp.GetString();
                    }

                    if (!string.IsNullOrEmpty(wallpaperPath))
                    {
                        OnWallpaperUpdate?.Invoke(wallpaperPath);
                    }

                    if (element.TryGetProperty("shop_animation", out var animProp))
                    {
                        string val = animProp.GetString() ?? "0";
                        if (int.TryParse(val, out int animIndex))
                        {
                            OnAnimationUpdate?.Invoke(animIndex);
                        }
                    }

                    if (element.TryGetProperty("announcement_text", out var annTextProp) || 
                        element.TryGetProperty("announcement_enabled", out var annEnabledProp))
                    {
                        string text = _settings.TryGetValue("announcement_text", out var t) ? t : "";
                        bool enabled = _settings.TryGetValue("announcement_enabled", out var e) && e == "true";
                        OnAnnouncementUpdate?.Invoke(text, enabled);
                    }
                }
                catch { }
            });

            // Remote Commands from Dashboard (matching server.js)
            _client.On("force_lock_pause", _ => OnRemoteLock?.Invoke());
            _client.On("force_resume", response => 
            {
                try 
                {
                    var element = response.GetValue<JsonElement>();
                    string user = "Guest";
                    int timeLeft = -1;
                    int points = 0;
                    int nextBonus = 0;
                    bool isVip = false;

                    if (element.TryGetProperty("username", out var userProp))
                    {
                        user = userProp.GetString() ?? "Guest";
                    }

                    if (element.TryGetProperty("time_left", out var timeProp))
                    {
                        if (timeProp.ValueKind == JsonValueKind.Number) timeLeft = (int)timeProp.GetDouble();
                        else if (timeProp.ValueKind == JsonValueKind.String && int.TryParse(timeProp.GetString(), out int tl)) timeLeft = tl;
                    }

                    if (element.TryGetProperty("points", out var pointsProp))
                    {
                        if (pointsProp.ValueKind == JsonValueKind.Number) points = (int)pointsProp.GetDouble();
                        else if (pointsProp.ValueKind == JsonValueKind.String && int.TryParse(pointsProp.GetString(), out int pts)) points = pts;
                    }
                    if (element.TryGetProperty("next_bonus", out var bonusProp))
                    {
                        if (bonusProp.ValueKind == JsonValueKind.Number) nextBonus = (int)bonusProp.GetDouble();
                        else if (bonusProp.ValueKind == JsonValueKind.String && int.TryParse(bonusProp.GetString(), out int nb)) nextBonus = nb;
                    }
                    if (element.TryGetProperty("is_vip", out var vipProp))
                    {
                        if (vipProp.ValueKind == JsonValueKind.True) isVip = true;
                        else if (vipProp.ValueKind == JsonValueKind.False) isVip = false;
                        else if (vipProp.ValueKind == JsonValueKind.Number) isVip = vipProp.GetDouble() > 0;
                    }
                    OnRemoteResume?.Invoke(timeLeft, user, points, nextBonus, isVip);
                }
                catch { OnRemoteResume?.Invoke(-1, "Guest", 0, 0, false); }
            });
            _client.On("display_message", response => 
            {
                LogToFile($"Received display_message: {response}");
                try 
                { 
                    var element = response.GetValue<JsonElement>();
                    if (element.TryGetProperty("message", out var msgProp))
                    {
                        string msg = msgProp.GetString() ?? "";
                        LogToFile($"Invoking OnRemoteMessage with: {msg}");
                        OnRemoteMessage?.Invoke(msg);
                    }
                } catch (Exception ex) {
                    LogToFile($"display_message error: {ex.Message}");
                }
            });

            _client.On("buzz_client", response => 
            {
                LogToFile($"Received buzz_client: {response}");
                try 
                { 
                    var element = response.GetValue<JsonElement>();
                    if (element.TryGetProperty("message", out var msgProp))
                    {
                        string msg = msgProp.GetString() ?? "";
                        LogToFile($"Invoking OnRemoteBuzz with: {msg}");
                        OnRemoteBuzz?.Invoke(msg);
                    }
                } catch (Exception ex) {
                    LogToFile($"buzz_client error: {ex.Message}");
                }
            });

            // Spectate Commands
            _client.On("start_sharing", response => 
            {
                System.Diagnostics.Debug.WriteLine("SocketIO: Received start_sharing command");
                OnStartSpectate?.Invoke();
            });

            _client.On("stop_spectate", response => 
            {
                System.Diagnostics.Debug.WriteLine("SocketIO: Received stop_spectate command");
                OnStopSpectate?.Invoke();
            });

            _client.On("system_command", response =>
            {
                try
                {
                    var element = response.GetValue<JsonElement>();
                    string cmd = "";
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        cmd = element.GetString() ?? "";
                    }
                    else if (element.TryGetProperty("command", out var cmdProp))
                    {
                        cmd = cmdProp.GetString() ?? "";
                    }

                    if (!string.IsNullOrEmpty(cmd))
                    {
                        LogToFile($"Received system_command: {cmd}");
                        OnSystemCommand?.Invoke(cmd);
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"system_command error: {ex.Message}");
                }
            });

            _client.On("op_success", response =>
            {
                try { 
                    var msg = response.GetValue<string>();
                    if (msg.Contains("Registration successful")) OnRegisterSuccess?.Invoke(msg);
                    OnOperationSuccess?.Invoke(msg); 
                } catch { }
            });

            _client.On("op_error", response =>
            {
                try { 
                    var msg = response.GetValue<string>();
                    if (msg.Contains("Registration failed") || msg.Contains("Username already exists") || msg.Contains("Insufficient time")) OnRegisterFailed?.Invoke(msg);
                    OnOperationError?.Invoke(msg); 
                } catch { }
            });
        }

        public string GetSetting(string key, string defaultValue = "")
        {
            return _settings.TryGetValue(key, out var val) ? val : defaultValue;
        }

        public async Task ConnectAsync()
        {
            if (_client == null) return;
            try
            {
                System.Diagnostics.Debug.WriteLine($"SocketIO: Connecting to {_serverUrl}...");
                await _client.ConnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Socket Connection Error: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client != null && _client.Connected)
            {
                await _client.DisconnectAsync();
            }
        }

        public async Task LoginAsync(string username, string password, string voucher = "", string mac = "")
        {
            if (_client == null || !_client.Connected) return;
            System.Diagnostics.Debug.WriteLine($"SocketIO: Sending attempt_login for {username} with voucher: {voucher}...");
            await _client.EmitAsync("attempt_login", new
            {
                username = username,
                password = password,
                voucher = voucher,
                pc_name = _pcName,
                pc_id = _pcId,
                mac = mac,
                device = "PC"
            });
        }

        public async Task RegisterMemberAsync(string username, string password, string pcName)
        {
            if (_client == null || !_client.Connected) return;
            await _client.EmitAsync("member_register", new
            {
                username = username,
                password = password,
                pc_name = pcName
            });
        }

        public async Task SaveShopSettingsAsync(object settings)
        {
            if (_client == null || !_client.Connected) return;
            await _client.EmitAsync("save_shop_settings", settings);
        }

        public async Task UseVoucherAsync(string code, string mac = "")
        {
            if (_client == null || !_client.Connected) return;
            System.Diagnostics.Debug.WriteLine($"SocketIO: Sending use_voucher for {code}...");
            await _client.EmitAsync("use_voucher", new
            {
                code = code,
                pc_name = _pcName,
                mac = mac,
                id = _client.Id
            });
        }

        public async Task SendStatusAsync(object statusData)
        {
            if (_client != null && _client.Connected)
            {
                await _client.EmitAsync("pc_status", statusData);
            }
        }

        public async Task SendScreenFrameAsync(string base64Image)
        {
            if (_client != null && _client.Connected)
            {
                await _client.EmitAsync("screen_data", new { image = base64Image, pc_name = _pcName });
            }
        }

        public async Task LogoutAsync(string username, string mac = "")
        {
            if (_client != null && _client.Connected)
            {
                await _client.EmitAsync("user_logout", new 
                { 
                    username = username, 
                    pc_name = _pcName,
                    mac = mac,
                    device = "PC" 
                });
            }
            OnLogout?.Invoke();
        }

        public async Task SendCoinInsertedAsync(int amount)
        {
            if (_client != null && _client.Connected)
            {
                await _client.EmitAsync("client_coin_inserted", new { amount = amount });
            }
        }

        public class LoginSuccessPayload
        {
            [JsonPropertyName("time_left")]
            public int TimeLeft { get; set; }

            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("points")]
            public int Points { get; set; }

            [JsonPropertyName("next_bonus")]
            public int NextBonus { get; set; }
        }
    }
}
