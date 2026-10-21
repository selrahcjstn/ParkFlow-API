namespace ParkFlow.Infrastructure.Verification;

public class OcrOptions
{
    public string TesseractPath { get; set; } = "tesseract";
    public string PdfRendererPath { get; set; } = "pdftoppm";
    public string? TessdataDirectory { get; set; }
    public string Language { get; set; } = "eng";
    public int MaxFileBytes { get; set; } = 5 * 1024 * 1024;
    public int MaxPages { get; set; } = 5;
    public int MaxImagePixels { get; set; } = 20_000_000;
    public int MaxTextCharacters { get; set; } = 100_000;
    public int MaxProcessMemoryMb { get; set; } = 256;
    public int JobTimeoutSeconds { get; set; } = 120;
    public int ProcessTimeoutSeconds { get; set; } = 30;
    public int PollSeconds { get; set; } = 5;
    public int MaxAttempts { get; set; } = 3;
}
