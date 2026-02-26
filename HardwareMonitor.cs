using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.IO;
using System.Threading.Tasks;

namespace PisonetLockscreenApp.Services
{
    public class HardwareMonitor : IDisposable
    {
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        private long _lastTotalBytes = -1;
        private string _lastNetSpeed = "0.00 Mbps";
        private DateTime _lastNetUpdate = DateTime.MinValue;
        private string _lastCpuTemp = "N/A";
        private DateTime _lastTempUpdate = DateTime.MinValue;

        // Cached static info to reduce WMI overhead
        private string? _cachedCpuModel;
        private string? _cachedGpuInfo;
        private string? _cachedMacAddress;
        private string? _cachedOsVersion;
        private string? _cachedMotherboard;
        private float _cachedTotalRamMB = -1;

        public HardwareMonitor()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch { }
            
            // Pre-cache static info in background to avoid blocking UI thread during initialization
            Task.Run(() => {
                GetCpuModel();
                GetGpuInfo();
                GetMacAddress();
                GetOsVersion();
                GetMotherboard();
                GetTotalRamMB();
            });
        }

        public string GetCpuUsage() => _cpuCounter != null ? $"{Math.Round(_cpuCounter.NextValue())}%" : "N/A";

        public string GetCpuModel()
        {
            if (_cachedCpuModel != null) return _cachedCpuModel;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (ManagementObject mo in searcher.Get()) 
                {
                    _cachedCpuModel = mo["Name"]?.ToString() ?? "N/A";
                    return _cachedCpuModel;
                }
            }
            catch { }
            return "N/A";
        }

        public string GetRamUsage()
        {
            if (_ramCounter == null) return "N/A";
            try
            {
                float available = _ramCounter.NextValue();
                float total = GetTotalRamMB();
                float used = total - available;
                if (used < 0) used = 0;
                return $"{Math.Round(used / 1024, 1)}GB / {Math.Round(total / 1024, 1)}GB";
            }
            catch { return "N/A"; }
        }

        public string GetNetworkSpeed()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up && 
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

                long currentBytes = 0;
                foreach (var nic in interfaces)
                {
                    try
                    {
                        var stats = nic.GetIPStatistics();
                        currentBytes += stats.BytesReceived + stats.BytesSent;
                    }
                    catch { }
                }

                if (_lastTotalBytes == -1)
                {
                    _lastTotalBytes = currentBytes;
                    _lastNetUpdate = DateTime.Now;
                    return "0.00 Mbps";
                }

                double elapsedSeconds = (DateTime.Now - _lastNetUpdate).TotalSeconds;
                if (elapsedSeconds < 0.8) return _lastNetSpeed;

                long diff = currentBytes - _lastTotalBytes;
                if (diff < 0) diff = 0; // Handle counter reset

                double mbps = (diff * 8 / 1_000_000.0) / elapsedSeconds;
                
                _lastTotalBytes = currentBytes;
                _lastNetUpdate = DateTime.Now;
                
                // Smooth the value slightly if it's a very small change
                if (mbps < 0.01) mbps = 0;
                
                _lastNetSpeed = $"{mbps:F2} Mbps";
                return _lastNetSpeed;
            }
            catch 
            {
                return "N/A";
            }
        }

        public string GetCpuTemp()
        {
            // WMI Temperature queries are extremely heavy. Cache for 10 seconds.
            if ((DateTime.Now - _lastTempUpdate).TotalSeconds < 10 && _lastCpuTemp != "N/A")
            {
                return _lastCpuTemp;
            }

            _lastTempUpdate = DateTime.Now;
            try
            {
                // Method 1: MSAcpi_ThermalZoneTemperature (Requires Admin)
                using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        double tempK = Convert.ToDouble(obj["CurrentTemperature"]);
                        double celsius = (tempK / 10.0) - 273.15;
                        if (celsius > 0 && celsius < 150)
                        {
                            _lastCpuTemp = $"{Math.Round(celsius)}°C";
                            return _lastCpuTemp;
                        }
                    }
                }
            }
            catch { }

            try
            {
                // Method 2: Win32_TemperatureProbe
                using (var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_TemperatureProbe"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object val = obj["CurrentReading"];
                        if (val != null)
                        {
                            _lastCpuTemp = $"{val}°C";
                            return _lastCpuTemp;
                        }
                    }
                }
            }
            catch { }

            _lastCpuTemp = "N/A";
            return "N/A";
        }

        public string GetIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "N/A";
        }

        public string GetMacAddress()
        {
            if (_cachedMacAddress != null) return _cachedMacAddress;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT MACAddress FROM Win32_NetworkAdapter WHERE MACAddress IS NOT NULL AND PhysicalAdapter = TRUE");
                foreach (ManagementObject mo in searcher.Get())
                {
                    _cachedMacAddress = mo["MACAddress"]?.ToString() ?? "N/A";
                    return _cachedMacAddress;
                }
            }
            catch { }
            return "N/A";
        }

        public string GetGpuInfo()
        {
            if (_cachedGpuInfo != null) return _cachedGpuInfo;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (ManagementObject mo in searcher.Get())
                {
                    _cachedGpuInfo = mo["Name"]?.ToString() ?? "N/A";
                    return _cachedGpuInfo;
                }
            }
            catch { }
            return "N/A";
        }

        public string GetDiskUsage()
        {
            try
            {
                var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
                if (drive == null) return "N/A";
                
                long used = drive.TotalSize - drive.TotalFreeSpace;
                int percent = (int)((double)used / drive.TotalSize * 100);
                return $"{Math.Round(used / 1e9, 1)}GB / {Math.Round(drive.TotalSize / 1e9, 1)}GB ({percent}%)";
            }
            catch { return "N/A"; }
        }

        public string GetOsVersion()
        {
            if (_cachedOsVersion != null) return _cachedOsVersion;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
                foreach (ManagementObject mo in searcher.Get())
                {
                    _cachedOsVersion = mo["Caption"]?.ToString() ?? "N/A";
                    return _cachedOsVersion;
                }
            }
            catch { }
            _cachedOsVersion = Environment.OSVersion.ToString();
            return _cachedOsVersion;
        }

        public string GetMotherboard()
        {
            if (_cachedMotherboard != null) return _cachedMotherboard;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Product FROM Win32_BaseBoard");
                foreach (ManagementObject mo in searcher.Get())
                {
                    _cachedMotherboard = mo["Product"]?.ToString() ?? "N/A";
                    return _cachedMotherboard;
                }
            }
            catch { }
            return "N/A";
        }

        private float GetTotalRamMB()
        {
            if (_cachedTotalRamMB > 0) return _cachedTotalRamMB;
            try {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject res in searcher.Get())
                {
                    _cachedTotalRamMB = Convert.ToInt64(res["TotalPhysicalMemory"]) / 1024f / 1024f;
                    return _cachedTotalRamMB;
                }
            } catch { }
            return 4096f;
        }

        public void Dispose() { _cpuCounter?.Dispose(); _ramCounter?.Dispose(); }
    }
}
