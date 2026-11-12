using System;
using System.IO;

/// <summary>Small, validated overrides for standalone training players.</summary>
public static class TrainingCommandLine
{
    public static bool HasFlag(string option)
    {
        foreach (string arg in Environment.GetCommandLineArgs())
            if (string.Equals(arg, option, StringComparison.Ordinal))
                return true;
        return false;
    }

    public static int GetInt(string option, int fallback)
    {
        string value = GetValue(option);
        return int.TryParse(value, out int parsed) && parsed >= 0 ? parsed : fallback;
    }

    public static string GetSafeId(string option, string fallback)
    {
        string value = GetValue(option);
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        foreach (char c in value)
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                throw new ArgumentException($"{option} must use letters, digits, - or _.");
        return value;
    }

    private static string GetValue(string option)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], option, StringComparison.Ordinal))
                return args[i + 1];
        return null;
    }
}
