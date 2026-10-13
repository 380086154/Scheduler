using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.App.Localization;
using Scheduler.Core.InputActivity;
using Scheduler.Core.Services;

namespace Scheduler.App.Ui;

internal sealed class TrayController : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILocalizationService _localization;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _refreshItem;
    private readonly ToolStripMenuItem _exitItem;

    public TrayController(IServiceProvider services, ILocalizationService localization, Action openSettings, Action exit)
    {
        _services = services;
        _localization = localization;

        _settingsItem = new ToolStripMenuItem();
        _refreshItem = new ToolStripMenuItem();
        _exitItem = new ToolStripMenuItem();
        _settingsItem.Click += (_, _) => openSettings();
        _refreshItem.Click += (_, _) => UpdateTooltip();
        _exitItem.Click += (_, _) => exit();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_refreshItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIconFactory.NotifyIcon,
            Visible = false,
            Text = "Scheduler",
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => openSettings();

        ApplyLanguage();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public void Show()
    {
        UpdateTooltip();
        _notifyIcon.Visible = true;
    }

    public void ShowBalloon(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(4000);
    }

    public void UpdateTooltip()
    {
        try
        {
            var presence = _services.GetRequiredService<SessionPresenceService>();
            var input = _services.GetRequiredService<InputActivityService>();
            var minutes = (int)presence.TodayAtOfficeDuration.TotalMinutes;
            var snap = input.GetTodaySnapshot();
            _notifyIcon.Text = Truncate(_localization.Format(
                LocalizationKeys.TrayTooltip,
                minutes,
                snap.Keyboard,
                snap.MouseClicks));
        }
        catch
        {
            _notifyIcon.Text = _localization.GetString(LocalizationKeys.AppName);
        }
    }

    public void Dispose()
    {
        _localization.LanguageChanged -= OnLanguageChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyLanguage();

    private void ApplyLanguage()
    {
        _settingsItem.Text = _localization.GetString(LocalizationKeys.TraySettings);
        _refreshItem.Text = _localization.GetString(LocalizationKeys.TrayRefresh);
        _exitItem.Text = _localization.GetString(LocalizationKeys.TrayExit);
        UpdateTooltip();
    }

    private static string Truncate(string text) =>
        text.Length <= 63 ? text : text[..63];
}
