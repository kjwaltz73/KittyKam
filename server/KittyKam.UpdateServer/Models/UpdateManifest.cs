namespace UpdateServer.Models
{
    public class UpdateManifest
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string Sha256Hash { get; set; }
        public long FileSize { get; set; }
        public DateTime ReleasedUtc { get; set; }
        public string ReleaseNotes { get; set; }
        public bool IsRequired { get; set; }
    }
}