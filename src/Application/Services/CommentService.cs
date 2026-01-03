using Comments.Application.Interfaces;
using Comments.Contracts;
using Comments.Infrastructure.Data;
using Comments.Models;
using Comments.Models.Enums;
using Comments.Models.Filters;
using Microsoft.EntityFrameworkCore;

namespace Comments.Application.Services
{
    public class CommentService : ICommentService
    {
        private readonly CommentsDbContext _dbContext;
        private readonly ImageService _imageService;
        private readonly TextFileService _textFileService;
        public CommentService(CommentsDbContext dbContext, ImageService imageService, TextFileService textFileService)
        {
            _dbContext = dbContext;
            _imageService = imageService;
            _textFileService = textFileService;
        }

        public async Task<CommentResponse> CreateCommentAsync(CommentRequest request, IFormFile? file = null)
        {
            if (request.ParentId.HasValue)
            {
                bool parentIdExists = await _dbContext.Comments.AnyAsync(c => c.Id == request.ParentId);
                if (!parentIdExists)
                {
                    throw new ArgumentException("ParentId does not exist");
                }
            }

            Guid? imageId = null;
            Guid? textFileId = null;
            string? originalTextFileName = null;

            if (file?.Length > 0)
            {
                var fileType = DetectFile(file);

                switch (fileType)
                {
                    case FileType.Image:
                        {
                            imageId = await _imageService.ProcessAndSaveImageAsync(file);
                            break;
                        }

                    case FileType.Text:
                        {
                            var (fileId, originalFileName) = await _textFileService.ProcessAndSaveTextFileAsync(file);
                            textFileId = fileId;
                            originalTextFileName = originalFileName;
                            break;
                        }

                    default:
                        throw new Exception("Unsupported file type");
                }
            }

            // Sanitize request fields
            var cleanText = InputSanitizationService.SanitizeComment(request.Text);
            var cleanUserName = InputSanitizationService.SanitizeUsername(request.UserName);
            var cleanEmail = InputSanitizationService.SanitizeEmail(request.Email);

            var comment = new Comment
            {
                ParentId = request.ParentId,
                UserName = cleanUserName,
                Email = cleanEmail,
                Text = cleanText,
                ImageId = imageId,
                TextFileId = textFileId,
                OriginalTextFileName = originalTextFileName
            };

            _dbContext.Add(comment);
            await _dbContext.SaveChangesAsync();

            var commentResponse = new CommentResponse
            {
                Id = comment.Id,
                UserName = comment.UserName,
                Text = comment.Text,
                CreatedAt = comment.CreatedAt.ToString("dd-MM-yyyy HH:mm"),
                ImagePreviewUrl = _imageService.GetImagePreviewUrl(comment.ImageId),
                ImageOriginalUrl = _imageService.GetImageOriginalUrl(comment.ImageId),
                TextFileUrl = _textFileService.GetTextFileUrl(comment.TextFileId),
                TextFileName = comment.OriginalTextFileName
            };
            Console.WriteLine("Created commentresponse:ImagePreviewUrl" + commentResponse.ImagePreviewUrl+ "ImageOriginalUrl: "+commentResponse.ImageOriginalUrl);
            return commentResponse;
        }

        public async Task<List<CommentResponse>> GetCommentsAsync(CommentQuery commentQuery, int? parentId = null)
        {
            if(commentQuery.PageSize>25 || commentQuery.PageSize<0)
                commentQuery.PageSize = 0;

            var comments = _dbContext.Comments.AsQueryable();

            // root comments
            if (parentId == null)
                comments = comments.Where(c => c.ParentId == null);
            // child comments
            else
                comments = comments.Where(c => c.ParentId == parentId); 

            comments = commentQuery.SortBy switch
            {
                CommentSortField.userName => commentQuery.Ascending
                    ? comments.OrderBy(c => c.UserName)
                    : comments.OrderByDescending(c => c.UserName),

                CommentSortField.email => commentQuery.Ascending
                    ? comments.OrderBy(c => c.Email)
                    : comments.OrderByDescending(c => c.Email),

                _ => commentQuery.Ascending // default LIFO
                    ? comments.OrderBy(c => c.CreatedAt)
                    : comments.OrderByDescending(c => c.CreatedAt) 
            };

            return await comments
                .Select(c => new CommentResponse
                {
                    Id = c.Id,
                    UserName = c.UserName,
                    Text = c.Text,
                    CreatedAt = c.CreatedAt.ToString("dd-MM-yyyy HH:mm"),
                    ImagePreviewUrl = _imageService.GetImagePreviewUrl(c.ImageId),
                    ImageOriginalUrl = _imageService.GetImageOriginalUrl(c.ImageId),
                    TextFileUrl = _textFileService.GetTextFileUrl(c.TextFileId),
                    TextFileName = c.OriginalTextFileName
                })
                .Skip(commentQuery.Skip)
                .Take(commentQuery.PageSize)
                .AsNoTracking()
                .ToListAsync();
        }

        private FileType DetectFile(IFormFile file)
        {
            var type = file.ContentType;

            if (type.StartsWith("image/"))
                return FileType.Image;

            if (type.StartsWith("text/"))
                return FileType.Text;

            return FileType.Unknown;
        }
    }
}
