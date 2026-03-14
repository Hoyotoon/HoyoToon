using System.Collections.Generic;

namespace HoyoToon.Editor.ResourceSystem
{
    public class DownloadProgress
    {
        public string GameKey { get; set; }
        public string CurrentFile { get; set; }
        public int FilesCompleted { get; set; }
        public int TotalFiles { get; set; }
        public float OverallProgress => TotalFiles > 0 ? (float)FilesCompleted / TotalFiles : 0f;
        public string StatusMessage { get; set; }
        public bool HasErrors { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}
