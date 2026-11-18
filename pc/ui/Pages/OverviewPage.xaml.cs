using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace RigBuddy.Ui.Pages;

/**
 * The start page: the state of everything at a glance, the telemetry service (address, small service tiles).
 */
public partial class OverviewPage : UserControl
{
    readonly IHostBridge _host;
    string? _qrFor;
    readonly ObservableCollection<ServiceVm> _services = new();

    public OverviewPage(IHostBridge host)
    {
        _host = host;
        InitializeComponent();
        Services.ItemsSource = _services;
        Localize();
    }

    public void Localize()
    {
        Title.Text = Strings.T("ov.title");
        SumTelemLabel.Text = Strings.B("TELEMETRİ AKIŞI", "TELEMETRY STREAM");
        SumPhoneLabel.Text = Strings.B("MOBİL CİHAZLAR", "MOBILE DEVICES");
        SumSysLabel.Text = Strings.B("SİSTEM", "SYSTEM");
        SvcTitle.Text = Strings.B("TELEMETRİ SERVİSİ", "TELEMETRY SERVICE");
        AddressLabel.Text = Forms.Upper(Strings.T("sys.address"));
        CopyButton.Content = Strings.T("sys.copy");
        AppLabel.Text = Strings.B("TELEFON UYGULAMASI (ANDROID)", "PHONE APP (ANDROID)");
        AppHint.Text = Strings.B("Telefonun tarayıcısında bu adresi aç, indir ve kur.", "Open this address in the phone's browser, download and install.");
        CopyAppButton.Content = Strings.B("Uygulama adresini kopyala", "Copy app address");
        LogsButton.Content = Strings.T("sys.logs");
        ExitButton.Content = Strings.T("sys.exit");
    }

    string? _appUrl;
    void OnCopy(object sender, RoutedEventArgs e) => _host.CopyAddress();
    void OnCopyApp(object sender, RoutedEventArgs e) { try { if (_appUrl != null) Clipboard.SetText(_appUrl); } catch { } }
    void OnLogs(object sender, RoutedEventArgs e) => _host.ShowLogs();
    void OnExit(object sender, RoutedEventArgs e) => _host.Exit();
    void OnRestart(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is string n) _host.RestartService(n); }

    /** Everything the page shows that comes from the program itself, once per half second. */
    public void Apply(HostSnapshot s, bool game)
    {
        GameLine.Text = game ? "" : Strings.T("ov.waiting");
        SumTelem.Text = Strings.B(game ? "Çalışıyor" : "Bekliyor", game ? "Running" : "Waiting");
        SumTelem.Foreground = Forms.Brh(game ? "Ok" : "Text2");
        SumTelemSub.Text = s.Prefs.Game.ToUpperInvariant();
        SumPhone.Text = s.Clients.ToString();
        SumPhoneSub.Text = s.Address;
        SumSys.Text = s.Headline;
        SumSys.Foreground = HealthBrush.Of(s.HeadlineHealth);
        SumSysBar.Value = s.Percent;
        SumSysBar.Visibility = s.Percent is > 0 and < 100 ? Visibility.Visible : Visibility.Collapsed;

        Detail.Text = s.Detail;
        Address.Text = s.Address;
        Pairing.Text = s.Pairing;
        _appUrl = s.AppUrl;
        AppRow.Visibility = s.AppUrl != null ? Visibility.Visible : Visibility.Collapsed;
        CopyAppButton.Visibility = AppRow.Visibility;
        AppUrl.Text = s.AppUrl ?? "";
        if (s.AppUrl != _qrFor) { _qrFor = s.AppUrl; AppQr.Source = s.AppUrl == null ? null : QrImage.Of(s.AppUrl); }
        foreach (var row in s.Services)
        {
            var vm = _services.FirstOrDefault(x => x.Name == row.Name);
            if (vm == null) _services.Add(vm = new ServiceVm { Name = row.Name });
            vm.Title = row.Title;
            vm.State = row.State;
            vm.Dot = HealthBrush.Of(row.Health);
        }
    }
}
