using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>Scenario selection in the Unity Editor and Windows standalone player.</summary>
public static class ScenarioFilePicker
{
    public static string Open(string initialDirectory)
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel(
            "Load testing scenario", initialDirectory, "txt");
#elif UNITY_STANDALONE_WIN
        return OpenWindows(initialDirectory);
#else
        throw new NotSupportedException(
            "The scenario file picker supports the Unity Editor and Windows builds.");
#endif
    }

#if UNITY_STANDALONE_WIN
    private static string OpenWindows(string initialDirectory)
    {
        const int pathCapacity = 32768;
        var dialog = new OpenFileName
        {
            owner = GetActiveWindow(),
            filter = "Scenario text files (*.txt)\0*.txt\0\0",
            filterIndex = 1,
            file = new StringBuilder(pathCapacity),
            maxFile = pathCapacity,
            initialDirectory = initialDirectory,
            title = "Load testing scenario",
            // Explorer, file/path must exist, preserve working directory, hide read-only.
            flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00000004,
            defaultExtension = "txt"
        };
        dialog.structSize = Marshal.SizeOf(typeof(OpenFileName));
        if (GetOpenFileName(dialog)) return dialog.file.ToString();

        int error = CommDlgExtendedError();
        if (error != 0)
            throw new IOException($"Windows file picker failed (0x{error:X}).");
        return string.Empty;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class OpenFileName
    {
        public int structSize;
        public IntPtr owner;
        public IntPtr instance;
        public string filter;
        public IntPtr customFilter;
        public int maxCustomFilter;
        public int filterIndex;
        public StringBuilder file;
        public int maxFile;
        public IntPtr fileTitle;
        public int maxFileTitle;
        public string initialDirectory;
        public string title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        public string defaultExtension;
        public IntPtr customData;
        public IntPtr hook;
        public IntPtr templateName;
        public IntPtr reserved;
        public int reservedValue;
        public int flagsEx;
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName dialog);

    [DllImport("comdlg32.dll")]
    private static extern int CommDlgExtendedError();

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
#endif
}
