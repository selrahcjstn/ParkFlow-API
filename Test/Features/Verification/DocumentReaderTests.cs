using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Infrastructure.Cloudinary;
using ParkFlow.Infrastructure.Verification;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Test.Features.Verification;

public class DocumentReaderTests
{
    [Fact]
    public async Task TextLayerPreflightUsesNoNativeToolsAndIncludesNoPersonalDataInTimings()
    {
        var settings = new OcrOptions { TesseractPath = "unavailable-tesseract", PdfRendererPath = "unavailable-renderer" };
        var logs = new TimingLogger();
        var reader = new CorDocumentReader(Options.Create(settings), Options.Create(new CloudinarySettings()), logs);
        var result = await reader.ReadTextLayerAsync(new MemoryStream(DigitalPdf(), false), "private-student-2023106763.pdf", default);
        Assert.NotNull(result); Assert.Equal("DigitalPdfTextExtracted", result.Outcome);
        Assert.Contains(logs.Messages, m => m.Contains("COR PDF text layer") && m.Contains("OCR pages 0"));
        Assert.All(logs.Messages, m => { Assert.DoesNotContain("2023106763", m); Assert.DoesNotContain("Dela Cruz", m); Assert.DoesNotContain("private-student", m); });
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadTextLayerAsync(new MemoryStream(DigitalPdf()), "cor.pdf", cancellation.Token));
    }

    [Fact]
    public async Task DigitalPdfWithBlankPageRetainsTextWithoutNativeProcessing()
    {
        var builder = new PdfDocumentBuilder(); builder.AddPage(PageSize.A4);
        var page = builder.AddPage(PageSize.A4); var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Certificate of Registration Student Name: Example, Juan", 16, new PdfPoint(40, 750), font);
        var logs = new TimingLogger(); var reader = new CorDocumentReader(Options.Create(new OcrOptions { TesseractPath = "unavailable", PdfRendererPath = "unavailable" }), Options.Create(new CloudinarySettings()), logs);
        var result = await reader.ReadAsync(new MemoryStream(builder.Build()), "cor.pdf", default);
        Assert.Equal("DigitalPdfTextExtracted", result.Outcome); Assert.Contains("Example, Juan", result.Text);
        Assert.Contains(logs.Messages, m => m.Contains("blank pages skipped 1"));
    }

    private sealed class TimingLogger : ILogger<CorDocumentReader>
    {
        public List<string> Messages { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    [NativeOcrFact]
    public async Task ScannedPdfPreflightDefersOcrAndSmallLogoDoesNotForceIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "parkflow-native-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var pdf = Path.Combine(directory, "cor.pdf"); await File.WriteAllBytesAsync(pdf, DigitalPdf());
            var prefix = Path.Combine(directory, "image"); await Render(pdf, prefix, "-png"); var png = await File.ReadAllBytesAsync(prefix + ".png");
            var reader = Reader(new OcrOptions { TesseractPath = "unavailable", PdfRendererPath = "unavailable" });
            Assert.Null(await reader.ReadTextLayerAsync(new MemoryStream(png), "cor.png", default));
            var scanned = new PdfDocumentBuilder(); scanned.AddPage(PageSize.A4).AddPng(png, new PdfRectangle(0, 0, 595, 842));
            Assert.Null(await reader.ReadTextLayerAsync(new MemoryStream(scanned.Build()), "cor.pdf", default));
            var digital = new PdfDocumentBuilder(); var page = digital.AddPage(PageSize.A4); var font = digital.AddStandard14Font(Standard14Font.Helvetica);
            page.AddText("Student Name: Example, Juan Student Number: 2023000001", 16, new PdfPoint(40, 700), font);
            page.AddPng(png, new PdfRectangle(0, 750, 40, 790));
            var result = await reader.ReadTextLayerAsync(new MemoryStream(digital.Build()), "cor.pdf", default);
            Assert.NotNull(result); Assert.Equal("DigitalPdfTextExtracted", result.Outcome);
            Assert.NotNull(CorTextVerification.Extract(result.Text).StudentNumber);
        }
        finally { Directory.Delete(directory, true); }
    }
    [LocalCorSampleFact]
    public async Task UserSuppliedDigitalSampleExtractsElevenClassesWithoutNativeOcr()
    {
        await using var input = File.OpenRead(Environment.GetEnvironmentVariable("PARKFLOW_COR_SAMPLE")!);
        var result = await Reader().ReadAsync(input, "cor.pdf", default);
        var fields = CorTextVerification.Extract(result.Text);
        Assert.Equal("DigitalPdfTextExtracted", result.Outcome);
        Assert.Empty(fields.ScheduleIssues); Assert.Equal(11, fields.Schedules.Count);
        Assert.Equal(4, CorStudentMapping.Aggregate(fields.Schedules).Count); Assert.Equal(7, CorStudentMapping.Week(fields).Count);
        Assert.False(string.IsNullOrWhiteSpace(fields.College)); Assert.False(string.IsNullOrWhiteSpace(fields.Section));
        Assert.All(new[] { fields.Name, fields.StudentNumber, fields.Course, fields.AcademicYear, fields.Semester }, value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }

    internal static OcrOptions Settings() => new()
    {
        TesseractPath = Environment.GetEnvironmentVariable("PARKFLOW_TEST_TESSERACT") ?? "tesseract",
        PdfRendererPath = Environment.GetEnvironmentVariable("PARKFLOW_TEST_PDFTOPPM") ?? "pdftoppm"
    };
    internal static CorDocumentReader Reader(OcrOptions? settings = null) => new(Options.Create(settings ?? Settings()), Options.Create(new CloudinarySettings { CloudName = "test" }));
    internal static byte[] DigitalPdf()
    {
        var builder = new PdfDocumentBuilder(); var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var y = 750;
        foreach (var line in VerificationRulesTests.Text.Split('\n')) { page.AddText(line, 16, new PdfPoint(40, y), font); y -= 35; }
        return builder.Build();
    }

    [Fact]
    public async Task ExtractsActualDigitalPdfAndVerifiesFields()
    {
        var result = await Reader().ReadAsync(new MemoryStream(DigitalPdf()), "cor.pdf", CancellationToken.None);
        Assert.Equal("DigitalPdfTextExtracted", result.Outcome);
        Assert.Empty(CorTextVerification.Compare(CorTextVerification.Extract(result.Text), VerificationRulesTests.Subject(), VerificationRulesTests.Policy()));
    }

    [Theory]
    [InlineData("cor.jpg")]
    [InlineData("cor.png")]
    [InlineData("cor.exe")]
    public async Task RejectsSpoofedExtensions(string fileName) =>
        await Assert.ThrowsAsync<DocumentValidationException>(() => Reader().ValidateAsync(new MemoryStream(DigitalPdf()), fileName, CancellationToken.None));

    [Fact]
    public async Task RejectsOversizeUnreadableAndTooManyPages()
    {
        await Assert.ThrowsAsync<DocumentValidationException>(() => Reader(new OcrOptions { MaxFileBytes = 10 }).ValidateAsync(new MemoryStream(DigitalPdf()), "cor.pdf", CancellationToken.None));
        await Assert.ThrowsAsync<DocumentValidationException>(() => Reader().ValidateAsync(new MemoryStream("%PDF-1.4 damaged"u8.ToArray()), "cor.pdf", CancellationToken.None));
        var builder = new PdfDocumentBuilder(); for (var i = 0; i < 6; i++) builder.AddPage(PageSize.A4);
        await Assert.ThrowsAsync<DocumentValidationException>(() => Reader().ValidateAsync(new MemoryStream(builder.Build()), "cor.pdf", CancellationToken.None));
    }

    [Theory]
    [InlineData("/OpenAction << /S /JavaScript /JS (sensitive-content) >>")]
    [InlineData("/Names << /EmbeddedFiles << /Names [] >> >>")]
    [InlineData("/AcroForm << /XFA (dynamic form) >>")]
    public async Task RejectsActivePdfContentAndAttachments(string catalog)
    {
        var error = await Assert.ThrowsAsync<DocumentValidationException>(() => Reader().ValidateAsync(new MemoryStream(PdfWithCatalog(catalog)), "cor.pdf", CancellationToken.None));
        Assert.Contains("active content", error.Message);
        Assert.DoesNotContain("sensitive-content", error.Message);
    }

    [Theory]
    [InlineData("https://evil.test/document.pdf", false)]
    [InlineData("https://res.cloudinary.com/other/raw/upload/parkflow/cor/document.pdf", false)]
    [InlineData("uploads/parkflow/cor/../../secret.pdf", false)]
    [InlineData("https://res.cloudinary.com/test/raw/upload/v123/parkflow/cor/document.pdf", true)]
    [InlineData("uploads/parkflow/cor/document.pdf", true)]
    public void FetchOnlyUsesConfiguredStorage(string url, bool expected) => Assert.Equal(expected, Reader().IsStoredDocument(url));

    [NativeOcrFact]
    public async Task SmartStudentFieldsAndDailySchedulesSurvivePngJpgAndScannedPdf()
    {
        var text = "Student General Information\nStudent No. 2023000001\nName Example, Juan R.\nCollege CICT\nProgram Bachelor of Science in Information Technology\nYear Level 4th Year\nSchool Year 2026-2027 1st Semester\nSection BSIT 4A\nRegistered Courses\nIT 101 M 08:00 AM-10:00 AM\nIT 102 M 03:00 PM-05:00 PM\nIT 103 T 07:00 AM-09:00 AM\nIT 104 Th 02:00 PM-04:00 PM\nAssessed Fees";
        var builder = new PdfDocumentBuilder(); var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica); var y = 790;
        foreach (var line in text.Split('\n')) { page.AddText(line, 14, new PdfPoint(30, y), font); y -= 30; }
        var directory = Path.Combine(Path.GetTempPath(), "parkflow-native-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        void Check(DocumentText result)
        {
            var fields = CorTextVerification.Extract(result.Text); Assert.Empty(fields.ScheduleIssues);
            Assert.Equal("2023000001", fields.StudentNumber); Assert.Equal("CICT", fields.College);
            Assert.Equal("2026-2027", fields.AcademicYear); Assert.Equal("1st Semester", fields.Semester);
            var daily = CorStudentMapping.Aggregate(fields.Schedules); Assert.Equal(3, daily.Count);
            Assert.Equal(TimeSpan.FromHours(8), daily[0].Start); Assert.Equal(TimeSpan.FromHours(17), daily[0].End);
            Assert.Equal(DayOfWeek.Tuesday, daily[1].Day); Assert.Equal(DayOfWeek.Thursday, daily[2].Day);
        }
        try
        {
            var pdf = builder.Build(); Check(await Reader().ReadAsync(new MemoryStream(pdf), "cor.pdf", default));
            var input = Path.Combine(directory, "cor.pdf"); await File.WriteAllBytesAsync(input, pdf);
            foreach (var (format, ext) in new[] { ("-png", ".png"), ("-jpeg", ".jpg") })
            {
                var prefix = Path.Combine(directory, "cor"); await Render(input, prefix, format);
                var image = await File.ReadAllBytesAsync(prefix + ext); Check(await Reader().ReadAsync(new MemoryStream(image), "cor" + ext, default));
                if (ext == ".png")
                {
                    var scanned = new PdfDocumentBuilder(); scanned.AddPage(PageSize.A4).AddPng(image, new PdfRectangle(0, 0, 595, 842));
                    Check(await Reader().ReadAsync(new MemoryStream(scanned.Build()), "scanned.pdf", default));
                }
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [NativeOcrFact]
    public async Task RecognizesPngJpgAndImageOnlyPdf()
    {
        var directory = Path.Combine(Path.GetTempPath(), "parkflow-native-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var pdf = Path.Combine(directory, "cor.pdf"); await File.WriteAllBytesAsync(pdf, DigitalPdf());
            foreach (var (format, ext) in new[] { ("-png", ".png"), ("-jpeg", ".jpg") })
            {
                var prefix = Path.Combine(directory, "cor");
                await Render(pdf, prefix, format);
                var image = await File.ReadAllBytesAsync(prefix + ext);
                var result = await Reader().ReadAsync(new MemoryStream(image), "cor" + ext, CancellationToken.None);
                Assert.Equal("ImageOcrCompleted", result.Outcome);
                Assert.Empty(CorTextVerification.Compare(CorTextVerification.Extract(result.Text), VerificationRulesTests.Subject(), VerificationRulesTests.Policy()));
                if (ext == ".png")
                {
                    var builder = new PdfDocumentBuilder(); var page = builder.AddPage(PageSize.A4);
                    page.AddPng(image, new PdfRectangle(0, 0, 595, 842));
                    var scanned = await Reader().ReadAsync(new MemoryStream(builder.Build()), "scan.pdf", CancellationToken.None);
                    Assert.Equal("ScannedPdfOcrCompleted", scanned.Outcome);
                    Assert.Empty(CorTextVerification.Compare(CorTextVerification.Extract(scanned.Text), VerificationRulesTests.Subject(), VerificationRulesTests.Policy()));
                    // Mixed PDFs can have a digital university heading above a scanned COR.
                    var headingFont = builder.AddStandard14Font(Standard14Font.Helvetica);
                    page.AddText("Bulacan State University Certificate of Registration", 12, new PdfPoint(40, 810), headingFont);
                    var mixed = await Reader().ReadAsync(new MemoryStream(builder.Build()), "mixed.pdf", CancellationToken.None);
                    Assert.Equal("ScannedPdfOcrCompleted", mixed.Outcome);
                    Assert.Empty(CorTextVerification.Compare(CorTextVerification.Extract(mixed.Text), VerificationRulesTests.Subject(), VerificationRulesTests.Policy()));
                }
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    internal static async Task Render(string input, string prefix, string format)
    {
        var info = new ProcessStartInfo(Settings().PdfRendererPath) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "-singlefile", "-scale-to", "2500", format, input, prefix }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
        Assert.Equal(0, process.ExitCode);
    }

    private static byte[] PdfWithCatalog(string catalog)
    {
        var text = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
        var objects = new[] { $"<< /Type /Catalog /Pages 2 0 R {catalog} >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>" };
        for (var i = 0; i < objects.Length; i++) { offsets.Add(text.Length); text.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = text.Length; text.Append($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) text.Append($"{offset:D10} 00000 n \n");
        text.Append($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(text.ToString());
    }
}

public class NativeOcrFactAttribute : FactAttribute
{
    public NativeOcrFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PARKFLOW_NATIVE_OCR_TESTS") != "1") Skip = "Set PARKFLOW_NATIVE_OCR_TESTS=1 with Tesseract and Poppler installed to run native OCR integration.";
    }
}
