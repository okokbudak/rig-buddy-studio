namespace RigBuddy.Ui;

/**
 * The window's texts: Turkish and English here, the other five languages in Translations.cs; a missing translation falls
 * back to English. RigHardware.exe (rig-buddy-hardware) builds this file into itself and adds its own (Strings.Hardware.cs).
 */
public static partial class Strings
{
    static Strings() => AddTexts(Table);
    static partial void AddTexts(Dictionary<string, (string Tr, string En)> table);

    public static string Code { get; set; } = "en";

    /** Texts the host knows in all its languages (set by the window); used for keys this table lacks. */
    public static Func<string, object?[], string>? Host { get; set; }

    public static string T(string key, params object?[] args)
    {
        string text = key;
        if (Table.TryGetValue(key, out var v)) text = Pick(v.Tr, v.En);
        else if (Host != null) return Host(key, args);
        return args.Length == 0 ? text : string.Format(text, args);
    }

    /** Inline two-language text for editor labels (moves into Table when more languages are translated). */
    public static string B(string tr, string en, params object?[] args) => args.Length == 0 ? Pick(tr, en) : string.Format(Pick(tr, en), args);

    /** Turkish, or the English text translated into the current language (English when there is no translation). */
    public static string Pick(string tr, string en) => Code == "tr" ? tr : Code == "en" ? en : Translations.Of(Code, en) ?? en;

    static readonly Dictionary<string, (string Tr, string En)> Table = new()
    {
        ["nav.overview"] = ("Genel Bakış", "Overview"),
        ["nav.system"] = ("Sistem", "System"),
        ["nav.logs"] = ("Günlük", "Logs"),
        ["ov.title"] = ("Genel Bakış", "Overview"),
        ["ov.waiting"] = ("Oyun bekleniyor: ETS2 veya ATS'yi telemetri eklentisiyle aç", "Waiting for the game: start ETS2 or ATS with the telemetry plugin"),
        ["ov.live"] = ("Canlı telemetri", "Live telemetry"),
        ["ov.speed"] = ("Hız", "Speed"),
        ["ov.rpm"] = ("Devir", "RPM"),
        ["ov.gear"] = ("Vites", "Gear"),
        ["ov.fuel"] = ("Yakıt", "Fuel"),
        ["ov.clock"] = ("Oyun saati", "Game clock"),
        ["ov.devices"] = ("Cihazlar", "Devices"),
        ["ov.none"] = ("Henüz cihaz yok. Cihazlar sayfasından ekle.", "No devices yet. Add one on the Devices page."),
        ["dev.channels"] = ("Göstergeler", "Gauges"),
        ["dev.channelsHint"] = ("Her satır bir değer gönderir: ad, formül (ör. truck.speed.kph), birim ve ölçek.", "Each row sends one value: name, formula (e.g. truck.speed.kph), unit and scale."),
        ["dev.channelsBoard"] = ("Kartın motor soketlerine göre sıralıdır. Ölçek kartın kendi ayarından gelir.", "In the order of the board's motor sockets. The scale comes from the board's own settings."),
        ["dev.test"] = ("Dene", "Test"),
        ["dev.addChannel"] = ("Gösterge ekle", "Add gauge"),
        ["dev.newChannel"] = ("Gösterge {0}", "Gauge {0}"),
        ["sys.title"] = ("Sistem", "System"),
        ["sys.address"] = ("Telefon bu adrese bağlanır", "The phone connects to this address"),
        ["sys.copy"] = ("Kopyala", "Copy"),
        ["sys.services"] = ("Servisler", "Services"),
        ["sys.restart"] = ("Yeniden başlat", "Restart"),
        ["sys.map"] = ("Harita verisi", "Map data"),
        ["sys.build"] = ("Oluştur", "Build"),
        ["sys.update"] = ("Güncelle", "Update"),
        ["sys.clients"] = ("Bağlı telefon: {0}", "Phones connected: {0}"),
        ["sys.exit"] = ("Çıkış", "Exit"),
        ["sys.logs"] = ("Günlükleri aç", "Open logs"),
        ["wheel.tablet.next"] = ("Tablet: sonraki", "Tablet: next"),
        ["wheel.tablet.prev"] = ("Tablet: önceki", "Tablet: previous"),
    };
}
