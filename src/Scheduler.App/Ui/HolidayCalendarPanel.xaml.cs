using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Scheduler.App.Localization;
using Scheduler.Core.Apis.Holiday;
using Scheduler.Core.Apis.Holiday.Dtos;

namespace Scheduler.App.Ui;

public partial class HolidayCalendarPanel : System.Windows.Controls.UserControl
{
    private HolidayService? _holidays;
    private ILocalizationService? _localization;
    private readonly List<HolidayEntry> _entries = [];
    private bool _suppress;
    private bool _dirty;
    private bool _ready;
    private int _year = DateTime.Today.Year;
    private int _index = -1;
    private string? _statusKey;
    private object[] _statusArgs = [];

    public string? StatusKey => _statusKey;

    public object[] StatusArgs => _statusArgs;

    public event EventHandler? StatusChanged;

    public HolidayCalendarPanel()
    {
        InitializeComponent();
        EntryKind.DisplayMemberPath = nameof(KindChoice.Name);
    }

    public void Attach(HolidayService holidays, ILocalizationService localization)
    {
        _holidays = holidays;
        _localization = localization;
        RebuildKindChoices();
        UpdateFetchHint();
    }

    public void EnsureReady()
    {
        if (_holidays is null)
            return;

        if (!_ready)
        {
            _ready = true;
            BindYears(null);
            return;
        }

        if (!_dirty)
            BindYears(_year == 0 ? null : _year);
    }

    public void ApplyLanguage()
    {
        RebuildKindChoices();
        UpdateFetchHint();
        RefreshList(_index);
    }

    private async void FetchNextYear_OnClick(object sender, RoutedEventArgs e)
    {
        FetchNextButton.IsEnabled = false;
        try
        {
            await FetchNextYearAsync();
        }
        finally
        {
            FetchNextButton.IsEnabled = true;
        }
    }

    private void UpdateFetchHint()
    {
        if (_localization is null)
            return;

        FetchHint.Text = _localization.Format(LocalizationKeys.HolidayFetchHint, DateTime.Today.Year + 1);
    }

    public void TrySave()
    {
        FlushAt(_index);
        if (_holidays is null || _localization is null || _year == 0)
            return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<HolidayItem>(_entries.Count);
        foreach (var entry in _entries)
        {
            if (entry.Date is not { } date)
            {
                SetStatus(LocalizationKeys.HolidayDateRequired);
                return;
            }

            if (date.Year != _year)
            {
                SetStatus(LocalizationKeys.HolidayDateYear, _year);
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                SetStatus(LocalizationKeys.HolidayNameRequired);
                return;
            }

            var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!seen.Add(key))
            {
                SetStatus(LocalizationKeys.HolidayDuplicateDate, key);
                return;
            }

            items.Add(entry.ToItem());
        }

        try
        {
            _holidays.SaveYear(_year, items);
            _dirty = false;
            SetStatus(LocalizationKeys.HolidaySaved);
            RefreshList(_index);
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationKeys.HolidaySaveFailed, ex.Message);
        }
    }

    public async Task FetchNextYearAsync()
    {
        if (_holidays is null)
            return;

        var next = DateTime.Today.Year + 1;
        FlushAt(_index);
        if (_dirty && !ConfirmDiscard())
            return;

        SetStatus(LocalizationKeys.HolidayFetching, next);
        try
        {
            await _holidays.FetchYearAsync(next);
            BindYears(next);
            SetStatus(LocalizationKeys.HolidayFetched, next);
        }
        catch (HolidayNotPublishedException)
        {
            BindYears(DateTime.Today.Year);
            SetStatus(LocalizationKeys.HolidayFetchNotPublished, next);
        }
        catch (Exception ex)
        {
            SetStatus(LocalizationKeys.HolidayFetchFailed, ex.Message);
        }
    }

    private void BindYears(int? select)
    {
        if (_holidays is null)
            return;

        var years = _holidays.ListStoredYears();
        _suppress = true;
        YearCombo.ItemsSource = years;
        if (years.Count == 0)
        {
            _year = 0;
            YearCombo.SelectedItem = null;
            _suppress = false;
            _entries.Clear();
            _index = -1;
            _dirty = false;
            RefreshList(0, flush: false);
            return;
        }

        var pick = select is int wanted && years.Contains(wanted)
            ? wanted
            : years.Contains(DateTime.Today.Year) ? DateTime.Today.Year : years[^1];
        YearCombo.SelectedItem = pick;
        _year = pick;
        _suppress = false;
        LoadYear(pick);
    }

    private void RebuildKindChoices()
    {
        if (_localization is null)
            return;

        var rest = EntryKind.SelectedItem is KindChoice current && current.IsRest;
        _suppress = true;
        EntryKind.ItemsSource = new[]
        {
            new KindChoice(true, _localization.GetString(LocalizationKeys.HolidayRest)),
            new KindChoice(false, _localization.GetString(LocalizationKeys.HolidayMakeup))
        };
        EntryKind.SelectedItem = EntryKind.Items
            .OfType<KindChoice>()
            .FirstOrDefault(choice => choice.IsRest == rest)
            ?? EntryKind.Items[0];
        _suppress = false;
    }

    private void YearCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || YearCombo.SelectedItem is not int year || year == _year)
            return;

        FlushAt(_index);
        if (_dirty && !ConfirmDiscard())
        {
            _suppress = true;
            YearCombo.SelectedItem = _year;
            _suppress = false;
            return;
        }

        _year = year;
        LoadYear(year);
    }

    private void LoadYear(int year)
    {
        _entries.Clear();
        try
        {
            if (_holidays is not null)
            {
                foreach (var item in _holidays.CopyYear(year))
                    _entries.Add(HolidayEntry.FromItem(item));
            }

        }
        catch (Exception ex)
        {
            SetStatus(LocalizationKeys.HolidayReadFailed, ex.Message);
        }

        _index = -1;
        _dirty = false;
        RefreshList(0, flush: false);
    }

    private void RefreshList(int selectIndex, bool flush = true)
    {
        _suppress = true;
        if (flush)
            FlushAt(_index);
        var today = DateTime.Today;
        EntryList.ItemsSource = null;
        EntryList.ItemsSource = _entries.Select(entry => new HolidayListItem
        {
            Label = FormatEntry(entry),
            IsPast = entry.Date is { } date && date.Date < today
        }).ToList();
        if (_entries.Count == 0)
        {
            _index = -1;
            EntryList.SelectedIndex = -1;
            EntryEditor.IsEnabled = false;
        }
        else
        {
            EntryEditor.IsEnabled = true;
            _index = Math.Clamp(selectIndex, 0, _entries.Count - 1);
            EntryList.SelectedIndex = _index;
            BindSelected();
        }

        _suppress = false;
    }

    private void BindSelected()
    {
        if (_index < 0 || _index >= _entries.Count)
            return;

        var entry = _entries[_index];
        _suppress = true;
        EntryDate.SelectedDate = entry.Date;
        EntryName.Text = entry.Name;
        EntryKind.SelectedItem = EntryKind.Items
            .OfType<KindChoice>()
            .FirstOrDefault(choice => choice.IsRest == entry.IsRest)
            ?? EntryKind.Items[0];
        _suppress = false;
    }

    private void FlushAt(int index)
    {
        if (index < 0 || index >= _entries.Count || EntryEditor.IsEnabled == false && _entries.Count == 0)
            return;

        var entry = _entries[index];
        var date = EntryDate.SelectedDate?.Date;
        var name = EntryName.Text.Trim();
        var isRest = EntryKind.SelectedItem is KindChoice kind ? kind.IsRest : entry.IsRest;
        if (entry.Date == date && entry.Name == name && entry.IsRest == isRest)
            return;

        entry.Date = date;
        entry.Name = name;
        entry.IsRest = isRest;
        _dirty = true;
    }

    private void EntryList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress)
            return;

        FlushAt(_index);
        _index = EntryList.SelectedIndex;
        BindSelected();
    }

    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        if (_year is < 2000 or > 2100)
            return;

        FlushAt(_index);
        var date = new DateTime(_year, 1, 1);
        _entries.Add(new HolidayEntry
        {
            Date = date,
            Name = Text(LocalizationKeys.NewHolidayName),
            IsRest = true,
            Wage = 3
        });
        _dirty = true;
        RefreshList(_entries.Count - 1);
    }

    private void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        var index = EntryList.SelectedIndex;
        if (index < 0 || !ConfirmDelete(FormatEntry(_entries[index])))
            return;

        _entries.RemoveAt(index);
        _dirty = true;
        _index = -1;
        RefreshList(index, flush: false);
    }

    private bool ConfirmDiscard()
    {
        var answer = System.Windows.MessageBox.Show(
            Text(LocalizationKeys.HolidayDiscardPrompt),
            Text(LocalizationKeys.AppName),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        return answer == System.Windows.MessageBoxResult.Yes;
    }

    private bool ConfirmDelete(string name)
    {
        var answer = System.Windows.MessageBox.Show(
            _localization?.Format(LocalizationKeys.DeleteConfirm, name) ?? name,
            Text(LocalizationKeys.AppName),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        return answer == System.Windows.MessageBoxResult.Yes;
    }

    private string FormatEntry(HolidayEntry entry)
    {
        var date = entry.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "----------";
        var kind = entry.IsRest
            ? Text(LocalizationKeys.HolidayRest)
            : Text(LocalizationKeys.HolidayMakeup);
        return $"{date}  {entry.Name}  {kind}";
    }

    private void SetStatus(string? key, params object[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private string Text(string key) => _localization?.GetString(key) ?? key;

    private sealed class HolidayEntry
    {
        public DateTime? Date { get; set; }

        public string Name { get; set; } = "";

        public bool IsRest { get; set; } = true;

        public int Wage { get; set; } = 3;

        public int Rest { get; set; }

        public bool? After { get; set; }

        public string? Target { get; set; }

        public static HolidayEntry FromItem(HolidayItem item)
        {
            DateTime.TryParseExact(
                item.Date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date);
            return new HolidayEntry
            {
                Date = date == default ? null : date,
                Name = item.Name,
                IsRest = item.Holiday,
                Wage = item.Wage,
                Rest = item.Rest,
                After = item.After,
                Target = item.Target
            };
        }

        public HolidayItem ToItem()
        {
            var date = Date!.Value.Date;
            return new HolidayItem
            {
                Holiday = IsRest,
                Name = Name.Trim(),
                Wage = IsRest ? Wage is 2 or 3 ? Wage : 3 : 1,
                Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Rest = Rest,
                After = After,
                Target = Target
            };
        }
    }

    private sealed record KindChoice(bool IsRest, string Name);

    private sealed class HolidayListItem
    {
        public required string Label { get; init; }

        public bool IsPast { get; init; }
    }
}
