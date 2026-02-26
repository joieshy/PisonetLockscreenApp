# Pisonet Lockscreen App

A Windows-based lockscreen application designed for Pisonet systems. This application manages user sessions, monitors hardware, and provides a secure lockscreen interface.

## Features

- **Secure Lockscreen:** Prevents unauthorized access to the desktop.
- **Session Management:** Handles user login and timer overlays.
- **Socket Integration:** Communicates with a server using Socket.IO for real-time updates.
- **Hardware Monitoring:** Monitors system hardware status.
- **Audio Control:** Integrated audio services using NAudio.
- **Configurable Settings:** Easily manage server IP, admin passwords, rates, and security options.

## Technologies Used

- **Framework:** .NET 8.0/9.0/10.0 (Windows Forms)
- **Communication:** SocketIOClient
- **Audio:** NAudio
- **Database:** MySQL / SQL Server support
- **Hardware Monitoring:** System.Management

## Getting Started

### Prerequisites

- .NET SDK (compatible with the version specified in `.csproj`)
- Visual Studio 2022 or VS Code

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/PisonetLockscreenApp.git
   ```
2. Open the solution file `PisonetLockscreenApp.sln` in Visual Studio.
3. Restore NuGet packages.
4. Build and run the project.

## Configuration

The application stores its configuration in the `AppData` folder:
`%AppData%\PisonetLockscreenApp\`

Key configuration files include:
- `server.txt`: Server IP address.
- `adminpass.txt`: Administrator password.
- `rates.txt`: Pricing rates.
- `security.txt`: Security and restriction settings.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built for the Pisonet community.
- Uses NAudio for audio management.
- Uses SocketIOClient for real-time communication.
