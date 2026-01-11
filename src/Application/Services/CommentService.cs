using Comments.Application.Interfaces;
using Comments.Contracts;
using Comments.Infrastructure.Data;
using Comments.Infrastructure.Logging;
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
        private readonly ILogger<CommentService> _logger;
        public CommentService(CommentsDbContext dbContext, ImageService imageService, TextFileService textFileService, ILogger<CommentService> logger)
        {
            _dbContext = dbContext;
            _imageService = imageService;
            _textFileService = textFileService;
            _logger = logger;
        }

        public async Task<CommentResponse> CreateCommentAsync(CommentRequest request, IFormFile? file = null)
        {
            if (request.ParentId.HasValue)
            {
                bool parentIdExists = await _dbContext.Comments.AnyAsync(c => c.Id == request.ParentId);
                if (!parentIdExists)
                {
                    throw new KeyNotFoundException("Root comment not found");
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
                        throw new ArgumentException("Unsupported file type");
                }
            }

            // Sanitize request fields
            var cleanText = InputSanitizationService.SanitizeComment(request.Text);
            var cleanUserName = InputSanitizationService.SanitizeUsername(request.UserName);
            var cleanEmail = InputSanitizationService.SanitizeEmail(request.Email);

            var comment = new Comment(
                request.ParentId,
                cleanUserName,
                cleanEmail,
                cleanText,
                imageId,
                textFileId,
                originalTextFileName
            );


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

            _logger.LogAuditUser(
                "Created comment {CommentId} by {UserName}, HasFile: {HasFile}, ParentId: {ParentId}",
                commentResponse.Id,
                commentResponse.UserName,
                file != null,
                request.ParentId);

            return commentResponse;
        }

        public async Task<List<CommentResponse>> GetCommentsAsync(CommentQuery commentQuery, int? parentId = null)
        {
            if (commentQuery.PageSize is < 1 or > 25)
                throw new ArgumentException("PageSize must be between 1 and 25");

            var comments = _dbContext.Comments.AsNoTracking()
                .AsQueryable();

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

           var commentsResponse = await comments
                .Skip(commentQuery.Skip)
                .Take(commentQuery.PageSize)
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
                .ToListAsync();

            return commentsResponse;
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
