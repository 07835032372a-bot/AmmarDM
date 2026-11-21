using System;
using System.Collections.Generic;

namespace AmmarDM.Core.Models
{
    public class UpdateInfo
    {
        public string Version { get; set; } = "1.0.0";
        public string ReleaseDate { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public List<string> Changelog { get; set; } = new();
        public bool IsMandatory { get; set; }
    }
}
