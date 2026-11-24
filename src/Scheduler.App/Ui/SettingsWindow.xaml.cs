using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.App.Localization;
using Scheduler.App.Settings;
using Scheduler.Core.Settings;
using Scheduler.Core.Apis.Holiday;
using Scheduler.Core.InputActivity;
using Scheduler.Core.Notifications;
using Scheduler.Core.Services;

namespace Scheduler.App.Ui;

public partial class SettingsWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly ILocalizationService _localization;
    private readonly List<NotificationRuleOptions> _rules = [];
    private bool _suppressRuleEvents;
    private int _ruleIndex = -1;
    private string? _statusKey;
    private object[] _statusArgs = [];
    private NotificationProvider _webhookProvider = NotificationProvider.Teams;
    private string _teamsUrl = "";
    private string _weComUrl = "";
    private string _dingTalkUrl = "";

    public SettingsWindow(IServiceProvider services, ILocalizationService localization)
    {
        _services = services;
        _localization = localization;
        InitializeComponent();
        Icon = AppIconFactory.WindowIcon;

        ProviderCombo.DisplayMemberPath = nameof(ProviderChoice.Name);
        RuleKind.DisplayMemberPath = nameof(KindChoice.Name);
        RuleSchedule.DisplayMemberPath = nameof(ScheduleChoice.Name);
        RebuildChoices();

        LoadFromStore();
        RefreshTodayStatus();
        UpdateWebhookHint();
        UpdateLanguageToggle();
        HolidayPanel.Attach(_services.GetRequiredService<HolidayService>(), _localization);
        HolidayPanel.StatusChanged += (_, _) => ShowCurrentStatus();
        SettingsTag.IsChecked = true;
        _localization.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => _localization.LanguageChanged -= OnLanguageChanged;
    }

    private void LoadFromStore()
    {
        var settings = UserSettingsStore.Load();
        settings.Notifications ??= new NotificationOptions();
        settings.Notifications.Rules ??= [];
        settings.InputActivity ??= new InputActivityOptions();

        _rules.Clear();
        _rules.AddRange(settings.Notifications.Rules);

        _teamsUrl = settings.Notifications.TeamsWebhookUrl ?? "";
        _weComUrl = settings.Notifications.WeComWebhookUrl ?? "";
        _dingTalkUrl = settings.Notifications.DingTalkWebhookUrl ?? "";
        _webhookProvider = settings.Notifications.Provider;
        WebhookBox.Text = WebhookFor(_webhookProvider);

        ProviderCombo.SelectedItem = ProviderCombo.Items
            .OfType<ProviderChoice>()
            .FirstOrDefault(x => x.Value == settings.Notifications.Provider)
            ?? ProviderCombo.Items.OfType<ProviderChoice>().FirstOrDefault();
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        InputStatsBox.IsChecked = settings.InputActivity.Enabled;

        RefreshRuleList(selectIndex: 0, flushCurrent: false);
    }

    private void RefreshRuleList(int selectIndex, bool flushCurrent = true)
    {
        _suppressRuleEvents = true;
        if (flushCurrent)
            FlushRuleAt(_ruleIndex);
        RuleList.ItemsSource = null;
        RuleList.ItemsSource = _rules.Select(FormatRuleListItem).ToList();
        if (_rules.Count == 0)
        {
            _ruleIndex = -1;
            RuleList.SelectedIndex = -1;
            RuleEditor.IsEnabled = false;
        }
        else
        {
            RuleEditor.IsEnabled = true;
            _ruleIndex = Math.Clamp(selectIndex, 0, _rules.Count - 1);
            RuleList.SelectedIndex = _ruleIndex;
            BindSelectedRule();
        }
        _suppressRuleEvents = false;
    }

    private void BindSelectedRule()
    {
        if (SelectedRule is not { } rule)
            return;

        _suppressRuleEvents = true;
        RuleEnabled.IsChecked = rule.Enabled;
        RuleKind.SelectedItem = RuleKind.Items
            .OfType<KindChoice>()
            .FirstOrDefault(x => x.Value == rule.Kind)
            ?? RuleKind.Items.OfType<KindChoice>().FirstOrDefault();
        RuleTitle.Text = rule.Title;
        RuleTime.Text = rule.Time;
        RuleProcess.Text = string.Join(", ", rule.ProcessNames);
        RuleAbsentMinutes.Text = rule.AbsentMinutes.ToString();
        RuleSchedule.SelectedItem = RuleSchedule.Items
            .OfType<ScheduleChoice>()
            .FirstOrDefault(x => x.Value == rule.Schedule)
            ?? RuleSchedule.Items[0];
        RuleOfficeMinutes.Text = rule.RequireAtOfficeMinutes.ToString();
        RuleMessage.Text = rule.Message;
        ApplyKindEditor(rule.Kind);
        _suppressRuleEvents = false;
    }

    private void FlushSelectedRule() => FlushRuleAt(_ruleIndex);

    private void FlushRuleAt(int index)
    {
        if (index < 0 || index >= _rules.Count) return;
        var rule = _rules[index];

        rule.Enabled = RuleEnabled.IsChecked == true;
        rule.Kind = SelectedKind;
        rule.Title = RuleTitle.Text.Trim();
        rule.Time = RuleTime.Text.Trim();
        rule.ProcessNames = ParseProcessNames(RuleProcess.Text).ToList();
        if (!int.TryParse(RuleAbsentMinutes.Text.Trim(), out var absentMinutes) || absentMinutes < 0)
            absentMinutes = 0;
        rule.AbsentMinutes = absentMinutes;
        if (RuleSchedule.SelectedItem is ScheduleChoice schedule)
            rule.Schedule = schedule.Value;
        if (!int.TryParse(RuleOfficeMinutes.Text.Trim(), out var minutes) || minutes < 0)
            minutes = 0;
        rule.RequireAtOfficeMinutes = minutes;
        rule.Message = RuleMessage.Text;
        if (string.IsNullOrWhiteSpace(rule.Id))
            rule.Id = $"rule-{Guid.NewGuid():N}"[..13];
    }

    private NotificationRuleOptions? SelectedRule =>
        RuleList.SelectedIndex >= 0 && RuleList.SelectedIndex < _rules.Count
            ? _rules[RuleList.SelectedIndex]
            : null;

    private void RuleList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressRuleEvents) return;
        FlushRuleAt(_ruleIndex);
        _ruleIndex = RuleList.SelectedIndex;
        BindSelectedRule();
    }

    private void AddRule_OnClick(object sender, RoutedEventArgs e)
    {
        FlushSelectedRule();
        _rules.Add(new NotificationRuleOptions
        {
            Id = $"rule-{Guid.NewGuid():N}"[..13],
            Enabled = true,
            Title = _localization.GetString(LocalizationKeys.NewRuleTitle),
            Time = "09:00",
            Schedule = NotificationSchedule.DailyWorkday,
            Message = ""
        });
        RefreshRuleList(_rules.Count - 1);
    }

    private void DeleteRule_OnClick(object sender, RoutedEventArgs e)
    {
        var index = RuleList.SelectedIndex;
        if (index < 0) return;
        var name = string.IsNullOrWhiteSpace(_rules[index].Title) ? _rules[index].Id : _rules[index].Title;
        if (!ConfirmDelete(name)) return;
        _rules.RemoveAt(index);
        RefreshRuleList(index);
    }

    private void ProviderCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressRuleEvents) return;
        StoreWebhookText(_webhookProvider);
        _webhookProvider = SelectedProvider;
        WebhookBox.Text = WebhookFor(_webhookProvider);
        UpdateWebhookHint();
    }

    private void UpdateWebhookHint()
    {
        var key = SelectedProvider switch
        {
            NotificationProvider.WeCom => LocalizationKeys.WebhookHintWeCom,
            NotificationProvider.DingTalk => LocalizationKeys.WebhookHintDingTalk,
            _ => LocalizationKeys.WebhookHintTeams
        };
        WebhookHint.Text = _localization.GetString(key);
    }

    private void RefreshStatus_OnClick(object sender, RoutedEventArgs e) => RefreshTodayStatus();

    private void RefreshTodayStatus()
    {
        try
        {
            var presence = _services.GetRequiredService<SessionPresenceService>();
            var input = _services.GetRequiredService<InputActivityService>();
            var snap = input.GetTodaySnapshot();
            var minutes = (int)presence.TodayAtOfficeDuration.TotalMinutes;
            var state = presence.IsRemote
                ? LocalizationKeys.StatusRemote
                : presence.IsUnlocked
                    ? LocalizationKeys.StatusUnlocked
                    : LocalizationKeys.StatusLocked;
            TodayStatusText.Text = _localization.Format(
                LocalizationKeys.TodayStatusLine,
                minutes,
                snap.Keyboard,
                snap.MouseClicks,
                snap.Wheel) + _localization.GetString(state);
        }
        catch (Exception ex)
        {
            TodayStatusText.Text = _localization.Format(LocalizationKeys.StatusReadFailed, ex.Message);
        }
    }

    private async void TestButton_OnClick(object sender, RoutedEventArgs e)
    {
        FlushSelectedRule();
        if (!TryCollect(out var settings, out var error, out var errorArgs))
        {
            SetStatus(error, errorArgs);
            return;
        }

        Persist(settings);
        TestButton.IsEnabled = false;
        SetStatus(LocalizationKeys.SendingTest);
        try
        {
            var notifier = _services.GetRequiredService<INotifier>();
            await notifier.NotifyAsync(
                _localization.GetString(LocalizationKeys.TestNotifyTitle),
                _localization.GetString(LocalizationKeys.TestNotifyBody));
            SetStatus(LocalizationKeys.TestSent);
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationKeys.SendFailed, ex.Message);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (HolidayTag.IsChecked == true)
        {
            HolidayPanel.TrySave();
            return;
        }

        FlushSelectedRule();
        if (!TryCollect(out var settings, out var error, out var errorArgs))
        {
            SetStatus(error, errorArgs);
            return;
        }

        Persist(settings);
        StartupRegistration.Apply(settings.StartWithWindows);
        SetStatus(LocalizationKeys.Saved);
        RefreshRuleList(_ruleIndex);
    }

    private void Persist(UserSettingsFile settings)
    {
        UserSettingsStore.Save(settings);
        if (_services.GetService<IConfiguration>() is IConfigurationRoot root)
            root.Reload();
    }

    private bool TryCollect(out UserSettingsFile settings, out string error, out object[] errorArgs)
    {
        settings = new UserSettingsFile();
        error = "";
        errorArgs = [];

        foreach (var rule in _rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Title))
            {
                error = LocalizationKeys.RuleTitleRequired;
                return false;
            }

            if (rule.Kind == NotificationRuleKind.ProcessAbsent)
            {
                if (rule.ProcessNames.Count == 0)
                {
                    error = LocalizationKeys.RuleProcessRequired;
                    errorArgs = [rule.Title];
                    return false;
                }

                if (rule.AbsentMinutes <= 0)
                {
                    error = LocalizationKeys.RuleAbsentMinutesInvalid;
                    errorArgs = [rule.Title];
                    return false;
                }

                continue;
            }

            if (!TimeOnly.TryParse(rule.Time, out _))
            {
                error = LocalizationKeys.RuleTimeInvalid;
                errorArgs = [rule.Title];
                return false;
            }
        }

        StoreWebhookText(SelectedProvider);
        var notifications = new NotificationOptions
        {
            Provider = SelectedProvider,
            TeamsWebhookUrl = _teamsUrl,
            WeComWebhookUrl = _weComUrl,
            DingTalkWebhookUrl = _dingTalkUrl,
            Rules = _rules.Select(CloneRule).ToList()
        };

        settings = new UserSettingsFile
        {
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            Notifications = notifications,
            InputActivity = new InputActivityOptions
            {
                Enabled = InputStatsBox.IsChecked != false
            }
        };
        return true;
    }

    private NotificationProvider SelectedProvider =>
        ProviderCombo.SelectedItem is ProviderChoice choice
            ? choice.Value
            : NotificationProvider.Teams;

    private void StoreWebhookText(NotificationProvider provider)
    {
        var text = WebhookBox.Text.Trim();
        switch (provider)
        {
            case NotificationProvider.WeCom:
                _weComUrl = text;
                break;
            case NotificationProvider.DingTalk:
                _dingTalkUrl = text;
                break;
            default:
                _teamsUrl = text;
                break;
        }
    }

    private string WebhookFor(NotificationProvider provider) => provider switch
    {
        NotificationProvider.WeCom => _weComUrl,
        NotificationProvider.DingTalk => _dingTalkUrl,
        _ => _teamsUrl
    };

    private static NotificationRuleOptions CloneRule(NotificationRuleOptions rule) => new()
    {
        Id = rule.Id,
        Enabled = rule.Enabled,
        Kind = rule.Kind,
        Title = rule.Title,
        Message = rule.Message,
        Time = rule.Time,
        Schedule = rule.Schedule,
        ProcessNames = rule.ProcessNames.ToList(),
        AbsentMinutes = rule.AbsentMinutes,
        RequireAtOfficeMinutes = rule.RequireAtOfficeMinutes,
        SkewMinutes = rule.SkewMinutes
    };

    private void LanguageButton_OnClick(object sender, RoutedEventArgs e) =>
        _localization.ToggleLanguage();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildChoices();
        UpdateLanguageToggle();
        RefreshRuleList(_ruleIndex);
        RefreshTodayStatus();
        HolidayPanel.ApplyLanguage();
        ShowCurrentStatus();
    }

    private void PageTag_OnChecked(object sender, RoutedEventArgs e)
    {
        if (SettingsPage is null || HolidayPanel is null)
            return;

        var holiday = HolidayTag.IsChecked == true;
        SettingsPage.Visibility = holiday ? Visibility.Collapsed : Visibility.Visible;
        HolidayPanel.Visibility = holiday ? Visibility.Visible : Visibility.Collapsed;
        TestButton.Visibility = holiday ? Visibility.Collapsed : Visibility.Visible;
        if (holiday)
            HolidayPanel.EnsureReady();

        ShowCurrentStatus();
    }

    private void ShowCurrentStatus()
    {
        if (HolidayTag.IsChecked == true)
        {
            StatusText.Text = HolidayPanel.StatusKey is { } key
                ? _localization.Format(key, HolidayPanel.StatusArgs)
                : "";
            return;
        }

        StatusText.Text = _statusKey is null
            ? ""
            : _localization.Format(_statusKey, _statusArgs);
    }

    private bool ConfirmDelete(string name)
    {
        var answer = System.Windows.MessageBox.Show(
            _localization.Format(LocalizationKeys.DeleteConfirm, name),
            _localization.GetString(LocalizationKeys.AppName),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        return answer == MessageBoxResult.Yes;
    }

    private void RebuildChoices()
    {
        var provider = SelectedProvider;
        var kind = SelectedKind;
        var schedule = RuleSchedule.SelectedItem is ScheduleChoice current
            ? current.Value
            : NotificationSchedule.DailyWorkday;

        _suppressRuleEvents = true;
        ProviderCombo.ItemsSource = new[]
        {
            new ProviderChoice(NotificationProvider.Teams, _localization.GetString(LocalizationKeys.ProviderTeams)),
            new ProviderChoice(NotificationProvider.WeCom, _localization.GetString(LocalizationKeys.ProviderWeCom)),
            new ProviderChoice(NotificationProvider.DingTalk, _localization.GetString(LocalizationKeys.ProviderDingTalk)),
        };
        ProviderCombo.SelectedItem = ProviderCombo.Items
            .OfType<ProviderChoice>()
            .FirstOrDefault(x => x.Value == provider);

        RuleKind.ItemsSource = new[]
        {
            new KindChoice(NotificationRuleKind.Scheduled, _localization.GetString(LocalizationKeys.RuleKindScheduled)),
            new KindChoice(NotificationRuleKind.ProcessAbsent, _localization.GetString(LocalizationKeys.RuleKindProcessAbsent))
        };
        RuleKind.SelectedItem = RuleKind.Items
            .OfType<KindChoice>()
            .FirstOrDefault(x => x.Value == kind)
            ?? RuleKind.Items[0];

        RuleSchedule.ItemsSource = new[]
        {
            new ScheduleChoice(NotificationSchedule.DailyWorkday, _localization.GetString(LocalizationKeys.ScheduleDaily)),
            new ScheduleChoice(NotificationSchedule.LastWorkdayOfWeek, _localization.GetString(LocalizationKeys.ScheduleLastWeek)),
            new ScheduleChoice(NotificationSchedule.LastWorkdayOfMonth, _localization.GetString(LocalizationKeys.ScheduleLastMonth))
        };
        RuleSchedule.SelectedItem = RuleSchedule.Items
            .OfType<ScheduleChoice>()
            .FirstOrDefault(x => x.Value == schedule)
            ?? RuleSchedule.Items[0];
        _suppressRuleEvents = false;
        ApplyKindEditor(SelectedKind);
        UpdateWebhookHint();
    }

    private void RuleKind_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressRuleEvents) return;
        var kind = SelectedKind;
        ApplyKindEditor(kind);
        if (kind == NotificationRuleKind.ProcessAbsent)
            PrefillProcessRule();
    }

    private void ApplyKindEditor(NotificationRuleKind kind)
    {
        var scheduled = kind == NotificationRuleKind.Scheduled;
        SetRowVisible(RuleTimeLabel, RuleTime, scheduled);
        SetRowVisible(RuleOfficeLabel, RuleOfficeMinutes, scheduled);
        SetRowVisible(RuleProcessLabel, RuleProcess, !scheduled);
        SetRowVisible(RuleAbsentLabel, RuleAbsentMinutes, !scheduled);
    }

    private static void SetRowVisible(UIElement label, UIElement input, bool visible)
    {
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        label.Visibility = visibility;
        input.Visibility = visibility;
    }

    private void PrefillProcessRule()
    {
        if (string.IsNullOrWhiteSpace(RuleProcess.Text))
            RuleProcess.Text = "olk, OUTLOOK";
        if (!int.TryParse(RuleAbsentMinutes.Text.Trim(), out var minutes) || minutes <= 0)
            RuleAbsentMinutes.Text = "30";

        var genericTitle = _localization.GetString(LocalizationKeys.NewRuleTitle);
        if (string.IsNullOrWhiteSpace(RuleTitle.Text) || RuleTitle.Text.Trim() == genericTitle)
            RuleTitle.Text = _localization.GetString(LocalizationKeys.NewProcessRuleTitle);
        if (string.IsNullOrWhiteSpace(RuleMessage.Text))
            RuleMessage.Text = _localization.GetString(LocalizationKeys.NewProcessRuleMessage);
    }

    private string FormatRuleListItem(NotificationRuleOptions rule)
    {
        var title = string.IsNullOrWhiteSpace(rule.Title) ? rule.Id : rule.Title;
        if (rule.Kind == NotificationRuleKind.ProcessAbsent)
            return $"{_localization.GetString(LocalizationKeys.RuleKindProcessAbsent)}  {title}";
        return $"{rule.Time}  {title}";
    }

    private NotificationRuleKind SelectedKind =>
        RuleKind.SelectedItem is KindChoice choice
            ? choice.Value
            : NotificationRuleKind.Scheduled;

    private static List<string> ParseProcessNames(string text)
    {
        return ProcessPresence.Normalize(text.Split([',', '，', ';', '；', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)).ToList();
    }

    private void UpdateLanguageToggle()
    {
        var chinese = _localization.CurrentLanguage == AppLanguage.Chinese;
        ApplyLanguageLabel(ChineseLabel, chinese);
        ApplyLanguageLabel(EnglishLabel, !chinese);
    }

    private static void ApplyLanguageLabel(System.Windows.Controls.TextBlock label, bool active)
    {
        label.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        label.Opacity = active ? 1 : 0.55;
    }

    private void SetStatus(string key, params object[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        StatusText.Text = _localization.Format(key, args);
    }

    private sealed record ProviderChoice(NotificationProvider Value, string Name);
    private sealed record KindChoice(NotificationRuleKind Value, string Name);
    private sealed record ScheduleChoice(NotificationSchedule Value, string Name);
}
