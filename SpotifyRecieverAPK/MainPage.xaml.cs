using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Concentus.Enums;
using Concentus.Structs;
using System.Diagnostics;
using Concentus;


#if ANDROID
using Android.Content;
#endif

namespace SpotifyRecieverAPK
{
    public partial class MainPage : ContentPage
    {
#if ANDROID
        private Android.Media.AudioTrack _audioTrack;
#endif
        private bool _isAudioInitialized = false;
        private bool _isPaused = true;
        private bool _hasIP = false;
        private OpusDecoder decoder = new OpusDecoder(24000, 1);

        private bool connected = false;
        private long receivedBytes = 0;

        private System.Net.IPEndPoint remoteEP = null;
        private UdpClient Client = new UdpClient();
        
        private IPAddress ServerIP = IPAddress.Parse("100.121.206.79");
        private string ServerIPString;
        IPEndPoint serverAudioEP;

        public MainPage()
        {
            InitializeComponent();
            try
            {
                string data = Path.Combine(FileSystem.AppDataDirectory, "ipaddress.txt");

                if (File.Exists(data))
                {
                    string ip = File.ReadAllText(data);
                    ServerIP = IPAddress.Parse(ip);
                    ServerIPString = ip;
                    _hasIP = true;

                    Label_2.Text = $"IP lautet {ip}";
                }
                else
                {
                    Label_2.Text = "IP wurde nicht gefunden. Bitte eingeben.";
                    Entry_1.IsVisible = true;
                }
            }
            catch
            {
                Label_1.Text = "Fehler beim Laden der IP. Bitte App neustarten.";
            }
        }

        private void IPChange(object sender, EventArgs e)
        {
            if (Entry_1.Text != null)
            {
                try
                {
                    string ip = Entry_1.Text;
                    ServerIP = IPAddress.Parse(ip);
                    ServerIPString = ip;
                    _hasIP = true;

                    string path = Path.Combine(FileSystem.AppDataDirectory, "ipaddress.txt");
  
                    File.WriteAllText(path, ip);
                    
                    MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Label_2.Text = $"IP ist jetzt {ip}";
                        Entry_1.IsVisible = false;
                    });

                }
                catch
                {
                    MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Label_2.Text = $"Etwas beim speichern hat nicht geklappt!";
                    });
                }
            }
        }

        private void NewIP(object sender, EventArgs e)
        {
            _hasIP = false;
            string path = Path.Combine(FileSystem.AppDataDirectory, "ipaddress.txt");
            File.Delete(path);

            MainThread.InvokeOnMainThreadAsync(() =>
            {
                Entry_1.IsVisible = true;
                Label_2.Text = $"Bitte neue IP eingeben";
            });
        }
        private async void StartConnection(object sender, EventArgs e)
        {
            serverAudioEP = new IPEndPoint(ServerIP, 5000);

            if (connected && _hasIP)
            {
#if ANDROID
                var context = Platform.AppContext;
                var intent = new Intent(context, typeof(SpotifyRecieverAPK.Platforms.Android.MyForegroundService));
                context.StopService(intent);
#endif

                Label_2.Text = "Disconnected";
                Label_1.Text = "STARTSCREEN";
                Button_1.Text = "Erneut verbinden";
                PlayPause.ImageSource = "play.png";
                PlayPause.IsVisible = false;
                Next.IsVisible = false;
                Previous.IsVisible = false;
                Button_2.IsVisible = true;

                _isPaused = true;

                byte[] bytes = new byte[1024];
                bytes = System.Text.Encoding.UTF8.GetBytes("DISCONNECT");
                Client.Send(bytes, bytes.Length, ServerIPString, 5002);
                connected = false;
                _isAudioInitialized = false;
                
                return;
            }

            Label_1.Text = "Waiting for connection";
            Label_2.Text = "";
            Button_2.IsVisible = false;
            int trys = 0;
            while (!connected && _hasIP)
            {
                
                await Task.Delay(500);
                try
                {
                    string message = "CONNECT";
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message);

                    Client.Send(bytes, bytes.Length, serverAudioEP);

                    using (var cts = new CancellationTokenSource(2000))
                    {
                        var response = await Client.ReceiveAsync(cts.Token);
                        bytes = response.Buffer;
                        string result = System.Text.Encoding.UTF8.GetString(bytes);
                        if (result == "OK")
                        {
                            
                            Label_1.Text = $"Connection started!";
                            connected = true;
                            Button_1.Text = "Verbindung stoppen";

                            Client.Client.ReceiveBufferSize = 2 * 1024 * 1024;
                        }
                    }

                }
                catch (OperationCanceledException)
                {
                    if(trys >= 6)
                    {
                        Label_1.Text = $"Server ist schon in verwendung oder IP stimmt nicht.";
                        Label_2.Text = $"Versuche später erneut. Sonst kontaktiere Support";
                    }
                    else
                    {
                        Label_1.Text = $"Keine Antwort. Versuche erneut...";
                        trys += 1;
                    }
                    
                }
                catch (Exception ex)
                {
                    Label_1.Text = $"Fehler Beim connecten! {ex}";
                    Button_1.Text = "Nochmal  versuchen";
                }
            }
#if ANDROID
            try
            {
                var context = Platform.AppContext;
                var intent = new Intent(context, typeof(SpotifyRecieverAPK.Platforms.Android.MyForegroundService));
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
                    context.StartForegroundService(intent);
                else
                    context.StartService(intent);

                int bufferSize = Android.Media.AudioTrack.GetMinBufferSize(
                    24000,
                    Android.Media.ChannelOut.Mono,
                    Android.Media.Encoding.Pcm16bit);

                int safetyBuffer = bufferSize * 20;

                _audioTrack = new Android.Media.AudioTrack(
                    Android.Media.Stream.Music,
                    24000,
                    Android.Media.ChannelOut.Mono,
                    Android.Media.Encoding.Pcm16bit,
                    safetyBuffer,
                    Android.Media.AudioTrackMode.Stream);

                
                _isAudioInitialized = true;
            }
            catch (Exception ex)
            {

                Label_1.Text = $"Audio-Init Fehler: {ex.Message}";
            }
#endif
            Button_2.IsVisible = false;
            PlayPause.IsVisible = true;
            PlayPause.ImageSource = "play.png";
            Next.IsVisible = true;
            Previous.IsVisible = true;
            
            _ = Task.Run(async () =>
            {
                Task.Delay(1000);
                int TestingConnectionCounter = 0;
                bool isPlayingStartet = false;
                while (connected)
                {
                    try
                    {
                        UdpReceiveResult result = await Client.ReceiveAsync();

                        try
                        {
                            byte[] bytes = result.Buffer;

                            if (bytes.Length > 0 && bytes[0] == 65)
                            {
                                TestingConnectionCounter += 1;
                                MainThread.BeginInvokeOnMainThread(() => { Label_2.Text = $"TestingConnection {TestingConnectionCounter}"; });
                                string message = System.Text.Encoding.UTF8.GetString(bytes);

                                if (message == "ALIVE")
                                {
                                    bytes = System.Text.Encoding.UTF8.GetBytes("ALIVE");
                                    Client.Send(bytes, bytes.Length, ServerIPString, 5001);
                                }
                            }
                            else
                            {
                                int packetsize = result.Buffer.Length;
                                //!!Senkt performance!!
#if WINDOWS
                                receivedBytes += packetsize;
                                double totalkiloBytes = (double)receivedBytes / (1024 * 1024);

                                MainThread.BeginInvokeOnMainThread(() => { Label_1.Text = $"Empfangenes audio in MB: {totalkiloBytes:F2}"; });
#endif
                                short[] receivedAudio = new short[480];
                                try
                                {
                                    int decodedAudio = decoder.Decode(result.Buffer, 0, result.Buffer.Length, receivedAudio, 0, receivedAudio.Length);

                                    byte[] finishedAudio = new byte[decodedAudio * 2];

                                    Buffer.BlockCopy(receivedAudio, 0, finishedAudio, 0, finishedAudio.Length);
#if ANDROID
                                    if (_isAudioInitialized && _audioTrack != null)
                                    {
                                        _audioTrack.Write(finishedAudio, 0, finishedAudio.Length);
                                        if(!isPlayingStartet)
                                        {
                                            _audioTrack.Play();
                                            isPlayingStartet = true;
                                        }
                                    }
#endif
                                }
                                catch (OpusException ex)
                                {
                                    connected = false;
                                    byte[] ERROR = new byte[1024];
                                    ERROR = System.Text.Encoding.UTF8.GetBytes("DISCONNECT");
                                    Client.Send(ERROR, ERROR.Length, ServerIPString, 5002);
                                    MainThread.BeginInvokeOnMainThread(() => { Label_2.Text = ex.Message; });
                                    continue;
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            byte[] ERROR = new byte[1024];
                            ERROR = System.Text.Encoding.UTF8.GetBytes("DISCONNECT");
                            Client.Send(ERROR, ERROR.Length, ServerIPString, 5002);
                            if (!connected) break;
                            MainThread.BeginInvokeOnMainThread(() => {
                                Label_1.Text = "Fehler beim Decoden";
                                Label_2.Text = $"Genauer Fehler: {ex}";
                            });
                        }
                    }
                    catch
                    {
                        if (!connected) break;
                        MainThread.BeginInvokeOnMainThread(() => {
                            Label_1.Text = "Fehler beim höhren";
                        });
                    }
                }
            });
        }

        private async void PLAYPAUSE(object sender, EventArgs e)
        {
            void SendMessage(string message)
            {
                byte[] command = new byte[1024];
                command = Encoding.UTF8.GetBytes(message);
                Client.Send(command, command.Length, ServerIPString, 5003);
            }
            

            if (_isPaused == false) { PlayPause.ImageSource = "play.png"; _isPaused = true; SendMessage("PAUSE"); }
            else { PlayPause.ImageSource = "pause.png"; _isPaused = false; SendMessage("PLAY"); }
        }

        private async void NEXT(object sender, EventArgs e)
        {
            byte[] command = new byte[1024];
            command = Encoding.UTF8.GetBytes("NEXT");
            Client.Send(command, command.Length, ServerIPString, 5003);
        }

        private async void PREVIOUS(object sender, EventArgs e)
        {
            byte[] command = new byte[1024];
            command = Encoding.UTF8.GetBytes("PREVIOUS");
            Client.Send(command, command.Length, ServerIPString, 5003);
        }

    }
}