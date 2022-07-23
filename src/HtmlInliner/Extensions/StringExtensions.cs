// ReSharper disable once CheckNamespace
namespace System;

internal static class StringExtensions
{
    // https://stackoverflow.com/questions/444798/case-insensitive-containsstring
    public static bool Contains(this string? source, string toCheck, StringComparison stringComparison)
    {
        return source?.IndexOf(toCheck, stringComparison) >= 0;
    }
}