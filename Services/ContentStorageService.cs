using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartBets.Data;
using SmartBets.Entities;

namespace SmartBets.Services;

public class ContentStorageService
{
    private const string EmptyArrayJson = "[]";
    private readonly AppDbContext _dbContext;

    public ContentStorageService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GetPayloadJsonAsync(
        string contentKey,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = await _dbContext.ContentDocuments
            .AsNoTracking()
            .Where(x => x.ContentKey == contentKey)
            .Select(x => x.PayloadJson)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(payloadJson)
            ? EmptyArrayJson
            : payloadJson;
    }

    public async Task<string> UpsertArrayPayloadAsync(
        string contentKey,
        JsonElement payload,
        string? updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (payload.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Payload must be a JSON array.", nameof(payload));

        var normalizedPayloadJson = payload.GetRawText();
        var nowUtc = DateTime.UtcNow;

        var document = await _dbContext.ContentDocuments
            .SingleOrDefaultAsync(x => x.ContentKey == contentKey, cancellationToken);

        if (document is null)
        {
            document = new ContentDocument
            {
                ContentKey = contentKey,
                PayloadJson = normalizedPayloadJson,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                UpdatedBy = NormalizeUpdatedBy(updatedBy)
            };

            _dbContext.ContentDocuments.Add(document);
        }
        else
        {
            document.PayloadJson = normalizedPayloadJson;
            document.UpdatedAtUtc = nowUtc;
            document.UpdatedBy = NormalizeUpdatedBy(updatedBy);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return normalizedPayloadJson;
    }

    private static string? NormalizeUpdatedBy(string? updatedBy)
    {
        return string.IsNullOrWhiteSpace(updatedBy)
            ? null
            : updatedBy.Trim();
    }
}
