// ReSharper disable once CheckNamespace
namespace System;

internal static class StringExtensions
{
    // https://stackoverflow.com/questions/444798/case-insensitive-containsstring
    public static bool Contains(this string? source, string toCheck, StringComparison stringComparison)
    {
        return source?.IndexOf(toCheck, stringComparison) >= 0;
    }

    public static bool IsHttp(this string? url)
    {
        return url is not null && url.StartsWith("http", StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool IsFile(this string? url)
    {
        return url is not null && url.StartsWith("file:///", StringComparison.InvariantCultureIgnoreCase);
    }
}