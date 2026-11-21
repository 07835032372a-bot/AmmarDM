using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AmmarDM.UI.Services
{
    public static class LocalHttpServer
    {
        private static HttpListener? _listener;
        private static CancellationTokenSource? _cts;
        public const int Port = 57891;

        public static void Start(Action<NativeHostMessage> onMessageReceived)
        {
            if (_listener != null && _listener.IsListening) return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");

            try
            {
                _listener.Start();
                Task.Run(() => ListenLoop(onMessageReceived, _cts.Token));
            }
            catch { }
        }

        private static async Task ListenLoop(Action<NativeHostMessage> onMessageReceived, CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequestAsync(ctx, onMessageReceived));
                }
                catch { }
            }
        }

        private static async Task ProcessRequestAsync(HttpListenerContext ctx, Action<NativeHostMessage> onMessageReceived)
        {
            // تمكين CORS لطلبات المتصفح
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            ctx.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.Close();
                return;
            }

            try
            {
                if (ctx.Request.Url?.AbsolutePath == "/settings")
                {
                    // إرجاع الإعدادات الحالية للامتدادات وقائمة التجاهل للمتصفح
                    var settingsJson = JsonSerializer.Serialize(new
                    {
                        extensions = App.Settings.CapturedExtensions.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries),
                        ignoredSites = App.Settings.IgnoredSites.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries),
                        captureDownloads = App.Settings.CaptureBrowserDownloads,
                        showVideoPanel = App.Settings.ShowVideoDownloadPanel
                    });

                    var buffer = Encoding.UTF8.GetBytes(settingsJson);
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = buffer.Length;
                    await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else if (ctx.Request.Url?.AbsolutePath == "/extract_media")
                {
                    // استخراج جودات وصيغ اليوتيوب والفيديوهات لحظياً للمتصفح بشكل غير متزامن
                    using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync();
                    var reqData = JsonSerializer.Deserialize<JsonElement>(body);
                    var pageUrl = reqData.GetProperty("url").GetString() ?? "";

                    try
                    {
                        var (title, formats) = await AmmarDM.Core.Downloading.MediaExtractor.ExtractYouTubeFormatsAsync(pageUrl);
                        var respJson = JsonSerializer.Serialize(new
                        {
                            title = title,
                            formats = formats
                        });

                        var buffer = Encoding.UTF8.GetBytes(respJson);
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.ContentLength64 = buffer.Length;
                        await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    catch { }
                }
                else if (ctx.Request.Url?.AbsolutePath == "/download_youtube")
                {
                    // استقبال طلب تنزيل يوتيوب بفهرس الصيغة - يُعيد توليد الرابط الحي لحظياً
                    using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync();
                    var reqData = JsonSerializer.Deserialize<JsonElement>(body);
                    var pageUrl = reqData.GetProperty("PageUrl").GetString() ?? "";
                    var formatIndex = reqData.GetProperty("FormatIndex").GetInt32();
                    var fileName = reqData.GetProperty("FileName").GetString() ?? "video.mp4";

                    // رد فوري للمتصفح حتى لا يتجمد
                    var okBytes = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = okBytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(okBytes, 0, okBytes.Length);
                    ctx.Response.Close();

                    // استخراج رابط حي جديد في الخلفية ثم إرساله للتنزيل
                    var capturedCallback = onMessageReceived;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var (title, formats) = await AmmarDM.Core.Downloading.MediaExtractor.ExtractYouTubeFormatsAsync(pageUrl);
                            if (formats != null && formatIndex >= 0 && formatIndex < formats.Count)
                            {
                                var format = formats[formatIndex];
                                var msg = new NativeHostMessage
                                {
                                    Action = "add_download",
                                    Url = format.DirectUrl,
                                    FileName = fileName,
                                    Referrer = pageUrl,
                                    TotalBytes = format.SizeBytes
                                };
                                capturedCallback(msg);
                            }
                        }
                        catch { }
                    });
                    return; // تم إغلاق الاستجابة بالفعل
                }
                else if (ctx.Request.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync();
                    var msg = JsonSerializer.Deserialize<NativeHostMessage>(body);

                    var responseMsg = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = responseMsg.Length;
                    await ctx.Response.OutputStream.WriteAsync(responseMsg, 0, responseMsg.Length);

                    if (msg != null && !string.IsNullOrEmpty(msg.Url))
                    {
                        _ = Task.Run(() => onMessageReceived(msg));
                    }
                }
                else
                {
                    var responseMsg = Encoding.UTF8.GetBytes("Ammar DM API Server Online");
                    await ctx.Response.OutputStream.WriteAsync(responseMsg, 0, responseMsg.Length);
                }
            }
            catch { }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }

        public static void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
        }
    }
}
