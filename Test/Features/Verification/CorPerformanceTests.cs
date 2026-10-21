using System.Diagnostics;
using System.Text.Json;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using ParkFlow.Application.Features.Verification;
using Xunit;
using Xunit.Abstractions;

namespace Test.Features.Verification;

public sealed class CorPerformanceFactAttribute : FactAttribute
{
    public CorPerformanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PARKFLOW_COR_PERFORMANCE") != "1")
            Skip = "Opt-in local COR timings; not a timing assertion or CI speed requirement.";
    }
}

public class CorPerformanceTests(ITestOutputHelper output)
{
    [CorPerformanceFact]
    public async Task MeasureNativeOcrWithoutLoggingDocumentData()
    {
        if (Environment.GetEnvironmentVariable("PARKFLOW_NATIVE_OCR_TESTS") != "1") return;
        var directory = Path.Combine(Path.GetTempPath(), "parkflow-perf-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "cor.pdf"); await File.WriteAllBytesAsync(input, DocumentReaderTests.DigitalPdf());
            var documents = new Dictionary<string, (byte[] Bytes, string Extension)>();
            var watch = Stopwatch.StartNew(); var prefix = Path.Combine(directory, "cor");
            await DocumentReaderTests.Render(input, prefix, "-png"); var renderMs = watch.Elapsed.TotalMilliseconds;
            var png = await File.ReadAllBytesAsync(prefix + ".png"); documents["Png"] = (png, ".png");
            await DocumentReaderTests.Render(input, prefix, "-jpeg"); documents["Jpg"] = (await File.ReadAllBytesAsync(prefix + ".jpg"), ".jpg");
            var pdf = new PdfDocumentBuilder(); pdf.AddPage(PageSize.A4).AddPng(png, new PdfRectangle(0, 0, 595, 842));
            documents["ScannedPdf"] = (pdf.Build(), ".pdf"); pdf.AddPage(PageSize.A4);
            documents["ScannedPdfWithBlankPage"] = (pdf.Build(), ".pdf");
            var timings = new List<object> { new { Kind = "Render2500px", Milliseconds = Math.Round(renderMs, 3) } };
            foreach (var (kind, document) in documents)
            {
                var samples = new List<double>(); var reader = DocumentReaderTests.Reader();
                await reader.ReadAsync(new MemoryStream(document.Bytes, false), "cor" + document.Extension, default);
                for (var i = 0; i < 3; i++)
                {
                    watch.Restart(); var result = await reader.ReadAsync(new MemoryStream(document.Bytes, false), "cor" + document.Extension, default);
                    var fields = CorTextVerification.Extract(result.Text); samples.Add(watch.Elapsed.TotalMilliseconds);
                    Assert.Empty(CorTextVerification.Compare(fields, VerificationRulesTests.Subject(), VerificationRulesTests.Policy()));
                }
                timings.Add(new { Kind = kind, Runs = 3, MedianMs = Median(samples) });
            }
            var json = JsonSerializer.Serialize(timings, new JsonSerializerOptions { WriteIndented = true }); output.WriteLine(json);
            var destination = Environment.GetEnvironmentVariable("PARKFLOW_COR_PERFORMANCE_NATIVE_OUTPUT");
            if (!string.IsNullOrWhiteSpace(destination)) await File.WriteAllTextAsync(destination, json);
        }
        finally { Directory.Delete(directory, true); }
    }
    [CorPerformanceFact]
    public async Task MeasureWarmExtractionParsingAndDailyPreviewWithoutLoggingDocumentData()
    {
        var documents = new Dictionary<string, byte[]> { ["SyntheticDigital"] = DocumentReaderTests.DigitalPdf() };
        var sample = Environment.GetEnvironmentVariable("PARKFLOW_COR_SAMPLE");
        if (!string.IsNullOrWhiteSpace(sample)) documents["ReferenceDigital"] = await File.ReadAllBytesAsync(sample);
        var timings = new List<object>();
        foreach (var (kind, bytes) in documents)
        {
            var reader = DocumentReaderTests.Reader();
            // Warm JIT and PdfPig. Reading the original file is outside timed work.
            await reader.ReadAsync(new MemoryStream(bytes, writable: false), "cor.pdf", default);
            var extraction = new List<double>(); var parsing = new List<double>(); var total = new List<double>(); var validation = new List<double>();
            for (var i = 0; i < 20; i++)
            {
                var watch = Stopwatch.StartNew();
                await reader.ValidateAsync(new MemoryStream(bytes, writable: false), "cor.pdf", default);
                validation.Add(watch.Elapsed.TotalMilliseconds);
                watch.Restart();
                var result = await reader.ReadAsync(new MemoryStream(bytes, writable: false), "cor.pdf", default);
                extraction.Add(watch.Elapsed.TotalMilliseconds);
                var beforeParse = watch.Elapsed.TotalMilliseconds;
                var fields = CorTextVerification.Extract(result.Text);
                var week = CorStudentMapping.Week(fields);
                var information = CorStudentMapping.Information(fields);
                parsing.Add(watch.Elapsed.TotalMilliseconds - beforeParse); total.Add(watch.Elapsed.TotalMilliseconds);
                Assert.Equal("DigitalPdfTextExtracted", result.Outcome); Assert.Equal(7, week.Count);
                if (kind == "ReferenceDigital") { Assert.Equal(11, fields.Schedules.Count); Assert.Empty(fields.ScheduleIssues); }
                GC.KeepAlive(information);
            }
            timings.Add(new { Kind = kind, Runs = 20, ValidationMedianMs = Median(validation), ReadMedianMs = Median(extraction),
                ParseAndPreviewMedianMs = Median(parsing), ReadParsePreviewMedianMs = Median(total), ReadParsePreviewP95Ms = Percentile95(total) });
        }
        // Parser benchmark uses fake identities and the existing representative layout.
        var source = "Student No. 2023000001\nName Example, Juan R.\nCollege CICT\nProgram Bachelor of Science in\nInformation Technology\nYear Level 4th Year\nSchool Year 2026-2027 1st Semester\nSection BSIT 4A\n" + CorScheduleTests.ScheduleTable;
        var batches = new List<double>();
        for (var batch = 0; batch < 20; batch++)
        {
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 100; i++) { var fields = CorTextVerification.Extract(source); Assert.Equal(7, CorStudentMapping.Week(fields).Count); }
            batches.Add(watch.Elapsed.TotalMilliseconds / 100);
        }
        timings.Add(new { Kind = "ParserAndAggregation", Runs = 2000, MedianMs = Median(batches) });
        var json = JsonSerializer.Serialize(timings, new JsonSerializerOptions { WriteIndented = true }); output.WriteLine(json);
        var destination = Environment.GetEnvironmentVariable("PARKFLOW_COR_PERFORMANCE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(destination)) await File.WriteAllTextAsync(destination, json);
    }
    private static double Median(List<double> samples) { samples.Sort(); return Math.Round((samples[(samples.Count - 1) / 2] + samples[samples.Count / 2]) / 2, 3); }
    private static double Percentile95(List<double> samples) { samples.Sort(); return Math.Round(samples[(int)Math.Ceiling(samples.Count * .95) - 1], 3); }
}
