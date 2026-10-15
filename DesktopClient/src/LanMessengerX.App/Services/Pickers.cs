using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LanMessengerX.App.Localization;
using LanMessengerX.App.Views;

namespace LanMessengerX.App.Services;

/// <summary>
/// File and folder choosers that always work: the platform dialog of Avalonia first; if that fails or returns instantly
/// without having shown anything, the classic Win32 dialog (Windows), and as a last resort a chooser drawn by the app itself.
/// </summary>
public static class Pickers
{
    public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };

    public static async Task<IReadOnlyList<string>> OpenFiles(Window owner, string title, bool multiple, bool imagesOnly = false)
    {
        // 1. Avalonia's own dialog
        try
        {
            var sw = Stopwatch.StartNew();
            var options = new FilePickerOpenOptions { Title = title, AllowMultiple = multiple };
            if (imagesOnly)
                options.FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = ImageExtensions.Select(e => "*" + e).ToArray() } };
            var files = await owner.StorageProvider.OpenFilePickerAsync(options);
            var paths = files.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToList();
            if (paths.Count > 0) return paths;
            if (sw.ElapsedMilliseconds >= 600) return paths; // the user looked at it and cancelled
            FileLog.Write("file dialog: returned nothing after " + sw.ElapsedMilliseconds + " ms, trying the next one");
        }
        catch (Exception e) { FileLog.Write("file dialog failed: " + e.Message); }

        // 2. the classic Windows dialog
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var hwnd = owner.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                var res = Win32.OpenFileName(hwnd, title, multiple, imagesOnly, out var ok);
                if (ok) return res;
                if (sw.ElapsedMilliseconds >= 600) return res;
            }
            catch (Exception e) { FileLog.Write("win32 file dialog failed: " + e.Message); }
        }

        // 3. our own
        var r = await FileBrowserWindow.Show(owner, title, multiple ? FileBrowserWindow.Mode.Files : FileBrowserWindow.Mode.File, imagesOnly);
        return r;
    }

    public static async Task<string?> OpenFolder(Window owner, string title)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var dirs = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            var p = dirs.Select(d => d.TryGetLocalPath()).FirstOrDefault(x => !string.IsNullOrEmpty(x));
            if (p != null) return p;
            if (sw.ElapsedMilliseconds >= 600) return null;
            FileLog.Write("folder dialog: returned nothing after " + sw.ElapsedMilliseconds + " ms, using the built-in chooser");
        }
        catch (Exception e) { FileLog.Write("folder dialog failed: " + e.Message); }
        var r = await FileBrowserWindow.Show(owner, title, FileBrowserWindow.Mode.Folder, false);
        return r.FirstOrDefault();
    }

    private static class Win32
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner, hInstance, lpstrFilter, lpstrCustomFilter;
            public int nMaxCustFilter, nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public IntPtr lpstrInitialDir, lpstrTitle;
            public int Flags;
            public short nFileOffset, nFileExtension;
            public IntPtr lpstrDefExt, lCustData, lpfnHook, lpTemplateName, pvReserved;
            public int dwReserved, FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);

        [DllImport("comdlg32.dll")]
        private static extern int CommDlgExtendedError();

        public static IReadOnlyList<string> OpenFileName(IntPtr owner, string title, bool multiple, bool images, out bool ok)
        {
            const int chars = 32768;
            var buffer = Marshal.AllocHGlobal(chars * 2);
            var filter = images
                ? "Images\0" + string.Join(";", ImageExtensions.Select(e => "*" + e)) + "\0All files\0*.*\0\0"
                : "All files\0*.*\0\0";
            var pFilter = Marshal.AllocHGlobal((filter.Length + 1) * 2);
            var pTitle = Marshal.StringToHGlobalUni(title);
            try
            {
                for (int i = 0; i < chars; i++) Marshal.WriteInt16(buffer, i * 2, 0);
                for (int i = 0; i < filter.Length; i++) Marshal.WriteInt16(pFilter, i * 2, filter[i]);
                Marshal.WriteInt16(pFilter, filter.Length * 2, 0);
                var ofn = new OPENFILENAME
                {
                    lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                    hwndOwner = owner,
                    lpstrFilter = pFilter,
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = chars,
                    lpstrTitle = pTitle,
                    // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR | OFN_HIDEREADONLY (| OFN_ALLOWMULTISELECT)
                    Flags = 0x80000 | 0x1000 | 0x800 | 0x8 | 0x4 | (multiple ? 0x200 : 0),
                };
                bool res = GetOpenFileNameW(ref ofn);
                if (!res)
                {
                    int err = CommDlgExtendedError();
                    ok = err == 0; // 0: the user cancelled
                    return Array.Empty<string>();
                }
                ok = true;
                var parts = new List<string>();
                int start = 0;
                for (int i = 0; i < chars - 1; i++)
                {
                    if (Marshal.ReadInt16(buffer, i * 2) != 0) continue;
                    if (i == start) break;
                    parts.Add(Marshal.PtrToStringUni(buffer + start * 2, i - start)!);
                    start = i + 1;
                }
                if (parts.Count <= 1) return parts;
                return parts.Skip(1).Select(f => Path.Combine(parts[0], f)).ToList();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                Marshal.FreeHGlobal(pFilter);
                Marshal.FreeHGlobal(pTitle);
            }
        }
    }
}
