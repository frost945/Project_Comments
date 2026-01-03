namespace Comments.Contracts
{
    public class CommentResponse
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string? ImagePreviewUrl { get; set; }
        public string? ImageOriginalUrl { get; set; }
        public string? TextFileUrl { get; set; }
        public string? TextFileName { get; set; }
        public CommentResponse() { }
        public CommentResponse(int id, string userName, string text, DateTime createdAt, string imagePreviewUrl, string imageOriginalUrl, string textFileUrl, string textFileName)
        {
            Id = id;
            UserName = userName;
            Text = text;
            CreatedAt = createdAt.ToLocalTime().ToString("dd-MM-yyyy HH:mm");
            ImagePreviewUrl = imagePreviewUrl;
            ImageOriginalUrl = imageOriginalUrl;
            TextFileUrl = textFileUrl;
            TextFileName = textFileName;
        }
    }
}
