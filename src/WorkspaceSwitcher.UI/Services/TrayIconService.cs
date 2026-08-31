using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Forms;
using WorkspaceSwitcher.Core;
using WorkspaceSwitcher.Core.Services;

namespace WorkspaceSwitcher.UI.Services;

public class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly WindowManager _windowManager;
    private readonly IProfileService _profileService;
    private readonly Action _showMainWindowAction;
    private readonly Action _exitAppAction;

    public TrayIconService(
        WindowManager windowManager,
        IProfileService profileService,
        Action showMainWindowAction,
        Action exitAppAction)
    {
        _windowManager = windowManager;
        _profileService = profileService;
        _showMainWindowAction = showMainWindowAction;
        _exitAppAction = exitAppAction;

        _notifyIcon = new NotifyIcon
        {
            Text = "Workspace / Window Layout Switcher",
            Visible = true,
            Icon = GenerateAppIcon()
        };

        _notifyIcon.DoubleClick += (s, e) => _showMainWindowAction();
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _showMainWindowAction();
            }
        };

        RebuildContextMenu();
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    public void RebuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        // Title item
        var titleItem = new ToolStripMenuItem("🪟 Workspace Switcher")
        {
            Enabled = false,
            Font = new Font(FontFamily.GenericSansSerif, 9, System.Drawing.FontStyle.Bold)
        };
        menu.Items.Add(titleItem);
        menu.Items.Add(new ToolStripSeparator());

        // Dynamic Profiles Submenu
        var profiles = _profileService.GetProfileNames();
        var quickSwitchMenu = new ToolStripMenuItem("⚡ Quick Switch Profile");

        if (profiles.Count == 0)
        {
            quickSwitchMenu.DropDownItems.Add(new ToolStripMenuItem("No profiles saved yet") { Enabled = false });
        }
        else
        {
            foreach (var name in profiles)
            {
                var item = new ToolStripMenuItem(name);
                item.Click += (s, e) =>
                {
                    var profile = _profileService.LoadProfile(name);
                    if (profile != null)
                    {
                        int restored = _windowManager.RestoreWorkspace(profile, launchIfNotRunning: true);
                        ShowNotification("Profile Applied", $"Restored layout '{name}' ({restored} windows).");
                    }
                };
                quickSwitchMenu.DropDownItems.Add(item);
            }
        }
        menu.Items.Add(quickSwitchMenu);

        // Snapshot Action
        var snapshotItem = new ToolStripMenuItem("📸 Quick Snapshot Current", null, (s, e) =>
        {
            string profileName = $"Quick_{DateTime.Now:HHmmss}";
            var profile = _windowManager.CaptureWorkspace(profileName, "Quick snapshot from tray icon");
            _profileService.SaveProfile(profile);
            RebuildContextMenu();
            ShowNotification("Snapshot Saved", $"Saved current layout as '{profileName}' ({profile.Windows.Count} windows).");
        });
        menu.Items.Add(snapshotItem);

        menu.Items.Add(new ToolStripSeparator());

        // Open Dashboard
        var openItem = new ToolStripMenuItem("⚙️ Open Dashboard", null, (s, e) => _showMainWindowAction());
        menu.Items.Add(openItem);

        // Exit
        var exitItem = new ToolStripMenuItem("❌ Exit", null, (s, e) => _exitAppAction());
        menu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = menu;
    }

    private static Icon GenerateAppIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background dark rounded box
        using var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
        g.FillRectangle(bgBrush, 0, 0, 32, 32);

        // Draw 4 window tiles in accent blue
        using var tileBrush1 = new SolidBrush(Color.FromArgb(0, 120, 215));
        using var tileBrush2 = new SolidBrush(Color.FromArgb(96, 205, 255));

        g.FillRectangle(tileBrush1, 3, 3, 11, 11);
        g.FillRectangle(tileBrush2, 17, 3, 12, 11);
        g.FillRectangle(tileBrush2, 3, 17, 11, 12);
        g.FillRectangle(tileBrush1, 17, 17, 12, 12);

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        GC.SuppressFinalize(this);
    }
}
