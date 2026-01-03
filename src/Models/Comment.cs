namespace Comments.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public List<Comment> Children { get; set; } = new();
        public Comment? Parent { get; set; } // Navigation property for parent comment
        public string UserName { get; set; }=string.Empty;
        public string Email { get; set; }=string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public Guid? ImageId { get; set; }
        public Guid? TextFileId { get; set; }
        public string? OriginalTextFileName { get; set; }

        public Comment() { }

        public Comment(int? parentId, string userName, string email, string text, string imagePath, Guid imageId, Guid textFileId, string originalTextFileName)
        {
            ParentId = parentId;
            SetUserName(userName);
            SetEmail(email);
            SetText(text);
            ImageId = imageId;
            TextFileId = textFileId;
            OriginalTextFileName = originalTextFileName;
        }

        // Constructor for seeding data with specific IDs
        public Comment(int id, int? parentId, string userName, string email, string text, string? imageId=null)
        {
            Id = id;
            ParentId = parentId;
            SetUserName(userName);
            SetEmail(email);
            SetText(text);
            ImageId = string.IsNullOrWhiteSpace(imageId) ? null : Guid.Parse(imageId);
        }

        private void SetUserName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName) || userName.Length < 3 || userName.Length > 20)
                throw new ArgumentException("Invalid username");

            UserName = userName;
        }

        private void SetEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Invalid email");

            Email = email;
        }

        private void SetText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 2000)
                throw new ArgumentException("Invalid text");

            Text = text;
        }
    }
}
