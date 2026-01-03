using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Comments.Application.Services
{
    public class ImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private const int MaxWidth = 320;
        private const int MaxHeight = 240;
        private const long MaxFileSize = 5 * 1024 * 1024;

        public ImageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<Guid> ProcessAndSaveImageAsync(IFormFile imageFile)
        {
            // checking file size
            if (imageFile.Length > MaxFileSize)
            {
                throw new ArgumentException($"The file size must not exceed {MaxFileSize / 1024 / 1024}MB");
            }

            // checking file extension
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                throw new ArgumentException("Acceptable formats: JPG, GIF, PNG");
            }

            var imageId = Guid.NewGuid();
            //original image path
            var nameOriginal = $"{imageId}{extension}";
            var originalDir = Path.Combine(_environment.WebRootPath, "uploads", "images", "original");

            if (!Directory.Exists(originalDir))
            {
                Directory.CreateDirectory(originalDir);
            }

            var originalPath = Path.Combine(originalDir, nameOriginal);

            //preview image path
            var namePreview = $"{imageId}_preview{extension}";
            var previewDir = Path.Combine(_environment.WebRootPath, "uploads", "images", "preview");

            if (!Directory.Exists(previewDir))
            {
                Directory.CreateDirectory(previewDir);
            }
            var previewPath = Path.Combine(previewDir, namePreview);

            try
            {
                await using var imageStream = imageFile.OpenReadStream();
                using var image = await Image.LoadAsync(imageStream);
                await using var originalStream = new FileStream(originalPath, FileMode.Create);

                await SaveImageAsync(image, originalStream, extension);

                if (image.Width > MaxWidth && image.Height > MaxHeight)
                {
                    using var previewImage = image.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();

                    ResizeImage(previewImage);
                  
                    await using var previewStream = new FileStream(previewPath, FileMode.Create);
                    await SaveImageAsync(previewImage, previewStream, extension);
                }
                
                return imageId;
            }
            catch (Exception ex)
            {
                // If there is an error, delete the partially created file
                if (File.Exists(originalPath))
                    File.Delete(originalPath);

                if (File.Exists(previewPath))
                    File.Delete(previewPath);

                throw new ArgumentException($"Ошибка обработки изображения: {ex.Message}");//залогировать исключение и отдавать общее на клиента
            }
        }

        private void ResizeImage(Image image)
        {
            var ratioX = (double)MaxWidth / image.Width;
            var ratioY = (double)MaxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            image.Mutate(x => x.Resize(newWidth, newHeight));
        }

        private async Task SaveImageAsync(Image image, Stream stream, string extension)
        {
            IImageEncoder encoder = extension.ToLower() switch
            {
                ".jpg" or ".jpeg" => new JpegEncoder { Quality = 80 },
                ".png" => new PngEncoder(),
                ".gif" => new GifEncoder(),
                _ => new JpegEncoder { Quality = 80 }
            };

            await image.SaveAsync(stream, encoder);
        }

        public string? GetImagePreviewUrl(Guid? imageId)
        {
            if (imageId == null)
                return null;

            var previewFileName = GetPreviewFileName(imageId.Value);

            return previewFileName != null
                ? $"/uploads/images/preview/{previewFileName}"
                : GetImageOriginalUrl(imageId);
        }

        public string? GetImageOriginalUrl(Guid? imageId)
        {
            if(imageId == null) 
                return null;

            var originalPath = Path.Combine(_environment.WebRootPath, "uploads", "images", "original");

            if (!Directory.Exists(originalPath))
                return null;


            var file = Directory.EnumerateFiles(originalPath, $"{imageId}.*")
                .FirstOrDefault();

            return file is null
                ? null
                : $"/uploads/images/original/{Path.GetFileName(file)}";
        }

        private string? GetPreviewFileName(Guid imageId)
        {
            var previewPath = Path.Combine(_environment.WebRootPath, "uploads", "images", "preview");

            if (!Directory.Exists(previewPath))
                return null;

            var file = Directory.EnumerateFiles(previewPath, $"{imageId}_preview.*")
                .FirstOrDefault();
            return file is null ? null : Path.GetFileName(file);
        }
    }
}