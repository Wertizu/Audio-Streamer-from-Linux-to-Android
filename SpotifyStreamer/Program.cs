using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Net;
using Concentus.Enums;
using Concentus.Structs;
using Tmds.DBus;
using System.Linq.Expressions;

class AudioRecorder
{
    private static readonly object serverLock = new object();
    private static bool connect = false;
    private static UdpClient AudioServer = new UdpClient(5000);
    private static UdpClient ControlServer = new UdpClient(5001);
    private static UdpClient DisconnectServer = new UdpClient(5002);
    private static UdpClient CommandServer = new UdpClient(5003);
    private static IPEndPoint IpClient = new IPEndPoint(IPAddress.Any, 0);
    private static Process ffmpegProcess;
    private static CancellationTokenSource cts;
    private static CancellationToken token;

    private static Connection connection = Connection.Session;

    private static ObjectPath spotifyObjectPath = new ObjectPath("/org/mpris/MediaPlayer2");
    private static string SpotifyService = "org.mpris.MediaPlayer2.spotify";

    public IMediaPlayer2Player player = connection.CreateProxy<IMediaPlayer2Player>(SpotifyService, spotifyObjectPath);
    static async Task Main()
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        while (true)
        {
            Console.WriteLine("\nWaiting for connection...");


            OpusEncoder encoder = new OpusEncoder(24000, 1, OpusApplication.OPUS_APPLICATION_AUDIO);
            encoder.Bitrate = 64000;

            while (!connect)
            {

                UdpReceiveResult result = await AudioServer.ReceiveAsync();
                IpClient = result.RemoteEndPoint;

                byte[] initData = result.Buffer;
                string message = Encoding.UTF8.GetString(initData);

                if (message == "CONNECT")
                {
                    message = null;
                    connect = true;
                    Console.WriteLine($"\nVerbindung von {IpClient.Address} akzeptiert!");

                    cts?.Dispose();
                    cts = new CancellationTokenSource();
                    token = cts.Token;
                    byte[] answer = Encoding.UTF8.GetBytes("OK");
                    AudioServer.Send(answer, answer.Length, IpClient);

                    _ = Task.Run(() => ConnectionTest(token), token);
                    _ = Task.Run(() => Disconnect(token), token);
                    _ = Task.Run(() => ReceiveCommands(token), token);
                }
            }
            
            

            string monitorName = "auto_null.monitor";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-f pulse -i {monitorName} -f s16le -ar 24000 -ac 1 -",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (ffmpegProcess = Process.Start(psi))
            {
                using (BinaryReader reader = new BinaryReader(ffmpegProcess.StandardOutput.BaseStream))
                {
                    Console.WriteLine("\nAufnahme Gestartet!");



                    while (!ffmpegProcess.HasExited && !token.IsCancellationRequested)
                    {
                        byte[] buffer = reader.ReadBytes(960);


                        if (buffer.Length < 960)
                        {
                            break; 
                        }
                        short[] pcmData = new short[buffer.Length / 2];
                        Buffer.BlockCopy(buffer, 0, pcmData, 0, buffer.Length);

                        byte[] opusOutput = new byte[1024];
                        int frameSize = 480;

                        int opusPacketLength = encoder.Encode(pcmData, 0, frameSize, opusOutput, 0, opusOutput.Length);


                        if (!token.IsCancellationRequested)
                        {
                            await AudioServer.SendAsync(opusOutput, opusPacketLength, IpClient);
                        }
                    }
                }
                try 
                {
                    if(ffmpegProcess != null)
                    {
                        bool hasExited = ffmpegProcess.HasExited;
                        if(!hasExited)
                        {
                            ffmpegProcess.Kill();
                            ffmpegProcess.WaitForExit();
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("[System] ffmpeg bereits gestoppt");
                }
                catch(Exception) { }
            }
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nMAIN is Restarting!");
            Console.ForegroundColor = ConsoleColor.Blue;

            connect = false;
            ResetServers();
        }
    }
    async static Task ConnectionTest(CancellationToken token)
    {
  
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(30 * 1000, token);
            byte[] testMessage = Encoding.UTF8.GetBytes("ALIVE");
            ControlServer.Send(testMessage, testMessage.Length, IpClient);

            try
            {

                using (CancellationTokenSource ResponseBuffer = new CancellationTokenSource(5000))
                {
                    UdpReceiveResult result = await ControlServer.ReceiveAsync(ResponseBuffer.Token);

                    string message = Encoding.UTF8.GetString(result.Buffer);

                    if (message == "ALIVE")
                    {
                        Console.WriteLine("\nClient still Alive");
                        
                    }     
                }
            }
            catch (OperationCanceledException)
            {
                
                cts?.Cancel();
                Console.WriteLine("\nClient disconnected!");
                
                break;
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"\nError during connection test: {ex.Message}");
            }
        }
    }
    async static Task Disconnect(CancellationToken token)
    {
        
        while (!token.IsCancellationRequested)
        {
            try
            {         
                UdpReceiveResult disconnect = await DisconnectServer.ReceiveAsync(token);

                string message = Encoding.UTF8.GetString(disconnect.Buffer);

                if (message == "DISCONNECT")
                {
                    cts?.Cancel();
                    Console.WriteLine("\nClient said Goodbye!");
                    
                    break;
                }
            }
            catch (OperationCanceledException )
            {
                break;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"\nFehler {ex}");
            }
        }  
    }

    async static Task ReceiveCommands(CancellationToken token)
    {
        var connection = Connection.Session;

        var spotifyObjectPath = new ObjectPath("/org/mpris/MediaPlayer2");
        var SpotifyService = "org.mpris.MediaPlayer2.spotify";

        var player = connection.CreateProxy<IMediaPlayer2Player>(SpotifyService, spotifyObjectPath);

        while(!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult command = await CommandServer.ReceiveAsync(token);

                string message = Encoding.UTF8.GetString(command.Buffer);

                if (message == "PLAY")
                {
                    await player.PlayAsync();
                }
                else if (message == "PAUSE")
                {
                    await player.PauseAsync();
                }
                else if (message == "NEXT")
                {
                    await player.NextAsync();
                }
                else if (message == "PREVIOUS")
                {
                    await player.PreviousAsync();
                }

            }
            catch (OperationCanceledException )
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n{ex}");
            }
        
    
        }
    }

    private static void ResetServers()
    {
        var player = connection.CreateProxy<IMediaPlayer2Player>(SpotifyService, spotifyObjectPath);
        player.PauseAsync();
        lock (serverLock)
        {
            try
            {
                // Alte Server schließen, um den Buffer zu leeren
                AudioServer?.Close();
                ControlServer?.Close();
                DisconnectServer?.Close();
                CommandServer?.Close();

                Thread.Sleep(50);
                // Neu instanziieren für die nächste Verbindung
                AudioServer = new UdpClient(5000);
                ControlServer = new UdpClient(5001);
                DisconnectServer = new UdpClient(5002);
                CommandServer = new UdpClient(5003);
            }
            catch { }
        }

        
    }
}

[DBusInterface("org.mpris.MediaPlayer2.Player")]
public interface IMediaPlayer2Player: IDBusObject
{
    Task NextAsync();
    Task PreviousAsync();
    Task PauseAsync();
    Task PlayPauseAsync();
    Task StopAsync();
    Task PlayAsync();
}


