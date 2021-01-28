namespace Artify.Models
{
    public class Settings
    {
        public string last_used_savepath { get; set; }
        public string pixiv_refresh_token { get; set; }
        public int concurrent_tasks { get; set; }
        public int download_attempts { get; set; }
        public int timeout { get; set; }
    }
}
