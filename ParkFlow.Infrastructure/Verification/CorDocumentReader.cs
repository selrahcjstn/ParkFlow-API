using System.Buffers.Binary;
using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using ParkFlow.Application.Interfaces;
using ParkFlow.Infrastructure.Cloudinary;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Tokens;

namespace ParkFlow.Infrastructure.Verification;

public class CorDocumentReader(IOptions<OcrOptions> options, IOptions<CloudinarySettings> cloud, ILogger<CorDocumentReader>? logger = null) : ICorDocumentReader
{
    private readonly OcrOptions limits = options.Value;
    public bool IsStoredDocument(string url)
    {
        if (url.StartsWith("uploads/parkflow/cor/", StringComparison.Ordinal) && !url.Contains("..") && !url.Contains('\\') && !url.Contains('%'))
            return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "res.cloudinary.com" &&
            !string.IsNullOrWhiteSpace(cloud.Value.CloudName) && uri.AbsolutePath.StartsWith("/" + cloud.Value.CloudName + "/", StringComparison.Ordinal) &&
            uri.AbsolutePath.Contains("/parkflow/cor/", StringComparison.Ordinal) && string.IsNullOrEmpty(uri.Query);
    }

    public async Task ValidateAsync(Stream stream, string fileName, CancellationToken ct)
    {
        var bytes = await ReadBounded(stream, ct);
        using var document = Validate(bytes, fileName, ct);
    }

    public async Task<DocumentText?> ReadTextLayerAsync(Stream stream, string fileName, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew(); var outcome = "Failed";
        try
        {
            var bytes = await ReadBounded(stream, ct);
            var prepared = Prepare(bytes, fileName, ct);
            outcome = prepared.IsPdf && prepared.Scans.Count == 0 ? "DigitalPdfTextExtracted" : "BackgroundOcrRequired";
            return outcome == "DigitalPdfTextExtracted" ? new(prepared.Text, outcome) : null;
        }
        finally { logger?.LogInformation("COR text-layer check {Outcome} in {DurationMs} ms", outcome, watch.Elapsed.TotalMilliseconds); }
    }

    public async Task<DocumentText> ReadAsync(Stream stream, string fileName, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew(); var outcome = "Failed";
        try
        {
            var result = await ReadDocumentAsync(stream, fileName, ct); outcome = result.Outcome; return result;
        }
        finally { logger?.LogInformation("COR extraction {Outcome} in {DurationMs} ms", outcome, watch.Elapsed.TotalMilliseconds); }
    }

    private async Task<DocumentText> ReadDocumentAsync(Stream stream, string fileName, CancellationToken ct)
    {
        var bytes = await ReadBounded(stream, ct);
        var prepared = Prepare(bytes, fileName, ct);
        var isPdf = prepared.IsPdf; var scans = prepared.Scans;
        var text = new StringBuilder(prepared.Text);
        if (isPdf && scans.Count == 0) return new(prepared.Text, "DigitalPdfTextExtracted");

        var directory = Path.Combine(Path.GetTempPath(), "parkflow-ocr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var input = Path.Combine(directory, "input" + Path.GetExtension(fileName).ToLowerInvariant());
            await File.WriteAllBytesAsync(input, bytes, ct);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(input, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            if (isPdf)
            {
                foreach (var page in scans)
                {
                    var imagePath = Path.Combine(directory, "page");
                    await Run(limits.PdfRendererPath, ["-f", page.ToString(), "-l", page.ToString(), "-singlefile", "-scale-to", "2500", "-png", input, imagePath], directory, ct, "PdfRender");
                    Append(await Recognize(imagePath + ".png", directory, ct));
                    File.Delete(imagePath + ".png");
                }
                return new(text.ToString(), "ScannedPdfOcrCompleted");
            }
            Append(await Recognize(input, directory, ct));
            return new(text.ToString(), "ImageOcrCompleted");
        }
        finally { Directory.Delete(directory, recursive: true); }

        void Append(string value)
        {
            if (text.Length + value.Length + Environment.NewLine.Length > limits.MaxTextCharacters) throw new DocumentValidationException("Document contains too much text. Upload only the required pages.");
            text.AppendLine(value);
        }
    }

    private (bool IsPdf, string Text, List<int> Scans) Prepare(byte[] bytes, string fileName, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        using var pdf = Validate(bytes, fileName, ct);
        var scans = new List<int>();
        if (pdf == null) return (false, "", scans);
        var text = new StringBuilder(); var blankPages = 0;
        foreach (var page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            // Skip only pages proven to contain no text, images or drawn paths.
            if (page.Letters.Count == 0 && page.NumberOfImages == 0 && page.Paths.Count == 0) { blankPages++; continue; }
            var pageText = ContentOrderTextExtractor.GetText(page);
            var hasText = pageText.Count(char.IsLetterOrDigit) >= 30;
            // A small university logo must not force OCR of a digital document.
            // Large images can contain scanned identity fields or the class table.
            var scanImage = page.NumberOfImages > 0 && page.GetImages().Any(image =>
                image.BoundingBox.Width * image.BoundingBox.Height >= page.Width * page.Height * .2);
            var incompleteImage = false;
            if (hasText && scanImage)
            {
                var fields = ParkFlow.Application.Features.Verification.CorTextVerification.Extract(pageText);
                incompleteImage = fields.Name == null || fields.StudentNumber == null || fields.Course == null ||
                    fields.AcademicYear == null || fields.Semester == null || fields.ScheduleIssues.Count > 0;
            }
            if (!hasText || incompleteImage) scans.Add(page.Number);
            else
            {
                if (text.Length + pageText.Length + Environment.NewLine.Length > limits.MaxTextCharacters) throw new DocumentValidationException("Document contains too much text. Upload only the required pages.");
                text.AppendLine(pageText);
            }
        }
        logger?.LogInformation("COR PDF text layer in {DurationMs} ms; pages {PageCount}, OCR pages {OcrPageCount}, blank pages skipped {BlankPageCount}",
            watch.Elapsed.TotalMilliseconds, pdf.NumberOfPages, scans.Count, blankPages);
        return (true, text.ToString(), scans);
    }

    private async Task<string> Recognize(string input, string directory, CancellationToken ct)
    {
        var args = new List<string> { input, "stdout", "-l", limits.Language, "--psm", "6" };
        if (!string.IsNullOrWhiteSpace(limits.TessdataDirectory)) args.AddRange(["--tessdata-dir", limits.TessdataDirectory]);
        return await Run(limits.TesseractPath, args, directory, ct, "Ocr");
    }

    private async Task<string> Run(string executable, IEnumerable<string> args, string directory, CancellationToken ct, string stage)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(limits.ProcessTimeoutSeconds));
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory };
        start.Environment["OMP_THREAD_LIMIT"] = "1";
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        var started = false;
        var watch = Stopwatch.StartNew();
        try
        {
            if (!process.Start()) throw new IOException("OCR process unavailable.");
            started = true;
            var output = ReadOutput(process.StandardOutput, limits.MaxTextCharacters, timeout.Token);
            // Drain stderr with a bound; never expose native errors containing document data.
            var errors = ReadOutput(process.StandardError, 16_384, timeout.Token);
            var exited = process.WaitForExitAsync(timeout.Token);
            while (!exited.IsCompleted)
            {
                await Task.WhenAny(exited, Task.Delay(100, timeout.Token));
                timeout.Token.ThrowIfCancellationRequested();
                process.Refresh();
                if (!process.HasExited && process.WorkingSet64 > (long)limits.MaxProcessMemoryMb * 1024 * 1024)
                    throw new IOException("Native processing exceeded its memory limit.");
                if (output.IsFaulted || errors.IsFaulted) await Task.WhenAll(output, errors);
            }
            await Task.WhenAll(exited, output, errors);
            if (process.ExitCode != 0) throw new IOException("Document processing failed.");
            return await output;
        }
        finally
        {
            if (started && !process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); }
            logger?.LogInformation("COR native stage {Stage} in {DurationMs} ms", stage, watch.Elapsed.TotalMilliseconds);
        }
    }

    private static async Task<string> ReadOutput(StreamReader reader, int max, CancellationToken ct)
    {
        var text = new StringBuilder(); var buffer = new char[4096]; int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            if (text.Length + read > max) throw new IOException("Processing output exceeds limit.");
            text.Append(buffer, 0, read);
        }
        return text.ToString();
    }

    private async Task<byte[]> ReadBounded(Stream stream, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        if (stream.CanSeek)
        {
            var remaining = stream.Length - stream.Position;
            if (remaining < 0 || remaining > limits.MaxFileBytes) throw new DocumentValidationException("File size cannot exceed 5MB.");
            var bytes = GC.AllocateUninitializedArray<byte>((int)remaining);
            await stream.ReadExactlyAsync(bytes.AsMemory(), ct);
            logger?.LogDebug("COR file read in {DurationMs} ms; bytes {ByteCount}", watch.Elapsed.TotalMilliseconds, bytes.Length);
            return bytes;
        }
        using var output = new MemoryStream(); var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, 8192), ct)) > 0)
            {
                if (output.Length + read > limits.MaxFileBytes) throw new DocumentValidationException("File size cannot exceed 5MB.");
                output.Write(buffer, 0, read);
            }
            logger?.LogDebug("COR file read in {DurationMs} ms; bytes {ByteCount}", watch.Elapsed.TotalMilliseconds, output.Length);
            return output.ToArray();
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }

    private PdfDocument? Validate(byte[] bytes, string fileName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var pdf = bytes.AsSpan().StartsWith("%PDF-"u8);
        var png = bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        if (!(extension == ".pdf" && pdf || extension == ".png" && png || extension is ".jpg" or ".jpeg" && jpeg))
            throw new DocumentValidationException("File contents must match the PDF, JPG or PNG extension.");
        if (pdf)
        {
            var document = OpenPdf(bytes);
            try
            {
                if (document.NumberOfPages < 1 || document.NumberOfPages > limits.MaxPages)
                    throw new DocumentValidationException($"Upload a document with 1 to {limits.MaxPages} pages.");
                ValidatePdfObjects(document, ct);
                return document;
            }
            catch { document.Dispose(); throw; }
        }
        else
        {
            var (width, height) = png ? PngDimensions(bytes) : JpegDimensions(bytes);
            if (width < 1 || height < 1 || (long)width * height > limits.MaxImagePixels)
                throw new DocumentValidationException("Image is invalid or exceeds the supported image dimensions.");
        }
        return null;
    }

    private static PdfDocument OpenPdf(byte[] bytes)
    {
        try { return PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = false }); }
        catch { throw new DocumentValidationException("PDF is unreadable, damaged or password protected. Upload a readable copy."); }
    }

    private static void ValidatePdfObjects(PdfDocument document, CancellationToken ct)
    {
        var visited = new HashSet<UglyToad.PdfPig.Core.IndirectReference>();
        var count = 0;
        Walk(document.Structure.Catalog.CatalogDictionary, 0);
        void Walk(IToken token, int depth)
        {
            ct.ThrowIfCancellationRequested();
            if (++count > 100_000 || depth > 100) throw new DocumentValidationException("PDF structure exceeds safety limits.");
            switch (token)
            {
                case IndirectReferenceToken reference:
                    if (visited.Add(reference.Data)) Walk(document.Structure.GetObject(reference.Data).Data, depth + 1);
                    break;
                case DictionaryToken dictionary:
                    foreach (var item in dictionary.Data)
                    {
                        if (item.Key is "JS" or "JavaScript" or "OpenAction" or "AA" or "EmbeddedFiles" or "EF" or "XFA" ||
                            item.Value is NameToken name && name.Data is "JavaScript" or "Launch" or "RichMedia" or "SubmitForm" or "ImportData")
                            throw new DocumentValidationException("PDF contains active content or attachments. Upload a flattened copy.");
                        Walk(item.Value, depth + 1);
                    }
                    break;
                case ArrayToken array: foreach (var item in array.Data) Walk(item, depth + 1); break;
                case StreamToken stream: Walk(stream.StreamDictionary, depth + 1); break;
            }
        }
    }

    private static (int, int) PngDimensions(byte[] bytes) => bytes.Length < 24 ? (0, 0) :
        (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    private static (int, int) JpegDimensions(byte[] bytes)
    {
        var offset = 2;
        while (offset + 9 < bytes.Length)
        {
            if (bytes[offset++] != 255) return (0, 0);
            while (offset < bytes.Length && bytes[offset] == 255) offset++;
            if (offset >= bytes.Length) break;
            var marker = bytes[offset++];
            if (marker is 0xD8 or 0xD9) continue;
            if (offset + 2 > bytes.Length) break;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
            if (length < 2 || offset + length > bytes.Length) break;
            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
                return (BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2)), BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2)));
            offset += length;
        }
        return (0, 0);
    }
}
