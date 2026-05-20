namespace ParkFlow.Application.Features.ParkingLogs.Services;

public static class ParkingLogIdHelper
{
    public static string GenerateHistoryId(DateTime entryTimeUtc, int suffixLength = 4)
    {
        var datePart = entryTimeUtc.ToString("yyyyMMdd");
        var suffix = GenerateRandomLetterCode(suffixLength);
        return $"PL-{datePart}-{suffix}";
    }

    private static string GenerateRandomLetterCode(int length)
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return new string(Enumerable.Range(0, length)
            .Select(_ => letters[Random.Shared.Next(letters.Length)])
            .ToArray());
    }
}
