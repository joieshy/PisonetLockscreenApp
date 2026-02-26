using System;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace PisonetLockscreenApp.Services
{
    public class AudioService
    {
        private MMDevice? _device;

        public AudioService()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AudioService Initialize Error: {ex.Message}");
            }
        }

        public void SetMute(bool mute)
        {
            try
            {
                if (_device == null) Initialize();
                if (_device != null)
                {
                    // Ensure master volume is NOT muted so system sounds (like countdown) can play
                    if (_device.AudioEndpointVolume.Mute)
                    {
                        _device.AudioEndpointVolume.Mute = false;
                    }

                    // Iterate through all audio sessions and mute only non-system/non-app processes
                    var sessions = _device.AudioSessionManager.Sessions;
                    uint currentPid = (uint)Environment.ProcessId;

                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var session = sessions[i];
                        if (session == null) continue;

                        // In NAudio, we can access the ProcessID via the GetProcessID property
                        // which is available on the AudioSessionControl object in newer versions
                        // or we can use the underlying IAudioSessionControl2 interface.
                        uint pid = session.GetProcessID;

                        // Skip system sounds (PID 0) and our own app's sounds
                        if (pid == 0 || pid == currentPid)
                        {
                            // Ensure these are unmuted
                            session.SimpleAudioVolume.Mute = false;
                        }
                        else
                        {
                            // Mute/Unmute other applications (Browsers, Media Players, etc.)
                            // This targets browsers as requested, but also other apps that might interfere with the lockscreen
                            session.SimpleAudioVolume.Mute = mute;
                        }
                    }
                }

                if (mute)
                {
                    StopMedia();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AudioService SetMute Error: {ex.Message}");
            }
        }

        private void StopMedia()
        {
            try
            {
                // Send Media Stop key to pause/stop any active media players
                NativeMethods.keybd_event(NativeMethods.VK_MEDIA_STOP, 0, 0, 0);
                NativeMethods.keybd_event(NativeMethods.VK_MEDIA_STOP, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
            }
            catch { }
        }

        public void PlaySound(string filePath)
        {
            try
            {
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
                if (System.IO.File.Exists(fullPath))
                {
                    using (var player = new System.Media.SoundPlayer(fullPath))
                    {
                        player.Play();
                    }
                }
                else if (System.IO.File.Exists(filePath))
                {
                    using (var player = new System.Media.SoundPlayer(filePath))
                    {
                        player.Play();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AudioService PlaySound Error: {ex.Message}");
            }
        }
    }
}
