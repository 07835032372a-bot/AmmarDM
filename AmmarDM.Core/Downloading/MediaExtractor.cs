using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace AmmarDM.Core.Downloading
{
    public class MediaFormatOption
    {
        public string Title { get; set; } = string.Empty;
        public string QualityLabel { get; set; } = string.Empty;
        public string Container { get; set; } = string.Empty;
        public string DirectUrl { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public bool IsAudioOnly { get; set; }

        public string DisplayLabel => IsAudioOnly 
            ? $"🎵 صوت MP3 / M4A ({QualityLabel}) - {Models.DownloadItem.FormatSize(SizeBytes)}"
            : $"🎬 فيديو MP4 ({QualityLabel}) - {Models.DownloadItem.FormatSize(SizeBytes)}";
    }

    public static class MediaExtractor
    {
        private static readonly YoutubeClient _youtubeClient = new();

        public static bool IsYouTubeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            return url.Contains("youtube.com/watch") || url.Contains("youtu.be/") || url.Contains("youtube.com/shorts");
        }

        public static async Task<(string Title, List<MediaFormatOption> Formats)> ExtractYouTubeFormatsAsync(string url)
        {
            var result = new List<MediaFormatOption>();
            try
            {
                var video = await _youtubeClient.Videos.GetAsync(url);
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(url);

                // 1. الفيديوهات المدمجة بالصوت والصورة معاً (Muxed Streams - جاهزة للتشغيل المباشر)
                var muxed = streamManifest.GetMuxedStreams().OrderByDescending(s => s.VideoQuality);
                foreach (var s in muxed)
                {
                    result.Add(new MediaFormatOption
                    {
                        Title = video.Title,
                        QualityLabel = s.VideoQuality.Label,
                        Container = s.Container.Name.ToLower(),
                        DirectUrl = s.Url,
                        SizeBytes = s.Size.Bytes,
                        IsAudioOnly = false
                    });
                }

                // 2. الفيديوهات بصيغة MP4 (Video-only) للجودات العالية مثل 1080p, 1440p, 4K
                var videoStreams = streamManifest.GetVideoOnlyStreams()
                    .Where(s => s.Container.Name.ToLower() == "mp4")
                    .OrderByDescending(s => s.VideoQuality)
                    .DistinctBy(s => s.VideoQuality.Label)
                    .Take(4);

                foreach (var s in videoStreams)
                {
                    // تجنب تكرار جودة موجودة بالفعل في المدمج
                    if (result.Any(r => !r.IsAudioOnly && r.QualityLabel.Contains(s.VideoQuality.Label))) continue;

                    result.Add(new MediaFormatOption
                    {
                        Title = video.Title,
                        QualityLabel = $"{s.VideoQuality.Label} High",
                        Container = "mp4",
                        DirectUrl = s.Url,
                        SizeBytes = s.Size.Bytes,
                        IsAudioOnly = false
                    });
                }

                // 3. مسارات الصوت (Audio Only) للأغاني والمحاضرات (MP3 / M4A)
                var audioStreams = streamManifest.GetAudioOnlyStreams()
                    .OrderByDescending(s => s.Bitrate)
                    .Take(2);

                foreach (var s in audioStreams)
                {
                    var kbps = (int)Math.Round(s.Bitrate.KiloBitsPerSecond);
                    result.Add(new MediaFormatOption
                    {
                        Title = video.Title,
                        QualityLabel = $"{kbps} kbps",
                        Container = "mp3",
                        DirectUrl = s.Url,
                        SizeBytes = s.Size.Bytes,
                        IsAudioOnly = true
                    });
                }

                return (video.Title, result);
            }
            catch
            {
                return (string.Empty, result);
            }
        }
    }
}
