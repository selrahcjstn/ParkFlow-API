using System;
using System.Collections.Generic;
using System.Linq;

namespace ParkFlow.Application.Common;

public static class PasswordGenerator
{
    private static readonly Random _random = new Random();
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Special = "!@#$%^&*";

    public static string GenerateTemporaryPassword(int length = 10)
    {
        var chars = new List<char>
        {
            Uppercase[_random.Next(Uppercase.Length)],
            Lowercase[_random.Next(Lowercase.Length)],
            Digits[_random.Next(Digits.Length)],
            Special[_random.Next(Special.Length)]
        };

        string allChars = Uppercase + Lowercase + Digits + Special;
        for (int i = chars.Count; i < length; i++)
        {
            chars.Add(allChars[_random.Next(allChars.Length)]);
        }

        return new string(chars.OrderBy(_ => _random.Next()).ToArray());
    }
}
