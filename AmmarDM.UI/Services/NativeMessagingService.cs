using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AmmarDM.UI.Services
{
    public class NativeHostMessage
    {
        public string? Action { get; set; }
        public string? Url { get; set; }
        public string? FileName { get; set; }
        public string? Referrer { get; set; }
        public long TotalBytes { get; set; }
    }

    public static class NativeMessagingService
    {
        public const string HostName = "com.ammardm.downloader";
        public const string PipeName = "AmmarDM_IPC_Pipe";

        /// <summary>
        /// تسجيل الـ Native Messaging Host في سجل النظام (Windows Registry) لمتصفحي Chrome و Edge
        /// </summary>
        public static void RegisterHost(string exePath)
        {
            try
            {
                var hostDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AmmarDM");
                Directory.CreateDirectory(hostDir);
                var manifestPath = Path.Combine(hostDir, "com.ammardm.downloader.json");

                var manifestContent = new
                {
                    name = HostName,
                    description = "Ammar DM Native Messaging Host",
                    path = exePath,
                    type = "stdio",
                    allowed_origins = new[]
                    {
                        "chrome-extension://*",
                        "edge-extension://*"
                    }
                };

                File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifestContent, new JsonSerializerOptions { WriteIndented = true }));

                // تسجيل في Chrome
                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Google\Chrome\NativeMessagingHosts\{HostName}"))
                {
                    key.SetValue("", manifestPath);
                }

                // تسجيل في Edge
                using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Edge\NativeMessagingHosts\{HostName}"))
                {
                    key.SetValue("", manifestPath);
                }
            }
            catch { }
        }

        /// <summary>
        /// تشغيل نمط Native Host عندما يطلقه المتصفح عبر stdio
        /// </summary>
        public static void RunAsNativeHost()
        {
            try
            {
                using var stdin = Console.OpenStandardInput();
                var lengthBytes = new byte[4];
                while (stdin.Read(lengthBytes, 0, 4) == 4)
                {
                    int length = BitConverter.ToInt32(lengthBytes, 0);
                    if (length <= 0) break;

                    var buffer = new byte[length];
                    int read = 0;
                    while (read < length)
                    {
                        int r = stdin.Read(buffer, read, length - read);
                        if (r <= 0) break;
                        read += r;
                    }

                    var json = Encoding.UTF8.GetString(buffer, 0, read);
                    // إرسال البيانات إلى التطبيق الرئيسي المفتوح عبر Named Pipe
                    SendToRunningApp(json);
                }
            }
            catch { }
        }

        private static void SendToRunningApp(string json)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(1000);
                using var writer = new StreamWriter(client, Encoding.UTF8);
                writer.WriteLine(json);
                writer.Flush();
            }
            catch { }
        }

        /// <summary>
        /// الاستماع لرسائل المتصفح داخل نافذة البرنامج الرئيسية
        /// </summary>
        public static void StartIpcServer(Action<NativeHostMessage> onMessageReceived, CancellationToken token)
        {
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                        await server.WaitForConnectionAsync(token);
                        using var reader = new StreamReader(server, Encoding.UTF8);
                        var line = await reader.ReadLineAsync(token);
                        if (!string.IsNullOrEmpty(line))
                        {
                            var msg = JsonSerializer.Deserialize<NativeHostMessage>(line);
                            if (msg != null)
                            {
                                onMessageReceived(msg);
                            }
                        }
                    }
                    catch { }
                }
            }, token);
        }
    }
}
