using System.Text;

namespace Comments.Application.Services
{
    public class TextFileService
    {
        private readonly IWebHostEnvironment _environment;
        private const long MaxFileSize = 100 * 1024; // 100 KB
        private readonly string[] _allowedExtensions = { ".txt" };

        public TextFileService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<(Guid fileId, string originalFileName)> ProcessAndSaveTextFileAsync(IFormFile textFile)
        {
            if (textFile.Length > MaxFileSize)
            {
                throw new ArgumentException($"Размер файла не должен превышать {MaxFileSize / 1024}KB");
            }

            var extension = Path.GetExtension(textFile.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                throw new ArgumentException("Допустим только формат TXT");
            }

            // Reading the contents of the file
            string content;
            await using (var stream = textFile.OpenReadStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync();
            }

            // Check encoding and content
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Файл пустой или содержит только пробелы");
            }

            // Checking for dangerous contents
            if (ContainsDangerousContent(content))
            {
                throw new ArgumentException("Файл содержит запрещенное содержимое");
            }

            // create unique file name
            var fileId = Guid.NewGuid();

            // save original file name
            var originalFileName = textFile.FileName;

            var dir = Path.Combine(_environment.WebRootPath, "uploads", "textfiles");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var path = Path.Combine(dir, $"{fileId}{extension}");

            // Save file
            await using var fileStream = new FileStream(path, FileMode.Create);
            await using var writer = new StreamWriter(fileStream, Encoding.UTF8);
            await writer.WriteAsync(content);

            return (fileId, originalFileName);
        }

        private bool ContainsDangerousContent(string content)
        {
            var dangerousPatterns = new[]
            {
            "<script", "javascript:", "onload=", "onerror=", "onclick=",
            "<?php", "<%", "eval(", "document.cookie", "localStorage",
            "window.location", "alert(", "prompt(", "confirm("
        };

            var lowerContent = content.ToLowerInvariant();
            return dangerousPatterns.Any(pattern => lowerContent.Contains(pattern));
        }

        public string? GetTextFileUrl(Guid? fileId)
        {
            if (fileId is null)
                return null;

            return $"/uploads/textfiles/{fileId}.txt";
        }
    }
}
