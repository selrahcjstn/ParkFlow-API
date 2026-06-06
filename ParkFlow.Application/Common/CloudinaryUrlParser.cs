using System;

namespace ParkFlow.Application.Common;

public static class CloudinaryUrlParser
{
    public static string? ExtractPublicId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        // Find the index of "parkflow/"
        int parkflowIndex = url.IndexOf("parkflow/", StringComparison.OrdinalIgnoreCase);
        if (parkflowIndex == -1) return null;

        // Get everything from "parkflow/" onwards
        string fromFolder = url.Substring(parkflowIndex);

        // Remove the file extension (e.g., .jpg, .pdf)
        int lastDotIndex = fromFolder.LastIndexOf('.');
        if (lastDotIndex != -1)
        {
            fromFolder = fromFolder.Substring(0, lastDotIndex);
        }

        return fromFolder;
    }
}
