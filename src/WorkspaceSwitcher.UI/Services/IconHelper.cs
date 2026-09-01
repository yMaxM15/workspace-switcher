using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkspaceSwitcher.UI.Services;

public static class IconHelper
{
    private static readonly ConcurrentDictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIconForExecutable(string? executablePath, string processName)
    {
        var resolvedPath = WorkspaceSwitcher.Core.Services.AppIdentityHelper.ResolveLaunchableExecutable(processName, executablePath);
        string? targetPath = (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath)) ? resolvedPath : executablePath;

        string key = targetPath ?? processName;
        if (string.IsNullOrEmpty(key)) return null;

        return IconCache.GetOrAdd(key, _ =>
        {
            try
            {
                if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                {
                    using var icon = Icon.ExtractAssociatedIcon(targetPath);
                    if (icon != null)
                    {
                        var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions()
                        );
                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                }
            }
            catch
            {
                // Fallback on extraction failure
            }

            return null;
        });
    }
}
