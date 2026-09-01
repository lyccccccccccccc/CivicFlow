using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace CivicFlow.Api.Common;

public sealed record ValidatedAttachment(Stream Content, string FileName, string ContentType, long SizeBytes, string Sha256);

public static class AttachmentFileValidator
{
    public const long MaximumBytes = 10 * 1024 * 1024;
    private const long MaximumPixels = 40_000_000;
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    { [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".pdf"] = "application/pdf" };

    public static async Task<ValidatedAttachment> ValidateAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaximumBytes) throw new InvalidOperationException("File must be between 1 byte and 10 MB.");
        var fileName = SafeFileName(file.FileName); var extension = Path.GetExtension(fileName);
        if (!Types.TryGetValue(extension, out var expected) || !string.Equals(expected, file.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only JPG, PNG and PDF files with matching content types are supported.");

        var content = new MemoryStream((int)file.Length);
        await using (var input = file.OpenReadStream())
        {
            var buffer = new byte[81920]; int read; long total = 0;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read; if (total > MaximumBytes) throw new InvalidOperationException("File exceeds the 10 MB limit.");
                await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        content.Position = 0;
        var header = new byte[Math.Min(8, content.Length)]; await content.ReadExactlyAsync(header, cancellationToken); content.Position = 0;
        var signatureMatches = expected switch
        {
            "image/jpeg" => header.Length >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
            "image/png" => header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "application/pdf" => header.Length >= 5 && Encoding.ASCII.GetString(header, 0, 5) == "%PDF-",
            _ => false
        };
        if (!signatureMatches) throw new InvalidOperationException("File signature does not match its declared type.");
        if (expected.StartsWith("image/", StringComparison.Ordinal))
        {
            // SKCodec owns and closes streams supplied to Create(Stream). Decode a copy so the
            // validated stream remains readable for hashing and the storage provider.
            using var imageData = SKData.CreateCopy(content.ToArray());
            using var codec = SKCodec.Create(imageData);
            var info = codec?.Info;
            if (info is null || (long)info.Value.Width * info.Value.Height > MaximumPixels)
                throw new InvalidOperationException("Image cannot be decoded or exceeds the 40 megapixel safety limit.");
            content.Position = 0;
        }
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken)).ToLowerInvariant(); content.Position = 0;
        return new(content, fileName, expected, content.Length, digest);
    }

    public static string SafeFileName(string input)
    {
        var leaf = Path.GetFileName(input.Replace('\\', '/')).Normalize(NormalizationForm.FormKC);
        var clean = new string(leaf.Where(c =>
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            return category is not (UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse) && c is not ('/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|');
        }).ToArray()).Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(clean)) clean = "attachment";
        return clean.Length <= 255 ? clean : clean[..255];
    }
}
