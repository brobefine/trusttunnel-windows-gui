using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TrustTunnelGui.Services;

/// <summary>
/// Win32 file dialogs via comdlg32. Работают в elevated-процессах,
/// в отличие от Windows.Storage.Pickers.
/// </summary>
public static class Win32FileDialog
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class OpenFileName
    {
        public int    lStructSize       = Marshal.SizeOf(typeof(OpenFileName));
        public IntPtr hwndOwner         = IntPtr.Zero;
        public IntPtr hInstance         = IntPtr.Zero;
        public string? lpstrFilter      = null;
        public string? lpstrCustomFilter= null;
        public int    nMaxCustFilter    = 0;
        public int    nFilterIndex      = 0;
        public string? lpstrFile        = null;
        public int    nMaxFile          = 0;
        public string? lpstrFileTitle   = null;
        public int    nMaxFileTitle     = 0;
        public string? lpstrInitialDir  = null;
        public string? lpstrTitle       = null;
        public int    Flags             = 0;
        public short  nFileOffset       = 0;
        public short  nFileExtension    = 0;
        public string? lpstrDefExt      = null;
        public IntPtr lCustData         = IntPtr.Zero;
        public IntPtr lpfnHook          = IntPtr.Zero;
        public string? lpTemplateName   = null;
        public IntPtr pvReserved        = IntPtr.Zero;
        public int    dwReserved        = 0;
        public int    FlagsEx           = 0;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileName([In, Out] OpenFileName ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    private const int OFN_OVERWRITEPROMPT = 0x00000002;
    private const int OFN_PATHMUSTEXIST   = 0x00000800;
    private const int OFN_FILEMUSTEXIST   = 0x00001000;
    private const int OFN_EXPLORER        = 0x00080000;

    /// <param name="filter">Например: "TOML config\0*.toml\0All files\0*.*\0\0"</param>
    public static string? ShowSave(IntPtr ownerHwnd, string filter, string defaultExt, string suggestedName)
    {
        var buffer = new string('\0', 1024);
        var ofn = new OpenFileName
        {
            hwndOwner       = ownerHwnd,
            lpstrFilter     = filter,
            lpstrFile       = suggestedName.PadRight(1024, '\0'),
            nMaxFile        = 1024,
            lpstrDefExt     = defaultExt,
            Flags           = OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_EXPLORER,
            lpstrInitialDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        return GetSaveFileName(ofn) ? ofn.lpstrFile?.TrimEnd('\0') : null;
    }

    public static string? ShowOpen(IntPtr ownerHwnd, string filter)
    {
        var ofn = new OpenFileName
        {
            hwndOwner   = ownerHwnd,
            lpstrFilter = filter,
            lpstrFile   = new string('\0', 1024),
            nMaxFile    = 1024,
            Flags       = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_EXPLORER,
        };
        return GetOpenFileName(ofn) ? ofn.lpstrFile?.TrimEnd('\0') : null;
    }
}