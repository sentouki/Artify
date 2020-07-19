namespace ArtAPI
{
    /// <summary>
    /// container for Image
    /// </summary>
    public class ImageModel
    {
        /// <summary>
        /// Download URL for the image
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// name of the image
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// platform-specific(Artstation, Deviantart, Pixiv...etc) ID of the image
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// file type of the image (png, jpg, gif)
        /// </summary>
        public string FileType { get; set; } = "jpg";
    }
}
