// Rig Buddy Studio installer. One window, four pages: folder, options, progress, done.
//
// Everything goes into the folder the user picks (default C:\RigBuddyStudio): the program, the services, and later also
// its settings, logs and map data. Outside that folder the installer only touches what Windows needs to know about:
// the firewall rules (so a phone can connect), the "Apps" entry for uninstalling, the shortcuts the user ticked, the
// game folders (the telemetry plugin, only when ticked) and, when ticked, the start-with-Windows entry.
//
//   RigBuddyStudio-Setup.exe                   wizard
//   RigBuddyStudio-Setup.exe /S [/D=<folder>]  silent install with the defaults (plugin on, shortcuts on); exit code 2:
//                                              installed, but the plugin is not in every game found (printed which)
//
// The uninstaller is RigBuddy.exe --uninstall (pc/host/Uninstaller.cs), so the folder holds no second copy of this exe.

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

static class Program
{
    public const string AppName = "Rig Buddy Studio";
    public const string ExeName = "RigBuddy.exe";
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RigBuddyStudio";
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValue = "Rig Buddy";
    public const string HostMutex = @"Local\RigBuddy.Host";
    public const string DefaultFolder = @"C:\RigBuddyStudio";

    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool silent = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase));
        string? dir = args.Select(a => a.StartsWith("/D=", StringComparison.OrdinalIgnoreCase) ? a[3..].Trim('"') : null).FirstOrDefault(a => a != null);
        var opts = new Options { Folder = dir ?? ExistingFolder() ?? DefaultFolder };
        if (silent)
        {
            try { Installer.Run(opts, Console.WriteLine); return Installer.PluginProblem ? 2 : 0; } // 2: installed, but a game lacks the plugin
            catch (Exception e) { Console.WriteLine($"install failed: {e.Message}"); return 1; }
        }
        Application.Run(new Wizard(opts));
        return 0;
    }

    /** The folder of an earlier install (an update goes to the same place by default). */
    static string? ExistingFolder()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(UninstallKey);
            return k?.GetValue("InstallLocation") as string is { Length: > 0 } p && Directory.Exists(p) ? p : null;
        }
        catch { return null; }
    }
}

sealed class Options
{
    public required string Folder { get; set; }
    public bool Plugin { get; set; } = true;
    public bool Autostart { get; set; }
    public bool DesktopShortcut { get; set; } = true;
    public bool StartMenuShortcut { get; set; } = true;
}

static class Installer
{
    /** Number of steps Run reports, for the progress bar. */
    public const int Steps = 6;

    public static Stream Payload() =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip") ?? throw new InvalidOperationException("payload.zip is not inside this installer");

    public static void Run(Options o, Action<string> say, Action? step = null)
    {
        string dir = Path.GetFullPath(o.Folder);
        string exe = Path.Combine(dir, Program.ExeName);

        say(T.Get("s.quit"));
        QuitRunning(exe);
        step?.Invoke();

        say(T.Get("s.files"));
        Directory.CreateDirectory(dir);
        // an update replaces the program as a whole; data\ and logs\ (the user's) are never touched
        foreach (string sub in new[] { "dist", "plugin", "node", "licenses" })
        {
            string p = Path.Combine(dir, sub);
            if (Directory.Exists(p)) Directory.Delete(p, recursive: true);
        }
        using (var zip = new ZipArchive(Payload(), ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;
                string target = Path.GetFullPath(Path.Combine(dir, entry.FullName));
                if (!target.StartsWith(dir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"bad path in payload: {entry.FullName}");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }
        }
        // the app runs as a normal user and writes only here
        foreach (string sub in new[] { "data", "logs" })
        {
            string p = Path.Combine(dir, sub);
            Directory.CreateDirectory(p);
            Run_("icacls.exe", $"\"{p}\" /grant *S-1-5-32-545:(OI)(CI)M /Q");
        }
        step?.Invoke();

        say(T.Get("s.firewall"));
        string node = Path.Combine(dir, @"node\node.exe");
        foreach (string rule in new[] { "Rig Buddy (Node.js)", "Rig Buddy (discovery)", "Rig Buddy (Studio)" })
            Run_("netsh.exe", $"advfirewall firewall delete rule name=\"{rule}\"");
        Run_("netsh.exe", $"advfirewall firewall delete rule name=all program=\"{node}\"");
        Run_("netsh.exe", $"advfirewall firewall delete rule name=all program=\"{exe}\"");
        // private and domain networks only; a "Public" one is handled by the app (it says so and offers the switch)
        Run_("netsh.exe", $"advfirewall firewall add rule name=\"Rig Buddy (Node.js)\" dir=in action=allow profile=private,domain protocol=TCP localport=62840,62843 program=\"{node}\"");
        Run_("netsh.exe", $"advfirewall firewall add rule name=\"Rig Buddy (discovery)\" dir=in action=allow profile=private,domain protocol=UDP localport=62846 program=\"{exe}\"");
        step?.Invoke();

        PluginReport = "";
        if (o.Plugin)
        {
            say(T.Get("s.plugin"));
            Run_(exe, "--install-plugin");
            Run_(exe, "--sync-sound");
            // what really is in the games now (the copies above fail without a word while a game runs)
            PluginReport = Report(Output(exe, "--plugin-status"));
            say(PluginReport);
        }
        step?.Invoke();

        say(T.Get("s.shortcuts"));
        RemoveShortcuts();
        if (o.DesktopShortcut) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Program.AppName + ".lnk"), exe, dir);
        if (o.StartMenuShortcut) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Program.AppName + ".lnk"), exe, dir);
        // Rig Buddy runs as administrator: Windows starts it at logon only from a scheduled task, not from the Run key
        using (var run = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Program.RunKey))
            run.DeleteValue(Program.RunValue, throwOnMissingValue: false);
        Run_(exe, o.Autostart ? "--set-autostart 1" : "--set-autostart 0");
        step?.Invoke();

        say(T.Get("s.register"));
        string version = FileVersionInfo.GetVersionInfo(exe).ProductVersion?.Split('+')[0] ?? "0";
        using (var k = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(Program.UninstallKey))
        {
            k.SetValue("DisplayName", Program.AppName);
            k.SetValue("DisplayVersion", version);
            k.SetValue("Publisher", "Orhan Kökbudak");
            k.SetValue("InstallLocation", dir);
            k.SetValue("DisplayIcon", exe);
            k.SetValue("UninstallString", $"\"{exe}\" --uninstall");
            k.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
            k.SetValue("EstimatedSize", (int)(Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / 1024), Microsoft.Win32.RegistryValueKind.DWord);
        }
        step?.Invoke();
        say(T.Get("s.done"));
    }

    /** The plugin check for the last page and the silent install's output. */
    public static string PluginReport { get; private set; } = "";
    public static bool PluginProblem { get; private set; }

    public static bool GameRunning() =>
        Process.GetProcessesByName("eurotrucks2").Length > 0 || Process.GetProcessesByName("amtrucks").Length > 0;

    /** "ETS2|ok|<dir>|<notes>" lines (RigBuddy.exe --plugin-status) -> one line per game for the user. */
    static string Report(string? status)
    {
        PluginProblem = false;
        if (status == null) { PluginProblem = true; return T.Get("p.unknown"); }
        var lines = new List<string>();
        foreach (var line in status.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line == "none") { lines.Add(T.Get("p.none")); continue; }
            var f = line.Split('|');
            if (f.Length < 4) continue;
            string game = f[0] == "ETS2" ? "Euro Truck Simulator 2" : f[0] == "ATS" ? "American Truck Simulator" : f[0];
            if (f[1] == "ok") lines.Add("✓ " + game + (f[3].Length > 0 ? $" ({f[3]})" : ""));
            else { PluginProblem = true; lines.Add("✗ " + game + ": " + f[3]); }
        }
        if (PluginProblem) lines.Add(T.Get("p.fix"));
        return lines.Count > 0 ? T.Get("p.title") + "\n" + string.Join("\n", lines) : T.Get("p.unknown");
    }

    /** Runs the app with a command and returns what it printed (null when it could not run). */
    static string? Output(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true });
            if (p == null) return null;
            string text = p.StandardOutput.ReadToEnd();
            p.WaitForExit(60_000);
            return text;
        }
        catch (Exception e) { Console.WriteLine($"{Path.GetFileName(file)}: {e.Message}"); return null; }
    }

    /** Asks a running copy to quit and waits until it is gone, so its files and ports are free. */
    static void QuitRunning(string exe)
    {
        if (!Mutex.TryOpenExisting(Program.HostMutex, out var m)) return;
        m.Dispose();
        if (File.Exists(exe)) Run_(exe, "--quit");
        for (int i = 0; i < 60; i++)
        {
            if (!Mutex.TryOpenExisting(Program.HostMutex, out var again)) return;
            again.Dispose();
            Thread.Sleep(250);
        }
        throw new InvalidOperationException(T.Get("e.running"));
    }

    static void RemoveShortcuts()
    {
        foreach (var folder in new[] { Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.CommonPrograms })
        {
            try { File.Delete(Path.Combine(Environment.GetFolderPath(folder), Program.AppName + ".lnk")); } catch { }
        }
    }

    static void Shortcut(string lnk, string target, string workDir)
    {
        Type? t = Type.GetTypeFromProgID("WScript.Shell");
        if (t == null) return;
        dynamic shell = Activator.CreateInstance(t)!;
        dynamic s = shell.CreateShortcut(lnk);
        s.TargetPath = target;
        s.WorkingDirectory = workDir;
        s.IconLocation = target;
        s.Description = Program.AppName;
        s.Save();
    }

    static void Run_(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            p?.WaitForExit(120_000);
        }
        catch (Exception e) { Console.WriteLine($"{Path.GetFileName(file)}: {e.Message}"); }
    }
}

/** Texts in the app's seven languages: English, Turkish, German, Russian, Portuguese, Spanish, French (Windows' display language; English otherwise). */
static class T
{
    static readonly string[] Codes = ["en", "tr", "de", "ru", "pt", "es", "fr"];
    static readonly int Lang = Math.Max(0, Array.IndexOf(Codes, System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));

    static readonly Dictionary<string, string[]> S = new()
    {
        ["title"] = ["Rig Buddy Studio setup", "Rig Buddy Studio kurulumu", "Rig Buddy Studio – Installation", "Установка Rig Buddy Studio", "Instalação do Rig Buddy Studio", "Instalación de Rig Buddy Studio", "Installation de Rig Buddy Studio"],
        ["welcome"] = ["Rig Buddy Studio will be installed in this folder. Everything it keeps (settings, logs, map data) stays in this folder too; nothing is written to your user profile.", "Rig Buddy Studio bu klasöre kurulacak. Ayarlar, loglar ve harita verileri de dahil her şey bu klasörde kalır; kullanıcı klasörüne hiçbir şey yazılmaz.", "Rig Buddy Studio wird in diesen Ordner installiert. Alles, was es speichert (Einstellungen, Protokolle, Kartendaten), bleibt ebenfalls in diesem Ordner; nichts wird in dein Benutzerprofil geschrieben.", "Rig Buddy Studio будет установлен в эту папку. Всё, что он хранит (настройки, журналы, данные карты), тоже остаётся в этой папке; в профиль пользователя ничего не записывается.", "O Rig Buddy Studio vai ser instalado nesta pasta. Tudo o que guarda (definições, registos, dados do mapa) fica também nesta pasta; nada é escrito no teu perfil de utilizador.", "Rig Buddy Studio se instalará en esta carpeta. Todo lo que guarda (ajustes, registros, datos del mapa) se queda también en esta carpeta; no se escribe nada en tu perfil de usuario.", "Rig Buddy Studio sera installé dans ce dossier. Tout ce qu'il conserve (réglages, journaux, données de carte) reste aussi dans ce dossier ; rien n'est écrit dans votre profil utilisateur."],
        ["folder"] = ["Install folder", "Kurulum klasörü", "Installationsordner", "Папка установки", "Pasta de instalação", "Carpeta de instalación", "Dossier d'installation"],
        ["browse"] = ["Browse...", "Gözat...", "Durchsuchen …", "Обзор…", "Procurar…", "Examinar…", "Parcourir…"],
        ["next"] = ["Next >", "İleri >", "Weiter >", "Далее >", "Seguinte >", "Siguiente >", "Suivant >"],
        ["back"] = ["< Back", "< Geri", "< Zurück", "< Назад", "< Anterior", "< Atrás", "< Précédent"],
        ["install"] = ["Install", "Kur", "Installieren", "Установить", "Instalar", "Instalar", "Installer"],
        ["cancel"] = ["Cancel", "İptal", "Abbrechen", "Отмена", "Cancelar", "Cancelar", "Annuler"],
        ["finish"] = ["Finish", "Bitir", "Fertig", "Готово", "Concluir", "Finalizar", "Terminer"],
        ["plugin"] = ["Install the telemetry plugin into ETS2 / ATS (needed for navigation, the trip computer and the live values)", "Telemetri eklentisini ETS2 / ATS'ye kur (navigasyon, yol bilgisayarı ve canlı değerler için gerekli)", "Das Telemetrie-Plugin in ETS2 / ATS installieren (nötig für Navigation, Bordcomputer und Live-Werte)", "Установить плагин телеметрии в ETS2 / ATS (нужен для навигации, бортового компьютера и данных в реальном времени)", "Instalar o plugin de telemetria no ETS2 / ATS (necessário para a navegação, o computador de bordo e os valores em direto)", "Instalar el plugin de telemetría en ETS2 / ATS (necesario para la navegación, el ordenador de a bordo y los valores en vivo)", "Installer le plugin de télémétrie dans ETS2 / ATS (nécessaire pour la navigation, l'ordinateur de bord et les valeurs en direct)"],
        ["autostart"] = ["Start with Windows", "Windows ile birlikte başlat", "Mit Windows starten", "Запускать вместе с Windows", "Iniciar com o Windows", "Iniciar con Windows", "Démarrer avec Windows"],
        ["desktop"] = ["Desktop shortcut", "Masaüstü kısayolu", "Desktop-Verknüpfung", "Ярлык на рабочем столе", "Atalho no ambiente de trabalho", "Acceso directo en el escritorio", "Raccourci sur le bureau"],
        ["startmenu"] = ["Start menu shortcut", "Başlat menüsü kısayolu", "Startmenü-Verknüpfung", "Ярлык в меню «Пуск»", "Atalho no menu Iniciar", "Acceso directo en el menú Inicio", "Raccourci dans le menu Démarrer"],
        ["options"] = ["Options", "Seçenekler", "Optionen", "Параметры", "Opções", "Opciones", "Options"],
        ["launch"] = ["Start Rig Buddy Studio now", "Rig Buddy Studio'yu şimdi başlat", "Rig Buddy Studio jetzt starten", "Запустить Rig Buddy Studio сейчас", "Iniciar o Rig Buddy Studio agora", "Iniciar Rig Buddy Studio ahora", "Lancer Rig Buddy Studio maintenant"],
        ["installed"] = ["Rig Buddy Studio is installed.", "Rig Buddy Studio kuruldu.", "Rig Buddy Studio ist installiert.", "Rig Buddy Studio установлен.", "O Rig Buddy Studio está instalado.", "Rig Buddy Studio está instalado.", "Rig Buddy Studio est installé."],
        ["failed"] = ["The installation failed:", "Kurulum başarısız oldu:", "Die Installation ist fehlgeschlagen:", "Установка не удалась:", "A instalação falhou:", "La instalación ha fallado:", "L'installation a échoué :"],
        ["warnpf"] = ["Program Files is read-only for normal users; the app keeps its data in its own folder. Pick a folder like C:\\RigBuddyStudio instead.", "Program Files normal kullanıcılar için salt okunurdur; uygulama verilerini kendi klasöründe tutar. C:\\RigBuddyStudio gibi bir klasör seçin.", "„Programme“ ist für normale Benutzer schreibgeschützt; die App speichert ihre Daten im eigenen Ordner. Wähle stattdessen einen Ordner wie C:\\RigBuddyStudio.", "Program Files доступна обычным пользователям только для чтения; приложение хранит данные в своей папке. Выберите папку вроде C:\\RigBuddyStudio.", "Program Files é só de leitura para utilizadores normais; a app guarda os dados na sua própria pasta. Escolhe uma pasta como C:\\RigBuddyStudio.", "Archivos de programa es de solo lectura para usuarios normales; la app guarda sus datos en su propia carpeta. Elige una carpeta como C:\\RigBuddyStudio.", "Program Files est en lecture seule pour les utilisateurs normaux ; l'appli garde ses données dans son propre dossier. Choisissez plutôt un dossier comme C:\\RigBuddyStudio."],
        ["s.quit"] = ["Closing a running copy...", "Çalışan kopya kapatılıyor...", "Laufende Instanz wird geschlossen …", "Закрывается запущенная копия…", "A fechar a cópia em execução…", "Cerrando la copia en ejecución…", "Fermeture de la copie en cours…"],
        ["s.files"] = ["Copying files...", "Dosyalar kopyalanıyor...", "Dateien werden kopiert …", "Копирование файлов…", "A copiar ficheiros…", "Copiando archivos…", "Copie des fichiers…"],
        ["s.firewall"] = ["Allowing the phone to connect (Windows firewall, private networks)...", "Telefonun bağlanmasına izin veriliyor (Windows güvenlik duvarı, özel ağlar)...", "Verbindung des Handys wird erlaubt (Windows-Firewall, private Netzwerke) …", "Разрешается подключение телефона (брандмауэр Windows, частные сети)…", "A permitir a ligação do telemóvel (firewall do Windows, redes privadas)…", "Permitiendo la conexión del móvil (firewall de Windows, redes privadas)…", "Autorisation de la connexion du téléphone (pare-feu Windows, réseaux privés)…"],
        ["s.plugin"] = ["Installing the telemetry plugin into the games...", "Telemetri eklentisi oyunlara kuruluyor...", "Telemetrie-Plugin wird in die Spiele installiert …", "Установка плагина телеметрии в игры…", "A instalar o plugin de telemetria nos jogos…", "Instalando el plugin de telemetría en los juegos…", "Installation du plugin de télémétrie dans les jeux…"],
        ["s.shortcuts"] = ["Shortcuts...", "Kısayollar...", "Verknüpfungen …", "Ярлыки…", "Atalhos…", "Accesos directos…", "Raccourcis…"],
        ["s.register"] = ["Registering for uninstall...", "Kaldırma kaydı yapılıyor...", "Wird für die Deinstallation registriert …", "Регистрация для удаления…", "A registar para desinstalação…", "Registrando para la desinstalación…", "Enregistrement pour la désinstallation…"],
        ["s.done"] = ["Done.", "Tamam.", "Fertig.", "Готово.", "Feito.", "Hecho.", "Terminé."],
        ["p.title"] = ["Game plugin:", "Oyun eklentisi:", "Spiel-Plugin:", "Плагин игры:", "Plugin do jogo:", "Plugin del juego:", "Plugin du jeu :"],
        ["p.none"] = ["No ETS2 / ATS was found (Steam). Rig Buddy puts the plugin into a game it finds later on its own.", "ETS2 / ATS bulunamadı (Steam). Rig Buddy, sonradan bulduğu oyuna eklentiyi kendisi kurar.", "Kein ETS2 / ATS gefunden (Steam). Rig Buddy installiert das Plugin selbst in ein Spiel, das es später findet.", "ETS2 / ATS не найдены (Steam). Rig Buddy сам установит плагин в игру, которую найдёт позже.", "Não foi encontrado nenhum ETS2 / ATS (Steam). O Rig Buddy instala o plugin sozinho num jogo que encontre mais tarde.", "No se encontró ningún ETS2 / ATS (Steam). Rig Buddy instala el plugin por sí mismo en un juego que encuentre más tarde.", "Aucun ETS2 / ATS trouvé (Steam). Rig Buddy installe lui-même le plugin dans un jeu qu'il trouvera plus tard."],
        ["p.unknown"] = ["Could not check the game plugin.", "Oyun eklentisi kontrol edilemedi.", "Das Spiel-Plugin konnte nicht geprüft werden.", "Не удалось проверить плагин игры.", "Não foi possível verificar o plugin do jogo.", "No se pudo comprobar el plugin del juego.", "Le plugin du jeu n'a pas pu être vérifié."],
        ["p.fix"] = ["Close the game and start Rig Buddy Studio: it offers \"Fix now\" for the plugin.", "Oyunu kapatıp Rig Buddy Studio'yu aç: eklenti için \"Şimdi düzelt\" düğmesini gösterir.", "Schließe das Spiel und starte Rig Buddy Studio: es bietet „Jetzt beheben“ für das Plugin an.", "Закройте игру и запустите Rig Buddy Studio: он предложит «Исправить сейчас» для плагина.", "Fecha o jogo e inicia o Rig Buddy Studio: ele oferece \"Corrigir agora\" para o plugin.", "Cierra el juego e inicia Rig Buddy Studio: ofrece \"Corregir ahora\" para el plugin.", "Fermez le jeu et lancez Rig Buddy Studio : il propose « Corriger maintenant » pour le plugin."],
        ["g.running"] = ["Euro Truck Simulator 2 or American Truck Simulator is running. The game plugin can only be put into a game that is closed.\n\nClose the game and press Retry, or Ignore to install without it now.", "Euro Truck Simulator 2 veya American Truck Simulator açık. Oyun eklentisi yalnızca kapalı oyuna kurulabilir.\n\nOyunu kapatıp Yeniden Dene'ye bas, ya da şimdilik eklentisiz kurmak için Yoksay'a bas.", "Euro Truck Simulator 2 oder American Truck Simulator läuft. Das Spiel-Plugin kann nur in ein geschlossenes Spiel gelegt werden.\n\nSchließe das Spiel und drücke „Wiederholen“, oder „Ignorieren“, um jetzt ohne es zu installieren.", "Запущен Euro Truck Simulator 2 или American Truck Simulator. Плагин можно установить только в закрытую игру.\n\nЗакройте игру и нажмите «Повторить» или «Пропустить», чтобы установить без него.", "O Euro Truck Simulator 2 ou o American Truck Simulator está aberto. O plugin do jogo só pode ser colocado num jogo fechado.\n\nFecha o jogo e carrega em Repetir, ou em Ignorar para instalar sem ele agora.", "Euro Truck Simulator 2 o American Truck Simulator está abierto. El plugin del juego solo se puede poner en un juego cerrado.\n\nCierra el juego y pulsa Reintentar, u Omitir para instalar sin él ahora.", "Euro Truck Simulator 2 ou American Truck Simulator est ouvert. Le plugin du jeu ne peut être placé que dans un jeu fermé.\n\nFermez le jeu et appuyez sur Réessayer, ou sur Ignorer pour installer sans lui maintenant."],
        ["e.running"] = ["Rig Buddy Studio is still running. Close it (tray icon > Exit) and try again.", "Rig Buddy Studio hâlâ çalışıyor. Kapatın (tepsi simgesi > Çıkış) ve tekrar deneyin.", "Rig Buddy Studio läuft noch. Schließe es (Tray-Symbol > Beenden) und versuche es erneut.", "Rig Buddy Studio всё ещё запущен. Закройте его (значок в трее > Выход) и повторите.", "O Rig Buddy Studio ainda está a correr. Fecha-o (ícone da bandeja > Sair) e tenta outra vez.", "Rig Buddy Studio sigue en marcha. Ciérralo (icono de la bandeja > Salir) y vuelve a intentarlo.", "Rig Buddy Studio est toujours en cours. Fermez-le (icône de la zone de notification > Quitter) et réessayez."],
    };

    public static string Get(string key) => S.TryGetValue(key, out var t) ? (t.Length > Lang && t[Lang].Length > 0 ? t[Lang] : t[0]) : key;
}

sealed class Wizard : Form
{
    readonly Options _o;
    readonly Panel[] _pages = new Panel[4];
    readonly TextBox _folder = new();
    readonly Label _warn = new() { AutoSize = false, ForeColor = Color.DarkOrange };
    readonly CheckBox _plugin = new(), _autostart = new(), _desktop = new(), _startMenu = new(), _launch = new();
    readonly Button _back = new(), _next = new(), _cancel = new();
    readonly ProgressBar _bar = new();
    readonly Label _status = new() { AutoSize = false };
    readonly Label _result = new() { AutoSize = false };
    int _page;
    bool _busy, _ok;

    public Wizard(Options o)
    {
        _o = o;
        Text = T.Get("title");
        ClientSize = new Size(560, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { }

        for (int i = 0; i < _pages.Length; i++)
        {
            _pages[i] = new Panel { Location = new Point(0, 0), Size = new Size(560, 270), Visible = false };
            Controls.Add(_pages[i]);
        }

        // page 0: folder
        _pages[0].Controls.Add(new Label { Text = Program.AppName, Font = new Font("Segoe UI", 16F, FontStyle.Bold), Location = new Point(24, 20), AutoSize = true });
        _pages[0].Controls.Add(new Label { Text = T.Get("welcome"), Location = new Point(24, 66), Size = new Size(512, 56) });
        _pages[0].Controls.Add(new Label { Text = T.Get("folder"), Location = new Point(24, 138), AutoSize = true });
        _folder.Text = o.Folder;
        _folder.Location = new Point(24, 160);
        _folder.Size = new Size(410, 24);
        _folder.TextChanged += (_, _) => CheckFolder();
        var browse = new Button { Text = T.Get("browse"), Location = new Point(444, 158), Size = new Size(92, 28) };
        browse.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { SelectedPath = _folder.Text, ShowNewFolderButton = true };
            if (d.ShowDialog(this) == DialogResult.OK) _folder.Text = d.SelectedPath;
        };
        _warn.Location = new Point(24, 196);
        _warn.Size = new Size(512, 40);
        _pages[0].Controls.AddRange([_folder, browse, _warn]);

        // page 1: options
        _pages[1].Controls.Add(new Label { Text = T.Get("options"), Font = new Font("Segoe UI", 12F, FontStyle.Bold), Location = new Point(24, 20), AutoSize = true });
        int y = 62;
        foreach (var (box, key, on) in new[] { (_plugin, "plugin", o.Plugin), (_autostart, "autostart", o.Autostart), (_desktop, "desktop", o.DesktopShortcut), (_startMenu, "startmenu", o.StartMenuShortcut) })
        {
            box.Text = T.Get(key);
            box.Checked = on;
            box.Location = new Point(28, y);
            box.Size = new Size(508, 40);
            _pages[1].Controls.Add(box);
            y += 44;
        }

        // page 2: progress
        _status.Location = new Point(24, 90);
        _status.Size = new Size(512, 40);
        _bar.Location = new Point(24, 140);
        _bar.Size = new Size(512, 22);
        _bar.Maximum = Installer.Steps;
        _pages[2].Controls.AddRange([_status, _bar]);

        // page 3: done
        _result.Location = new Point(24, 24);
        _result.Size = new Size(512, 180);
        _result.Font = new Font("Segoe UI", 10F);
        _launch.Text = T.Get("launch");
        _launch.Checked = true;
        _launch.Location = new Point(28, 214);
        _launch.Size = new Size(508, 24);
        _pages[3].Controls.AddRange([_result, _launch]);

        _back.Text = T.Get("back"); _back.Location = new Point(290, 288); _back.Size = new Size(84, 30);
        _next.Text = T.Get("next"); _next.Location = new Point(380, 288); _next.Size = new Size(84, 30);
        _cancel.Text = T.Get("cancel"); _cancel.Location = new Point(470, 288); _cancel.Size = new Size(84, 30);
        _back.Click += (_, _) => Show(_page - 1);
        _next.Click += (_, _) => Next();
        _cancel.Click += (_, _) => Close();
        Controls.AddRange([_back, _next, _cancel]);
        CheckFolder();
        Show(0);
    }

    void CheckFolder()
    {
        string f = _folder.Text.Trim();
        bool pf = f.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase)
               || f.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase);
        _warn.Text = pf ? T.Get("warnpf") : "";
        _next.Enabled = f.Length > 3 && Path.IsPathRooted(f);
    }

    void Show(int page)
    {
        _page = Math.Clamp(page, 0, 3);
        for (int i = 0; i < _pages.Length; i++) _pages[i].Visible = i == _page;
        _back.Visible = _page is 1;
        _next.Visible = _page != 2;
        _next.Text = _page switch { 1 => T.Get("install"), 3 => T.Get("finish"), _ => T.Get("next") };
        _cancel.Visible = _page != 3;
        _cancel.Enabled = !_busy;
    }

    void Next()
    {
        switch (_page)
        {
            case 0:
                _o.Folder = _folder.Text.Trim();
                Show(1);
                break;
            case 1:
                _o.Plugin = _plugin.Checked; _o.Autostart = _autostart.Checked;
                _o.DesktopShortcut = _desktop.Checked; _o.StartMenuShortcut = _startMenu.Checked;
                // the plugin cannot be replaced in a running game
                while (_o.Plugin && Installer.GameRunning())
                {
                    var r = MessageBox.Show(this, T.Get("g.running"), T.Get("title"), MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Warning);
                    if (r == DialogResult.Abort) return;
                    if (r == DialogResult.Ignore) break;
                }
                Show(2);
                _ = InstallAsync();
                break;
            case 3:
                if (_ok && _launch.Checked) Launch();
                Close();
                break;
        }
    }

    async Task InstallAsync()
    {
        _busy = true; _cancel.Enabled = false;
        string? error = null;
        try
        {
            await Task.Run(() => Installer.Run(_o, s => BeginInvoke(() => _status.Text = s), () => BeginInvoke(() => _bar.Value = Math.Min(_bar.Maximum, _bar.Value + 1))));
            _ok = true;
        }
        catch (Exception e) { error = e.Message; }
        _busy = false;
        _result.Text = _ok ? T.Get("installed") + "\n" + Path.GetFullPath(_o.Folder) + (Installer.PluginReport.Length > 0 ? "\n\n" + Installer.PluginReport : "")
            : T.Get("failed") + "\n\n" + error;
        _result.ForeColor = _ok && Installer.PluginProblem ? Color.DarkOrange : SystemColors.ControlText;
        _launch.Visible = _ok;
        Show(3);
    }

    /** Starts the app as the normal user, not as the elevated installer (explorer.exe hands it to the user's session). */
    void Launch()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path.Combine(_o.Folder, Program.ExeName)}\"") { UseShellExecute = false }); }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy) e.Cancel = true;
        base.OnFormClosing(e);
    }
}
