using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TradeStorm.Api.DTOs;
using TradeStorm.Api.Models;
using TradeStorm.Api.Repositories;
using TradeStorm.Api.Services;
using Xunit;

namespace TradeStorm.Api.Tests;

public class VideoListingSupportTests
{
    private static IFormFile CreateMockVideoFile(string fileName, string contentType, byte[] headerBytes, long totalLength = 1024)
    {
        var memoryStream = new MemoryStream();
        memoryStream.Write(headerBytes, 0, headerBytes.Length);
        if (totalLength > headerBytes.Length)
        {
            var remaining = new byte[totalLength - headerBytes.Length];
            memoryStream.Write(remaining, 0, remaining.Length);
        }
        memoryStream.Position = 0;

        return new FormFile(memoryStream, 0, memoryStream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private class OversizedFormFile : IFormFile
    {
        public string ContentType => "video/mp4";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => 105 * 1024 * 1024; // 105 MB
        public string Name => "file";
        public string FileName => "oversized.mp4";
        public void CopyTo(Stream target) { }
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Stream OpenReadStream() => new MemoryStream(new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 });
    }

    [Fact]
    public void ValidateVideo_ValidMp4_ReturnsSuccess()
    {
        // MP4 header: 4 bytes length, then 'ftyp'
        var mp4Header = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32 };
        var file = CreateMockVideoFile("brand_showcase.mp4", "video/mp4", mp4Header);

        var (isValid, errorMessage) = FileUploadValidator.ValidateVideo(file);

        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ValidateVideo_ValidWebm_ReturnsSuccess()
    {
        // WebM header: 1A 45 DF A3
        var webmHeader = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0xF7, 0x7B };
        var file = CreateMockVideoFile("product_review.webm", "video/webm", webmHeader);

        var (isValid, errorMessage) = FileUploadValidator.ValidateVideo(file);

        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ValidateVideo_DisallowedExtension_ReturnsError()
    {
        var fakeHeader = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 };
        var file = CreateMockVideoFile("malicious.exe", "video/mp4", fakeHeader);

        var (isValid, errorMessage) = FileUploadValidator.ValidateVideo(file);

        Assert.False(isValid);
        Assert.Contains("Непідтримуваний формат відео", errorMessage);
    }

    [Fact]
    public void ValidateVideo_InvalidSignature_ReturnsError()
    {
        var textBytes = Encoding.UTF8.GetBytes("This is definitely not a real mp4 video file");
        var file = CreateMockVideoFile("fake_video.mp4", "video/mp4", textBytes);

        var (isValid, errorMessage) = FileUploadValidator.ValidateVideo(file);

        Assert.False(isValid);
        Assert.Contains("сигнатура не розпізнана", errorMessage);
    }

    [Fact]
    public void ValidateVideo_Exceeds100MbLimit_ReturnsError()
    {
        var file = new OversizedFormFile();

        var (isValid, errorMessage) = FileUploadValidator.ValidateVideo(file);

        Assert.False(isValid);
        Assert.Contains("100 МБ", errorMessage);
    }

    private class FakeListingRepository : IListingRepository
    {
        public readonly List<Listing> Store = new();

        public Task<Listing> AddAsync(Listing entity)
        {
            entity.Id = Store.Count + 1;
            Store.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<Listing?> GetByIdAsync(int id) =>
            Task.FromResult(Store.FirstOrDefault(l => l.Id == id));

        public Task<Listing?> GetByIdWithDetailsAsync(int id) =>
            Task.FromResult(Store.FirstOrDefault(l => l.Id == id));

        public Task UpdateAsync(Listing entity) => Task.CompletedTask;
        public Task DeleteAsync(Listing entity) { Store.Remove(entity); return Task.CompletedTask; }
        public Task<IReadOnlyList<Listing>> GetAllAsync() => Task.FromResult<IReadOnlyList<Listing>>(Store);
        public Task<IReadOnlyList<Listing>> FindAsync(Expression<Func<Listing, bool>> predicate) =>
            Task.FromResult<IReadOnlyList<Listing>>(Store.AsQueryable().Where(predicate).ToList());
        public Task<int> CountAsync(Expression<Func<Listing, bool>>? predicate = null) =>
            Task.FromResult(predicate == null ? Store.Count : Store.AsQueryable().Count(predicate));
        public IQueryable<Listing> Query() => Store.AsQueryable();

        public Task<(int TotalCount, IReadOnlyList<object> Items)> GetPagedListingsAsync(
            string? search, int? categoryId, decimal? minPrice, decimal? maxPrice,
            string? condition, int? sellerId, string? status, string? sortBy, int? page, int? pageSize)
        {
            return Task.FromResult((Store.Count, (IReadOnlyList<object>)Store.Cast<object>().ToList()));
        }
    }

    [Fact]
    public async Task ListingService_CreateAndEdit_PersistsVideoUrl()
    {
        // Arrange
        var fakeRepo = new FakeListingRepository();
        var service = new ListingService(fakeRepo);

        var createDto = new CreateListingDto
        {
            Title = "Ексклюзивна сукня від бренду",
            Description = "Фірмовий брендовий одяг з якісних матеріалів",
            Price = 2500,
            CategoryId = 1,
            Location = "Київ",
            VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
        };

        // Act
        var created = await service.CreateListingAsync(42, createDto);

        // Assert
        Assert.NotNull(created);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", created.VideoUrl);

        // Test Update
        var updateDto = new UpdateListingDto
        {
            Title = "Ексклюзивна сукня від бренду (оновлено)",
            Description = "Опис оновлено",
            Price = 2700,
            CategoryId = 1,
            VideoUrl = "/uploads/videos/new_brand_clip.mp4"
        };

        var (success, error, updated) = await service.UpdateListingAsync(created.Id, 42, updateDto);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(updated);
        Assert.Equal("/uploads/videos/new_brand_clip.mp4", updated.VideoUrl);
    }
}
