Audio Streamer from Linux to Android

With this project, you can stream your entire system audio from a Linux OS (like Ubuntu-based systems) to your Android phone. It also features an integrated Spotify controller, allowing you to Play/Pause or skip to the Previous/Next song.

Good to Know

  Ports: The Server uses the ports 5000, 5001, 5002, and 5003. Please ensure they are unused or change them in the code.

  If not, you need to install ffmpeg on your Linux system.

  I  used the Help of Gemini

How to Start the Server (Linux)

  - Download or clone the Server files to your Linux machine.

  - Open your terminal in the project folder and make the file executable:
    "chmod +x SpotifyStreamer"

  - Start the server by running:
    "./SpotifyStreamer"

  - The server should now be running and listening for connections.

How to Use the Client (Android APK)

  - When you first start the app, enter the IP address of your Linux system.

  - You can change this IP at any time by clicking the button in the bottom right corner of the start screen.

  - To start streaming, click the button labeled "Verbinden" (Connect). It will connect automatically.

  - Once connected, you can stop the stream by clicking "Disconnect".

  - Use the media buttons below to control Spotify (only Spotify is supported!) running on your Linux system.
