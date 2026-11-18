// UI text in the supported languages. The setting (HKCU\Software\Rig Buddy\
// Language) defaults to "system": Windows' display language when supported,
// English otherwise. Keys missing a translation fall back to English.

using System.Globalization;
using Microsoft.Win32;

static class L
{


    public static readonly (string Code, string Name)[] Languages =
    [
        ("tr", "Türkçe"), ("en", "English"), ("de", "Deutsch"), ("ru", "Русский"),
        ("pt", "Português"), ("es", "Español"), ("fr", "Français"),
    ];

    public static string Code { get; private set; } = Resolve();

    /** Raised when the language changes (UI is rebuilt). */
    public static event Action? Changed;

    public static string Setting
    {
        get
        {
            return Settings.Get("Language") ?? "system";
        }
        set
        {
            Settings.Set("Language", value);
            string code = Resolve();
            if (code == Code) return;
            Code = code;
            Changed?.Invoke();
        }
    }

    public static string T(string key)
    {
        if (!Strings.TryGetValue(key, out var t)) return key;
        int i = Array.FindIndex(Languages, l => l.Code == Code);
        return i >= 0 && i < t.Length && !string.IsNullOrEmpty(t[i]) ? t[i] : t[1];
    }

    public static string T(string key, params object?[] args) => string.Format(T(key), args);

    public static string LanguageName(string setting) =>
        setting == "system" ? T("lang.system") : Languages.FirstOrDefault(l => l.Code == setting).Name ?? setting;

    static string Resolve()
    {
        string s = Setting;
        if (s != "system" && Languages.Any(l => l.Code == s)) return s;
        string ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Any(l => l.Code == ui) ? ui : "en";
    }

    // key -> [tr, en, de, ru, pt, es, fr]
    static readonly Dictionary<string, string[]> Strings = new()
    {
        // tray
        ["tray.open"] = ["Pencereyi aç", "Open window", "Fenster öffnen", "Открыть окно", "Abrir janela", "Abrir ventana", "Ouvrir la fenêtre"],
        ["tray.restart_hint"] = ["Tıkla: yeniden başlat", "Click: restart", "Klicken: neu starten", "Нажмите: перезапустить", "Clique: reiniciar", "Clic: reiniciar", "Clic : redémarrer"],
        ["tray.copy_hint"] = ["Tıkla: kopyala", "Click: copy", "Klicken: kopieren", "Нажмите: копировать", "Clique: copiar", "Clic: copiar", "Clic : copier"],
        ["un.ask"] = ["{0} klasöründeki Rig Buddy Studio kaldırılsın mı?", "Uninstall Rig Buddy Studio from {0}?", "Rig Buddy Studio aus {0} entfernen?", "Удалить Rig Buddy Studio из {0}?", "Desinstalar o Rig Buddy Studio de {0}?", "¿Desinstalar Rig Buddy Studio de {0}?", "Désinstaller Rig Buddy Studio de {0} ?"],
        ["un.data"] = ["Evet: ayarlar, loglar ve harita verileri de ({0}) silinir.\nHayır: program silinir, verileriniz kalır.", "Yes: settings, logs and map data ({0}) are deleted too.\nNo: the program goes, your data stays.", "Ja: Einstellungen, Protokolle und Kartendaten ({0}) werden mitgelöscht.\nNein: nur das Programm, Ihre Daten bleiben.", "Да: настройки, журналы и данные карты ({0}) тоже будут удалены.\nНет: программа удаляется, данные остаются.", "Sim: configurações, logs e dados de mapa ({0}) também são excluídos.\nNão: só o programa, seus dados ficam.", "Sí: también se borran ajustes, registros y datos de mapa ({0}).\nNo: se quita el programa, tus datos se quedan.", "Oui : réglages, journaux et données de carte ({0}) sont aussi supprimés.\nNon : le programme part, vos données restent."],
        ["un.done"] = ["Rig Buddy Studio kaldırıldı.", "Rig Buddy Studio has been uninstalled.", "Rig Buddy Studio wurde entfernt.", "Rig Buddy Studio удалён.", "O Rig Buddy Studio foi desinstalado.", "Rig Buddy Studio se ha desinstalado.", "Rig Buddy Studio a été désinstallé."],
        ["tray.logs"] = ["Logları göster", "Show logs", "Protokolle anzeigen", "Показать журналы", "Mostrar logs", "Mostrar registros", "Afficher les journaux"],
        ["tray.game"] = ["Oyun (tek harita)", "Game (one map)", "Spiel (eine Karte)", "Игра (одна карта)", "Jogo (um mapa)", "Juego (un mapa)", "Jeu (une carte)"],
        ["tray.mods"] = ["Harita modları (ETS2)", "Map mods (ETS2)", "Karten-Mods (ETS2)", "Моды карты (ETS2)", "Mods de mapa (ETS2)", "Mods de mapa (ETS2)", "Mods de carte (ETS2)"],
        ["tray.mods_pick"] = ["Mod dosyalarını seç…", "Choose mod files…", "Mod-Dateien wählen…", "Выбрать файлы модов…", "Escolher arquivos de mod…", "Elegir archivos de mod…", "Choisir les fichiers de mod…"],
        ["tray.mods_clear"] = ["Modları kaldır", "Remove mods", "Mods entfernen", "Убрать моды", "Remover mods", "Quitar mods", "Retirer les mods"],
        ["tray.mods_count"] = ["Seçili: {0} dosya", "Selected: {0} files", "Ausgewählt: {0} Dateien", "Выбрано: {0}", "Selecionados: {0}", "Seleccionados: {0}", "Sélectionnés : {0}"],
        ["plugin.missing"] = ["Telemetri eklentisi eksik ({0}): oyun Rig Buddy'ye veri gönderemez.", "Telemetry plugin missing ({0}): the game can't send data to Rig Buddy.", "Telemetrie-Plugin fehlt ({0}): das Spiel kann keine Daten an Rig Buddy senden.", "Нет плагина телеметрии ({0}): игра не может отправлять данные в Rig Buddy.", "Plugin de telemetria ausente ({0}): o jogo não envia dados ao Rig Buddy.", "Falta el plugin de telemetría ({0}): el juego no puede enviar datos a Rig Buddy.", "Plugin de télémétrie manquant ({0}) : le jeu ne peut pas envoyer de données à Rig Buddy."],
        ["net.public"] = ["Ağınız (\"{0}\") Windows'ta \"Genel\" görünüyor: telefon bu bilgisayara bağlanamaz. \"Özel yap\" ile düzeltin.", "Your network (\"{0}\") is set to \"Public\" in Windows: the phone cannot reach this PC. Click \"Make private\" to fix it.", "Ihr Netzwerk (\"{0}\") ist in Windows auf \"Öffentlich\" gestellt: das Telefon erreicht diesen PC nicht. Mit \"Privat\" beheben.", "Сеть (\"{0}\") в Windows помечена как «Общедоступная»: телефон не сможет подключиться к этому ПК. Нажмите «Сделать частной».", "Sua rede (\"{0}\") está como \"Pública\" no Windows: o celular não alcança este PC. Clique em \"Tornar privada\".", "Tu red (\"{0}\") está como \"Pública\" en Windows: el teléfono no puede llegar a este PC. Pulsa \"Hacer privada\".", "Votre réseau (\"{0}\") est \"Public\" dans Windows : le téléphone ne peut pas joindre ce PC. Cliquez sur \"Rendre privé\"."],
        ["update.available"] = ["Yeni sürüm: {0} (indir)", "New version: {0} (download)", "Neue Version: {0} (herunterladen)", "Новая версия: {0} (скачать)", "Nova versão: {0} (baixar)", "Nueva versión: {0} (descargar)", "Nouvelle version : {0} (télécharger)"],
        ["update.balloon"] = ["Rig Buddy {0} yayınlandı. Simge menüsünden indirebilirsiniz; uygulamayı da güncellemeyi unutmayın.", "Rig Buddy {0} is out. Download it from the tray menu; update the app too.", "Rig Buddy {0} ist erschienen. Im Tray-Menü herunterladen; die App auch aktualisieren.", "Вышла Rig Buddy {0}. Скачайте из меню в трее и обновите приложение.", "O Rig Buddy {0} saiu. Baixe pelo menu da bandeja e atualize o app também.", "Ha salido Rig Buddy {0}. Descárgalo desde el menú de la bandeja y actualiza también la app.", "Rig Buddy {0} est disponible. Téléchargez-le depuis le menu et mettez aussi l'appli à jour."],
        ["update.prompt_title"] = ["Rig Buddy güncellemesi", "Rig Buddy update", "Rig Buddy-Update", "Обновление Rig Buddy", "Atualização do Rig Buddy", "Actualización de Rig Buddy", "Mise à jour de Rig Buddy"],
        ["update.prompt"] = ["Rig Buddy {0} çıktı (bu bilgisayarda {1} kurulu). Şimdi indirip kurmak ister misin?", "Rig Buddy {0} is out (this PC has {1}). Download and install it now?", "Rig Buddy {0} ist erschienen (dieser PC hat {1}). Jetzt herunterladen und installieren?", "Вышел Rig Buddy {0} (на этом ПК {1}). Скачать и установить сейчас?", "O Rig Buddy {0} saiu (este PC tem a {1}). Transferir e instalar agora?", "Ha salido Rig Buddy {0} (este PC tiene la {1}). ¿Descargarla e instalarla ahora?", "Rig Buddy {0} est sorti (ce PC a la {1}). Le télécharger et l'installer maintenant ?"],
        ["update.notes"] = ["Neler değişti?", "What's new?", "Was ist neu?", "Что нового?", "O que há de novo?", "¿Qué hay de nuevo?", "Quoi de neuf ?"],
        ["update.install"] = ["İndir ve kur", "Download and install", "Herunterladen und installieren", "Скачать и установить", "Transferir e instalar", "Descargar e instalar", "Télécharger et installer"],
        ["update.skip"] = ["Bu sürümü atla", "Skip this version", "Diese Version überspringen", "Пропустить эту версию", "Ignorar esta versão", "Omitir esta versión", "Ignorer cette version"],
        ["update.later"] = ["Sonra", "Later", "Später", "Позже", "Mais tarde", "Más tarde", "Plus tard"],
        ["update.downloading"] = ["İndiriliyor… {0} / {1} MB", "Downloading… {0} / {1} MB", "Wird heruntergeladen … {0} / {1} MB", "Загрузка… {0} / {1} МБ", "A transferir… {0} / {1} MB", "Descargando… {0} / {1} MB", "Téléchargement… {0} / {1} Mo"],
        ["update.starting"] = ["Kurulum başlatılıyor…", "Starting the installer…", "Installationsprogramm wird gestartet …", "Запуск установщика…", "A iniciar o instalador…", "Iniciando el instalador…", "Lancement du programme d'installation…"],
        ["update.bad_hash"] = ["indirilen dosya GitHub'daki dosyayla aynı değil, çalıştırılmadı", "the download does not match the file on GitHub; it was not run", "der Download stimmt nicht mit der Datei auf GitHub überein; er wurde nicht ausgeführt", "загруженный файл не совпадает с файлом на GitHub; он не запущен", "a transferência não corresponde ao ficheiro no GitHub; não foi executada", "la descarga no coincide con el archivo de GitHub; no se ejecutó", "le téléchargement ne correspond pas au fichier sur GitHub ; il n'a pas été lancé"],
        ["update.failed"] = ["Güncelleme kurulamadı: {0}", "The update could not be installed: {0}", "Das Update konnte nicht installiert werden: {0}", "Не удалось установить обновление: {0}", "Não foi possível instalar a atualização: {0}", "No se pudo instalar la actualización: {0}", "La mise à jour n'a pas pu être installée : {0}"],
        ["update.check"] = ["Güncellemeleri denetle", "Check for updates", "Nach Updates suchen", "Проверять обновления", "Verificar atualizações", "Buscar actualizaciones", "Rechercher des mises à jour"],
        ["update.check_hint"] = ["Açıkken GitHub'a birkaç saatte bir yeni sürüm olup olmadığı sorulur (başka bir şey gönderilmez).", "When on, GitHub is asked every few hours whether a newer version exists (nothing else is sent).", "Fragt GitHub alle paar Stunden nach einer neuen Version (sonst wird nichts gesendet).", "Раз в несколько часов запрашивает у GitHub наличие новой версии (больше ничего не отправляется).", "Pergunta ao GitHub a cada poucas horas se há versão nova (nada mais é enviado).", "Pregunta a GitHub cada pocas horas si hay una versión nueva (no se envía nada más).", "Demande à GitHub toutes les quelques heures s'il existe une version plus récente (rien d'autre n'est envoyé)."],
        // sound mods window (tr, en; the other languages fall back to English)
        ["wheel.button"] = ["tuş {0}", "button {0}", "Taste {0}", "кнопка {0}", "botão {0}", "botón {0}", "bouton {0}"],
        ["sound.open"] = ["Ses modları…", "Sound mods…", "Sound-Mods …", "Звуковые моды…", "Mods de som…", "Mods de sonido…", "Mods de son…"],
        ["sound.title"] = ["Rig Buddy: ses modları", "Rig Buddy: sound mods", "Rig Buddy: Sound-Mods", "Rig Buddy: звуковые моды", "Rig Buddy: mods de som", "Rig Buddy: mods de sonido", "Rig Buddy : mods de son"],
        ["sound.on"] = ["Ses plugin'i açık (oyunlara kurulu olsun)", "Sound plugin on (installed in the games)", "Sound-Plugin an (in den Spielen installiert)", "Звуковой плагин включён (установлен в играх)", "Plugin de som ligado (instalado nos jogos)", "Plugin de sonido activado (instalado en los juegos)", "Plugin de son activé (installé dans les jeux)"],
        ["sound.add"] = ["Mod ekle…", "Add mods…", "Mods hinzufügen …", "Добавить моды…", "Adicionar mods…", "Añadir mods…", "Ajouter des mods…"],
        ["sound.remove"] = ["Kaldır", "Remove", "Entfernen", "Удалить", "Remover", "Quitar", "Retirer"],
        ["sound.remove_confirm"] = ["{0} modu listeden kaldırılsın mı? Mod dosyaları (.bank, .bank.guids) klasörden silinir; geri almak için modu yeniden eklemeniz gerekir.", "Remove the mod {0}? Its files (.bank, .bank.guids) are deleted from the folder; to get it back you add the mod again.", "Den Mod {0} entfernen? Seine Dateien (.bank, .bank.guids) werden aus dem Ordner gelöscht; um ihn zurückzubekommen, füge den Mod erneut hinzu.", "Удалить мод {0}? Его файлы (.bank, .bank.guids) будут удалены из папки; чтобы вернуть его, добавьте мод снова.", "Remover o mod {0}? Os seus ficheiros (.bank, .bank.guids) são apagados da pasta; para o recuperar, adiciona o mod outra vez.", "¿Quitar el mod {0}? Sus archivos (.bank, .bank.guids) se borran de la carpeta; para recuperarlo, añade el mod de nuevo.", "Retirer le mod {0} ? Ses fichiers (.bank, .bank.guids) sont supprimés du dossier ; pour le récupérer, ajoutez à nouveau le mod."],
        ["sound.removed"] = ["{0} kaldırıldı.", "{0} removed.", "{0} entfernt.", "{0} удалён.", "{0} removido.", "{0} quitado.", "{0} retiré."],
        ["sound.remove_failed"] = ["{0} listeden çıkarıldı ama dosyaları silinemedi (oyun hâlâ kullanıyor olabilir). Oyunu kapatıp tekrar deneyin.", "{0} is out of the list but its files could not be deleted (the game may still hold them). Close the game and try again.", "{0} ist aus der Liste, aber seine Dateien konnten nicht gelöscht werden (das Spiel hält sie vielleicht noch). Schließe das Spiel und versuche es erneut.", "{0} убран из списка, но его файлы удалить не удалось (возможно, их держит игра). Закройте игру и попробуйте снова.", "{0} saiu da lista, mas os seus ficheiros não puderam ser apagados (o jogo pode ainda estar a usá-los). Fecha o jogo e tenta de novo.", "{0} ya no está en la lista, pero sus archivos no se pudieron borrar (puede que el juego aún los use). Cierra el juego y vuelve a intentarlo.", "{0} est retiré de la liste mais ses fichiers n'ont pas pu être supprimés (le jeu les utilise peut-être encore). Fermez le jeu et réessayez."],
        ["sound.up"] = ["Yukarı", "Up", "Nach oben", "Вверх", "Para cima", "Subir", "Monter"],
        ["sound.down"] = ["Aşağı", "Down", "Nach unten", "Вниз", "Para baixo", "Bajar", "Descendre"],
        ["sound.folder"] = ["Klasörü aç", "Open folder", "Ordner öffnen", "Открыть папку", "Abrir pasta", "Abrir carpeta", "Ouvrir le dossier"],
        ["sound.levels"] = ["Ses seviyeleri", "Sound levels", "Lautstärken", "Громкость", "Volumes", "Volúmenes", "Volumes"],
        ["sound.save"] = ["Kaydet", "Save", "Speichern", "Сохранить", "Guardar", "Guardar", "Enregistrer"],
        ["sound.saved"] = ["Kaydedildi. Oyun açıksa bir saniye içinde yüklenir.", "Saved. If the game is running, it loads within a second.", "Gespeichert. Läuft das Spiel, wird es innerhalb einer Sekunde geladen.", "Сохранено. Если игра запущена, изменения загрузятся за секунду.", "Guardado. Se o jogo estiver a correr, carrega num segundo.", "Guardado. Si el juego está en marcha, se carga en un segundo.", "Enregistré. Si le jeu tourne, c'est chargé en une seconde."],
        ["sound.lv.master"] = ["Ana ses", "Master", "Gesamt", "Общая", "Geral", "General", "Général"],
        ["sound.lv.engine"] = ["Motor", "Engine", "Motor", "Двигатель", "Motor", "Motor", "Moteur"],
        ["sound.lv.exhaust"] = ["Egzoz", "Exhaust", "Auspuff", "Выхлоп", "Escape", "Escape", "Échappement"],
        ["sound.lv.turbo"] = ["Turbo", "Turbo", "Turbo", "Турбина", "Turbo", "Turbo", "Turbo"],
        ["sound.lv.interior"] = ["Kabin içi", "Inside the cab", "In der Kabine", "В кабине", "Dentro da cabine", "Dentro de la cabina", "Dans la cabine"],
        ["sound.lv.navigation"] = ["Navigasyon sesi", "Navigation voice", "Navigationsstimme", "Голос навигатора", "Voz da navegação", "Voz de navegación", "Voix de navigation"],
        ["sound.lv.outside"] = ["Dış ses (camlar kapalıyken)", "Outside, windows closed", "Außen, Fenster zu", "Снаружи, окна закрыты", "Exterior, janelas fechadas", "Exterior, ventanillas cerradas", "Extérieur, vitres fermées"],
        ["sound.no_guids"] = ["Eksik: {0}.bank.guids dosyası yok, bu mod ses çalmaz. Modu .scs arşiviyle ekleyin ya da .guids dosyasını .bank'in yanına koyup tekrar ekleyin.", "Missing: no .bank.guids file for {0}, so it plays nothing. Add the mod from its .scs archive, or put the .guids next to the .bank and add it again.", "Fehlt: keine .bank.guids-Datei für {0}, daher spielt er nichts ab. Füge den Mod aus seinem .scs-Archiv hinzu oder lege die .guids neben die .bank und füge ihn erneut hinzu.", "Не хватает файла .bank.guids для {0}, поэтому он ничего не воспроизводит. Добавьте мод из его архива .scs или положите .guids рядом с .bank и добавьте снова.", "Falta o ficheiro .bank.guids de {0}, por isso não toca nada. Adiciona o mod a partir do seu arquivo .scs, ou põe o .guids ao lado do .bank e adiciona-o outra vez.", "Falta el archivo .bank.guids de {0}, así que no reproduce nada. Añade el mod desde su archivo .scs, o pon el .guids junto al .bank y añádelo de nuevo.", "Il manque le fichier .bank.guids de {0}, il ne joue donc rien. Ajoutez le mod depuis son archive .scs, ou placez le .guids à côté du .bank et ajoutez-le à nouveau."],
        ["sound.none_in_scs"] = ["{0} içinde ses modu (sound/truck/*.bank) bulunamadı.", "No sound mod (sound/truck/*.bank) found in {0}.", "Kein Sound-Mod (sound/truck/*.bank) in {0} gefunden.", "В {0} не найден звуковой мод (sound/truck/*.bank).", "Nenhum mod de som (sound/truck/*.bank) encontrado em {0}.", "No se encontró ningún mod de sonido (sound/truck/*.bank) en {0}.", "Aucun mod de son (sound/truck/*.bank) trouvé dans {0}."],
        ["sound.not_zip"] = ["{0} açılamadı: ZIP olmayan bir .scs (bu biçim desteklenmiyor). İçinden .bank ve .guids dosyalarını çıkarıp ekleyin.", "Could not open {0}: it is not a zip-based .scs (not supported). Take the .bank and .guids files out and add them.", "{0} konnte nicht geöffnet werden: es ist keine zip-basierte .scs (nicht unterstützt). Nimm die .bank- und .guids-Dateien heraus und füge sie hinzu.", "Не удалось открыть {0}: это не .scs на основе zip (не поддерживается). Извлеките файлы .bank и .guids и добавьте их.", "Não foi possível abrir {0}: não é um .scs baseado em zip (não suportado). Retira os ficheiros .bank e .guids e adiciona-os.", "No se pudo abrir {0}: no es un .scs basado en zip (no compatible). Saca los archivos .bank y .guids y añádelos.", "Impossible d'ouvrir {0} : ce n'est pas un .scs au format zip (non pris en charge). Extrayez les fichiers .bank et .guids et ajoutez-les."],
        ["sound.foreign"] = ["Oyunda ayrıca elle kurulmuş bir ts-fmod-plugin var: sesler iki kez duyulur. Onu plugins klasöründen kaldırın.", "There is also a ts-fmod-plugin installed by hand: you will hear the sounds twice. Remove it from the plugins folder.", "Es ist auch ein von Hand installiertes ts-fmod-plugin vorhanden: du hörst die Klänge doppelt. Entferne es aus dem plugins-Ordner.", "Также установлен вручную ts-fmod-plugin: звуки будут звучать дважды. Удалите его из папки plugins.", "Há também um ts-fmod-plugin instalado à mão: vais ouvir os sons duas vezes. Remove-o da pasta plugins.", "También hay un ts-fmod-plugin instalado a mano: oirás los sonidos dos veces. Quítalo de la carpeta plugins.", "Un ts-fmod-plugin installé à la main est aussi présent : vous entendrez les sons deux fois. Retirez-le du dossier plugins."],
        ["sound.synced"] = ["Tamam.", "Done.", "Erledigt.", "Готово.", "Feito.", "Hecho.", "Terminé."],
        ["sound.sync_failed"] = ["Bir oyunda yapılamadı (oyun açık olabilir).", "Could not do it in one game (it may be running).", "In einem Spiel ging es nicht (es läuft vielleicht).", "В одной из игр не получилось (возможно, она запущена).", "Não foi possível num dos jogos (pode estar a correr).", "No se pudo en uno de los juegos (puede que esté en marcha).", "Impossible dans l'un des jeux (il tourne peut-être)."],
        ["sound.nogame"] = ["Kurulu ETS2 / ATS bulunamadı.", "No ETS2 / ATS installation found.", "Keine ETS2-/ATS-Installation gefunden.", "Установка ETS2 / ATS не найдена.", "Nenhuma instalação de ETS2 / ATS encontrada.", "No se encontró ninguna instalación de ETS2 / ATS.", "Aucune installation d'ETS2 / ATS trouvée."],
        ["sound.empty"] = ["Henüz ses modu yok: \"Mod ekle\" ile .bank dosyalarını seçin.", "No sound mods yet: use \"Add mods\" to pick .bank files.", "Noch keine Sound-Mods: wähle mit „Mods hinzufügen“ .bank-Dateien aus.", "Звуковых модов пока нет: выберите файлы .bank через «Добавить моды».", "Ainda não há mods de som: usa \"Adicionar mods\" para escolher ficheiros .bank.", "Aún no hay mods de sonido: usa \"Añadir mods\" para elegir archivos .bank.", "Pas encore de mods de son : utilisez « Ajouter des mods » pour choisir des fichiers .bank."],
        ["sound.hint"] = ["Yüklenecek ses modlarını işaretleyip Kaydet'e basın (yukarıdan aşağıya; aynı sesi birden fazlası getiriyorsa ilki kazanır). Oyun açıksa değişiklik bir saniye içinde ulaşır; ses seviyeleri de öyle. Bir mod motor, egzoz ya da turbo sesi getirdiğinde Rig Buddy oyunun kendi sesini onun için kısar. Eklenti yalnızca yapıldığı oyun sürümünde çalışır; yeni oyun sürümü yeni bir Rig Buddy sürümü ister.", "Tick the sound mods to load and press Save (top to bottom; when several have the same sound the first one wins). If the game is running, the change reaches it within a second; so do the sound levels. When a mod brings the engine, exhaust or turbo, Rig Buddy mutes the game's own for it. The plugin only works on the game version it was made for; a new game version needs a new Rig Buddy release.", "Hake die Sound-Mods an, die geladen werden sollen, und drücke Speichern (von oben nach unten; haben mehrere denselben Klang, gewinnt der erste). Läuft das Spiel, erreicht es die Änderung innerhalb einer Sekunde, ebenso die Lautstärken. Bringt ein Mod Motor, Auspuff oder Turbo mit, schaltet Rig Buddy die eigenen Klänge des Spiels dafür stumm. Das Plugin funktioniert nur mit der Spielversion, für die es gemacht ist; eine neue Spielversion braucht ein neues Rig Buddy.", "Отметьте звуковые моды для загрузки и нажмите «Сохранить» (сверху вниз; если у нескольких один и тот же звук, побеждает первый). Если игра запущена, изменение доходит до неё за секунду, как и громкость. Если мод приносит двигатель, выхлоп или турбину, Rig Buddy сам глушит собственные звуки игры. Плагин работает только с той версией игры, для которой сделан; новой версии игры нужен новый Rig Buddy.", "Marca os mods de som a carregar e carrega em Guardar (de cima para baixo; quando vários têm o mesmo som, ganha o primeiro). Se o jogo estiver a correr, a alteração chega num segundo, tal como os volumes. Quando um mod traz motor, escape ou turbo, o Rig Buddy silencia os sons próprios do jogo. O plugin só funciona na versão do jogo para a qual foi feito; uma nova versão do jogo precisa de um novo Rig Buddy.", "Marca los mods de sonido que se cargarán y pulsa Guardar (de arriba abajo; si varios tienen el mismo sonido, gana el primero). Si el juego está en marcha, el cambio le llega en un segundo, igual que los volúmenes. Cuando un mod trae motor, escape o turbo, Rig Buddy silencia los sonidos propios del juego. El plugin solo funciona con la versión del juego para la que se hizo; una nueva versión del juego necesita un nuevo Rig Buddy.", "Cochez les mods de son à charger et appuyez sur Enregistrer (de haut en bas ; si plusieurs ont le même son, le premier l'emporte). Si le jeu tourne, le changement lui parvient en une seconde, comme les volumes. Quand un mod apporte le moteur, l'échappement ou le turbo, Rig Buddy coupe les sons propres du jeu. Le plugin ne fonctionne qu'avec la version du jeu pour laquelle il a été fait ; une nouvelle version du jeu demande un nouveau Rig Buddy."],
        ["net.fix"] = ["Özel yap", "Make private", "Privat", "Сделать частной", "Tornar privada", "Hacer privada", "Rendre privé"],
        ["plugin.fix"] = ["Şimdi düzelt", "Fix now", "Jetzt beheben", "Исправить", "Corrigir agora", "Arreglar ahora", "Corriger"],
        ["plugin.restart"] = ["Eklenti kuruldu. Oyunu kapatıp yeniden aç.", "Plugin installed. Restart the game.", "Plugin installiert. Spiel neu starten.", "Плагин установлен. Перезапустите игру.", "Plugin instalado. Reinicie o jogo.", "Plugin instalado. Reinicia el juego.", "Plugin installé. Relancez le jeu."],
        ["plugin.closegame"] = ["Oyun açık, eklenti kurulamaz. Oyunu kapat, sonra \"Şimdi düzelt\"e bas.", "The game is running, so the plugin can't be installed. Close the game, then click \"Fix now\".", "Das Spiel läuft, das Plugin kann nicht installiert werden. Spiel schließen, dann \"Jetzt beheben\".", "Игра запущена, плагин не установить. Закройте игру и нажмите «Исправить».", "O jogo está aberto, não dá para instalar o plugin. Feche o jogo e clique em \"Corrigir agora\".", "El juego está abierto y no se puede instalar el plugin. Ciérralo y pulsa \"Arreglar ahora\".", "Le jeu est ouvert, le plugin ne peut pas être installé. Fermez le jeu puis cliquez sur \"Corriger\"."],
        ["plugin.failed"] = ["Kurulamadı. Yönetici izni gerekebilir; tekrar dene.", "Could not install. Administrator rights may be needed; try again.", "Installation fehlgeschlagen. Administratorrechte nötig? Erneut versuchen.", "Не удалось установить. Возможно, нужны права администратора.", "Não foi possível instalar. Pode exigir administrador.", "No se pudo instalar. Puede requerir permisos de administrador.", "Échec. Des droits administrateur sont peut-être nécessaires."],
        ["tray.theme"] = ["Tema", "Theme", "Design", "Тема", "Tema", "Tema", "Thème"],
        ["tray.language"] = ["Dil", "Language", "Sprache", "Язык", "Idioma", "Idioma", "Langue"],
        ["tray.exit"] = ["Çıkış", "Exit", "Beenden", "Выход", "Sair", "Salir", "Quitter"],
        ["tray.hidden"] = [
            "Arka planda çalışmaya devam ediyor. Kapatmak için simgeye sağ tıklayıp Çıkış'ı seçin.",
            "Still running in the background. To quit, right-click the icon and choose Exit.",
            "Läuft im Hintergrund weiter. Zum Beenden mit der rechten Maustaste auf das Symbol klicken und „Beenden“ wählen.",
            "Продолжает работать в фоне. Чтобы выйти, щёлкните значок правой кнопкой и выберите «Выход».",
            "Continua em execução em segundo plano. Para sair, clique com o botão direito no ícone e escolha Sair.",
            "Sigue funcionando en segundo plano. Para salir, haz clic derecho en el icono y elige Salir.",
            "Continue de fonctionner en arrière-plan. Pour quitter, faites un clic droit sur l’icône et choisissez Quitter.",
        ],
        ["tray.game_on"] = ["Oyun: bağlı", "Game: connected", "Spiel: verbunden", "Игра: подключена", "Jogo: conectado", "Juego: conectado", "Jeu : connecté"],
        ["tray.game_off"] = ["Oyun: bekleniyor", "Game: waiting", "Spiel: wird erwartet", "Игра: ожидание", "Jogo: aguardando", "Juego: esperando", "Jeu : en attente"],
        ["tray.pair"] = ["Uygulama kodu: {0}", "App code: {0}", "App-Code: {0}", "Код для приложения: {0}", "Código do app: {0}", "Código de la app: {0}", "Code de l’appli : {0}"],
        ["tray.forget"] = ["Eşleşen cihazları unut", "Forget paired devices", "Gekoppelte Geräte vergessen", "Забыть сопряжённые устройства", "Esquecer dispositivos pareados", "Olvidar dispositivos emparejados", "Oublier les appareils appairés"],
        ["tray.forget_hint"] = ["Telefonlar ve tabletler kodu yeniden girmek zorunda kalır", "Phones and tablets have to enter a code again", "Handys und Tablets müssen den Code erneut eingeben", "Телефонам и планшетам придётся снова ввести код", "Celulares e tablets precisarão digitar o código de novo", "Los móviles y tablets tendrán que volver a introducir el código", "Les téléphones et tablettes devront ressaisir un code"],
        ["tray.forget_ask"] = ["Eşleşen tüm cihazlar unutulsun mu? Uygulamalar kodu yeniden girmek zorunda kalır.", "Forget all paired devices? The apps will have to enter a code again.", "Alle gekoppelten Geräte vergessen? Die Apps müssen den Code erneut eingeben.", "Забыть все сопряжённые устройства? Приложениям придётся снова ввести код.", "Esquecer todos os dispositivos pareados? Os apps precisarão digitar o código de novo.", "¿Olvidar todos los dispositivos emparejados? Las apps tendrán que volver a introducir el código.", "Oublier tous les appareils appairés ? Les applis devront ressaisir un code."],
        ["tray.address"] = ["PC adresi: {0}", "PC address: {0}", "PC-Adresse: {0}", "Адрес ПК: {0}", "Endereço do PC: {0}", "Dirección del PC: {0}", "Adresse du PC : {0}"],
        ["tray.no_network"] = ["PC adresi: ağ yok", "PC address: no network", "PC-Adresse: kein Netzwerk", "Адрес ПК: нет сети", "Endereço do PC: sem rede", "Dirección del PC: sin red", "Adresse du PC : pas de réseau"],
        ["state.running"] = ["çalışıyor", "running", "läuft", "работает", "em execução", "en marcha", "en cours"],
        ["state.starting"] = ["başlatılıyor…", "starting…", "startet…", "запускается…", "iniciando…", "iniciando…", "démarrage…"],
        ["state.restarting"] = ["yeniden başlatılacak", "restarting soon", "startet gleich neu", "скоро перезапуск", "reiniciando em breve", "se reiniciará pronto", "redémarrage imminent"],
        ["state.waiting"] = ["bekliyor", "waiting", "wartet", "ожидание", "aguardando", "esperando", "en attente"],
        ["state.stopped"] = ["durduruldu", "stopped", "gestoppt", "остановлен", "parado", "detenido", "arrêté"],
        ["tip.ready"] = ["hazır", "ready", "bereit", "готов", "pronto", "listo", "prêt"],
        ["tip.starting"] = ["başlatılıyor", "starting", "startet", "запуск", "iniciando", "iniciando", "démarrage"],
        ["tip.setup"] = ["kurulum eksik", "setup incomplete", "Einrichtung unvollständig", "установка не завершена", "instalação incompleta", "instalación incompleta", "installation incomplète"],
        ["tip.game"] = ["oyun bağlı", "game connected", "Spiel verbunden", "игра подключена", "jogo conectado", "juego conectado", "jeu connecté"],

        // main window
        ["win.subtitle"] = ["PC servisi · v{0}", "PC service · v{0}", "PC-Dienst · v{0}", "Служба ПК · v{0}", "Serviço do PC · v{0}", "Servicio del PC · v{0}", "Service PC · v{0}"],
        ["win.theme"] = ["Tema", "Theme", "Design", "Тема", "Tema", "Tema", "Thème"],
        ["win.language"] = ["Dil", "Language", "Sprache", "Язык", "Idioma", "Idioma", "Langue"],
        ["lang.auto"] = ["Otomatik", "Auto", "Automatisch", "Авто", "Automático", "Automático", "Auto"],
        ["win.preparing"] = ["Hazırlanıyor…", "Getting ready…", "Wird vorbereitet…", "Подготовка…", "Preparando…", "Preparando…", "Préparation…"],
        ["win.ready"] = ["Hazır", "Ready", "Bereit", "Готово", "Pronto", "Listo", "Prêt"],
        ["win.setup"] = ["Kurulum eksik", "Setup incomplete", "Einrichtung unvollständig", "Установка не завершена", "Instalação incompleta", "Instalación incompleta", "Installation incomplète"],
        ["win.game_connected"] = ["Oyun bağlı, iyi yolculuklar", "Game connected, drive safe", "Spiel verbunden, gute Fahrt", "Игра подключена, счастливого пути", "Jogo conectado, boa viagem", "Juego conectado, buen viaje", "Jeu connecté, bonne route"],
        ["win.open_game"] = ["Oyunu açabilirsiniz", "You can start the game", "Du kannst das Spiel starten", "Можно запускать игру", "Você já pode abrir o jogo", "Ya puedes abrir el juego", "Vous pouvez lancer le jeu"],
        ["win.server_stopped"] = ["Navigasyon sunucusu durduruldu", "Navigation server stopped", "Navigationsserver gestoppt", "Сервер навигации остановлен", "Servidor de navegação parado", "Servidor de navegación detenido", "Serveur de navigation arrêté"],
        ["win.address_title"] = ["Uygulamada girilecek PC adresi", "PC address to enter in the app", "PC-Adresse für die App", "Адрес ПК для ввода в приложении", "Endereço do PC para digitar no app", "Dirección del PC para la app", "Adresse du PC à saisir dans l’appli"],
        ["win.no_network"] = ["Ağ bağlantısı yok", "No network", "Kein Netzwerk", "Нет сети", "Sem rede", "Sin red", "Pas de réseau"],
        ["win.copied"] = ["Kopyalandı ✓", "Copied ✓", "Kopiert ✓", "Скопировано ✓", "Copiado ✓", "Copiado ✓", "Copié ✓"],
        ["win.pair"] = ["Uygulama kodu: {0}  ·  uygulamada bir kez girin", "App code: {0}  ·  enter it once in the app", "App-Code: {0}  ·  einmal in der App eingeben", "Код: {0}  ·  введите один раз в приложении", "Código: {0}  ·  digite uma vez no app", "Código: {0}  ·  introdúcelo una vez en la app", "Code : {0}  ·  à saisir une fois dans l’appli"],
        ["win.click_copy"] = ["Tıklayınca kopyalanır", "Click to copy", "Klicken zum Kopieren", "Нажмите, чтобы скопировать", "Clique para copiar", "Haz clic para copiar", "Cliquez pour copier"],
        ["win.features"] = ["Özellikler", "Features", "Funktionen", "Функции", "Recursos", "Funciones", "Fonctions"],
        ["win.autostart"] = ["Windows açılışında başlat", "Start with Windows", "Mit Windows starten", "Запускать вместе с Windows", "Iniciar com o Windows", "Iniciar con Windows", "Lancer avec Windows"],
        ["win.restart"] = ["Servisleri yeniden başlat", "Restart services", "Dienste neu starten", "Перезапустить службы", "Reiniciar serviços", "Reiniciar servicios", "Redémarrer les services"],
        ["win.logs"] = ["Loglar", "Logs", "Protokolle", "Журналы", "Logs", "Registros", "Journaux"],
        ["win.services"] = ["Hizmetler", "Services", "Dienste", "Службы", "Serviços", "Servicios", "Services"],
        ["win.map"] = ["Harita", "Map", "Karte", "Карта", "Mapa", "Mapa", "Carte"],
        ["win.prefs"] = ["Tercihler", "Preferences", "Einstellungen", "Настройки", "Preferências", "Preferencias", "Préférences"],
        ["win.exit_ask"] = ["Rig Buddy kapatılsın mı? Araç ekranının bağlantısı kesilir.", "Close Rig Buddy? The head unit loses its connection.", "Rig Buddy beenden? Das Head-Unit verliert die Verbindung.", "Закрыть Rig Buddy? Автомагнитола потеряет связь.", "Fechar o Rig Buddy? A central multimídia perde a conexão.", "¿Cerrar Rig Buddy? La unidad central pierde la conexión.", "Fermer Rig Buddy ? L'autoradio perd la connexion."],
        // {0} = ports, {1} = the commands that fix it (PortCheck.FixCommands)
        ["ports.reserved"] = [
            "Windows, Rig Buddy'nin ihtiyaç duyduğu portları ({0}) başka işler için ayırmış. Hyper-V, WSL ya da Docker olan bilgisayarlarda olur ve her başlangıçta değişebilir.\n\nÇözüm (bir kez, yönetici olarak açılmış PowerShell'de):\n\n{1}\n\nİkinci komut hata verirse bilgisayarı yeniden başlatın ve açılır açılmaz yalnızca ikinci komutu çalıştırın.\n\nBu komutlar kopyalansın mı?",
            "Windows has reserved ports Rig Buddy needs ({0}) for other uses. This happens on PCs with Hyper-V, WSL or Docker and can change at every start.\n\nFix, once, in a PowerShell opened as administrator:\n\n{1}\n\nIf the second command fails, restart the PC and run only the second command first thing.\n\nCopy these commands?",
            "Windows hat Ports, die Rig Buddy braucht ({0}), anderweitig reserviert. Das passiert auf PCs mit Hyper-V, WSL oder Docker und kann sich bei jedem Start ändern.\n\nLösung, einmalig, in einer PowerShell als Administrator:\n\n{1}\n\nSchlägt der zweite Befehl fehl, den PC neu starten und gleich nach dem Start nur den zweiten Befehl ausführen.\n\nBefehle kopieren?",
            "Windows зарезервировала порты, которые нужны Rig Buddy ({0}), для других целей. Так бывает на ПК с Hyper-V, WSL или Docker, и список может меняться при каждом запуске.\n\nРешение (один раз, в PowerShell от имени администратора):\n\n{1}\n\nЕсли вторая команда выдаёт ошибку, перезагрузите ПК и сразу после запуска выполните только вторую команду.\n\nСкопировать эти команды?",
            "O Windows reservou portas de que o Rig Buddy precisa ({0}) para outros usos. Isso acontece em PCs com Hyper-V, WSL ou Docker e pode mudar a cada inicialização.\n\nSolução, uma vez, em um PowerShell aberto como administrador:\n\n{1}\n\nSe o segundo comando falhar, reinicie o PC e execute só o segundo comando logo após ligar.\n\nCopiar estes comandos?",
            "Windows ha reservado puertos que Rig Buddy necesita ({0}) para otros usos. Ocurre en PC con Hyper-V, WSL o Docker y puede cambiar en cada inicio.\n\nSolución, una sola vez, en PowerShell abierto como administrador:\n\n{1}\n\nSi el segundo comando falla, reinicia el PC y ejecuta solo el segundo comando nada más arrancar.\n\n¿Copiar estos comandos?",
            "Windows a réservé des ports dont Rig Buddy a besoin ({0}) pour d'autres usages. Cela arrive sur les PC avec Hyper-V, WSL ou Docker et peut changer à chaque démarrage.\n\nSolution, une seule fois, dans PowerShell ouvert en administrateur :\n\n{1}\n\nSi la deuxième commande échoue, redémarrez le PC et exécutez uniquement la deuxième commande dès le démarrage.\n\nCopier ces commandes ?",
        ],
        ["ports.inuse"] = [
            "Başka bir program Rig Buddy'nin ihtiyaç duyduğu portları ({0}) kullanıyor. Onu kapatın (ya da bilgisayarı yeniden başlatın) ve Rig Buddy'yi yeniden açın.",
            "Another program is already using ports Rig Buddy needs ({0}). Close it (or restart the PC) and start Rig Buddy again.",
            "Ein anderes Programm belegt Ports, die Rig Buddy braucht ({0}). Schließen Sie es (oder starten Sie den PC neu) und starten Sie Rig Buddy erneut.",
            "Другая программа уже использует порты, нужные Rig Buddy ({0}). Закройте её (или перезагрузите ПК) и запустите Rig Buddy снова.",
            "Outro programa já está usando portas de que o Rig Buddy precisa ({0}). Feche-o (ou reinicie o PC) e abra o Rig Buddy de novo.",
            "Otro programa ya usa puertos que Rig Buddy necesita ({0}). Ciérralo (o reinicia el PC) y abre Rig Buddy de nuevo.",
            "Un autre programme utilise déjà des ports dont Rig Buddy a besoin ({0}). Fermez-le (ou redémarrez le PC) puis relancez Rig Buddy.",
        ],
        ["f.nav"] = ["Navigasyon ve rota", "Navigation and routes", "Navigation und Routen", "Навигация и маршруты", "Navegação e rotas", "Navegación y rutas", "Navigation et itinéraires"],
        ["f.vehicle"] = ["Araç bilgisayarı", "Trip computer", "Bordcomputer", "Бортовой компьютер", "Computador de bordo", "Ordenador de a bordo", "Ordinateur de bord"],
        ["f.jobs"] = ["İşler ve profil", "Jobs and profile", "Aufträge und Profil", "Заказы и профиль", "Fretes e perfil", "Trabajos y perfil", "Missions et profil"],
        ["f.radio"] = ["Oyun radyosu", "In-game radio", "Radio im Spiel", "Радио в игре", "Rádio do jogo", "Radio del juego", "Radio du jeu"],
        ["f.devices"] = ["Bağlı cihazlar", "Connected devices", "Verbundene Geräte", "Подключённые устройства", "Dispositivos conectados", "Dispositivos conectados", "Appareils connectés"],
        ["fs.ready"] = ["Hazır", "Ready", "Bereit", "Готово", "Pronto", "Listo", "Prêt"],
        ["fs.stopped"] = ["Durduruldu", "Stopped", "Gestoppt", "Остановлено", "Parado", "Detenido", "Arrêté"],
        ["fs.loading"] = ["Yükleniyor… %{0}", "Loading… {0}%", "Lädt… {0} %", "Загрузка… {0}%", "Carregando… {0}%", "Cargando… {0} %", "Chargement… {0} %"],
        ["fs.starting"] = ["Başlatılıyor…", "Starting…", "Startet…", "Запуск…", "Iniciando…", "Iniciando…", "Démarrage…"],
        ["fs.game_data"] = ["Oyundan veri geliyor", "Receiving game data", "Spieldaten kommen an", "Данные из игры поступают", "Recebendo dados do jogo", "Recibiendo datos del juego", "Données du jeu reçues"],
        ["fs.game_wait"] = ["Oyun bekleniyor", "Waiting for the game", "Warte auf das Spiel", "Ожидание игры", "Aguardando o jogo", "Esperando el juego", "En attente du jeu"],
        ["fs.save_ok"] = ["Kayıt okundu", "Save game read", "Spielstand gelesen", "Сохранение прочитано", "Save lido", "Partida guardada leída", "Sauvegarde lue"],
        ["fs.save_wait"] = ["Kayıt bekleniyor", "Waiting for a save", "Warte auf Spielstand", "Ожидание сохранения", "Aguardando save", "Esperando partida guardada", "En attente d’une sauvegarde"],
        ["fs.devices"] = ["{0} cihaz bağlı", "{0} connected", "{0} verbunden", "Подключено: {0}", "{0} conectado(s)", "{0} conectado(s)", "{0} connecté(s)"],
        ["fs.devices_wait"] = ["Cihaz bekleniyor", "Waiting for a device", "Warte auf ein Gerät", "Ожидание устройства", "Aguardando dispositivo", "Esperando un dispositivo", "En attente d’un appareil"],
        ["percent"] = ["%{0}", "{0}%", "{0} %", "{0}%", "{0}%", "{0} %", "{0} %"],

        ["theme.system"] = ["Sistem", "System", "System", "Системная", "Sistema", "Sistema", "Système"],
        ["theme.system_long"] = ["Sistem (Windows temasını izler)", "System (follows Windows)", "System (folgt Windows)", "Системная (как в Windows)", "Sistema (segue o Windows)", "Sistema (sigue a Windows)", "Système (suit Windows)"],
        ["theme.light"] = ["Açık", "Light", "Hell", "Светлая", "Claro", "Claro", "Clair"],
        ["theme.dark"] = ["Koyu", "Dark", "Dunkel", "Тёмная", "Escuro", "Oscuro", "Sombre"],
        ["lang.system"] = ["Sistem dili", "System language", "Systemsprache", "Язык системы", "Idioma do sistema", "Idioma del sistema", "Langue du système"],

        // map build
        ["map.check"] = ["Harita kontrol ediliyor", "Checking the map", "Karte wird geprüft", "Проверка карты", "Verificando o mapa", "Comprobando el mapa", "Vérification de la carte"],
        ["map.parse"] = ["Oyun dosyaları okunuyor ({0})", "Reading game files ({0})", "Spieldateien werden gelesen ({0})", "Чтение файлов игры ({0})", "Lendo os arquivos do jogo ({0})", "Leyendo archivos del juego ({0})", "Lecture des fichiers du jeu ({0})"],
        ["map.labels"] = ["Yer adları hazırlanıyor ({0})", "Preparing place names ({0})", "Ortsnamen werden vorbereitet ({0})", "Подготовка названий ({0})", "Preparando nomes de lugares ({0})", "Preparando nombres de lugares ({0})", "Préparation des noms de lieux ({0})"],
        ["map.search"] = ["Arama verisi hazırlanıyor ({0})", "Building search data ({0})", "Suchdaten werden erstellt ({0})", "Подготовка данных поиска ({0})", "Criando dados de busca ({0})", "Creando datos de búsqueda ({0})", "Création des données de recherche ({0})"],
        ["map.graph"] = ["Yol ağı oluşturuluyor ({0})", "Building the road network ({0})", "Straßennetz wird erstellt ({0})", "Построение дорожной сети ({0})", "Montando a malha viária ({0})", "Creando la red de carreteras ({0})", "Création du réseau routier ({0})"],
        ["map.roundabouts"] = ["Kavşaklar işleniyor ({0})", "Processing roundabouts ({0})", "Kreisverkehre werden verarbeitet ({0})", "Обработка кольцевых развязок ({0})", "Processando rotatórias ({0})", "Procesando rotondas ({0})", "Traitement des ronds-points ({0})"],
        ["map.zip"] = ["Rota verisi paketleniyor ({0})", "Packing route data ({0})", "Routendaten werden gepackt ({0})", "Упаковка данных маршрутов ({0})", "Empacotando dados de rotas ({0})", "Empaquetando datos de rutas ({0})", "Empaquetage des données d’itinéraire ({0})"],
        ["map.geojson"] = ["Harita çiziliyor ({0})", "Drawing the map ({0})", "Karte wird gezeichnet ({0})", "Отрисовка карты ({0})", "Desenhando o mapa ({0})", "Dibujando el mapa ({0})", "Dessin de la carte ({0})"],
        ["map.postprocess"] = ["Yollar düzenleniyor ({0})", "Tidying up roads ({0})", "Straßen werden bereinigt ({0})", "Обработка дорог ({0})", "Ajustando estradas ({0})", "Ajustando carreteras ({0})", "Nettoyage des routes ({0})"],
        ["map.tiles"] = ["Harita parçaları üretiliyor ({0})", "Creating map tiles ({0})", "Kartenkacheln werden erstellt ({0})", "Создание фрагментов карты ({0})", "Criando blocos do mapa ({0})", "Creando teselas del mapa ({0})", "Création des tuiles de carte ({0})"],
        ["map.copy"] = ["Kaydediliyor ({0})", "Saving ({0})", "Wird gespeichert ({0})", "Сохранение ({0})", "Salvando ({0})", "Guardando ({0})", "Enregistrement ({0})"],
        ["map.sprites"] = ["Simgeler hazırlanıyor", "Preparing icons", "Symbole werden vorbereitet", "Подготовка значков", "Preparando ícones", "Preparando iconos", "Préparation des icônes"],
        ["map.done"] = ["Harita hazır", "Map ready", "Karte fertig", "Карта готова", "Mapa pronto", "Mapa listo", "Carte prête"],
        ["map.uptodate"] = ["Harita güncel", "Map is up to date", "Karte ist aktuell", "Карта актуальна", "Mapa atualizado", "Mapa al día", "Carte à jour"],
        ["win.map_building"] = ["Harita hazırlanıyor…", "Preparing the map…", "Karte wird vorbereitet…", "Подготовка карты…", "Preparando o mapa…", "Preparando el mapa…", "Préparation de la carte…"],
        ["win.map_once"] = ["Bu işlem yalnızca bir kez yapılır", "This is done only once", "Das passiert nur einmal", "Это делается только один раз", "Isso é feito só uma vez", "Esto solo se hace una vez", "Cela n’est fait qu’une fois"],
        ["win.map_failed"] = ["Harita hazırlanamadı", "Map could not be prepared", "Karte konnte nicht erstellt werden", "Не удалось подготовить карту", "Não foi possível preparar o mapa", "No se pudo preparar el mapa", "Impossible de préparer la carte"],
        ["win.map_retry"] = ["Tekrar dene", "Try again", "Erneut versuchen", "Повторить", "Tentar novamente", "Reintentar", "Réessayer"],
        ["win.map_update"] = ["Harita güncellemesi var · Güncelle", "Map update available · Update", "Kartenupdate verfügbar · Aktualisieren", "Доступно обновление карты · Обновить", "Atualização do mapa · Atualizar", "Actualización del mapa · Actualizar", "Mise à jour de la carte · Mettre à jour"],
        ["win.no_game"] = ["Oyun bulunamadı", "Game not found", "Spiel nicht gefunden", "Игра не найдена", "Jogo não encontrado", "Juego no encontrado", "Jeu introuvable"],
        ["win.no_game_hint"] = ["ETS2 ya da ATS'nin Steam'de kurulu olduğundan emin olun", "Make sure ETS2 or ATS is installed in Steam", "ETS2 oder ATS muss in Steam installiert sein", "Убедитесь, что ETS2 или ATS установлены в Steam", "Verifique se ETS2 ou ATS está instalado na Steam", "Asegúrate de tener ETS2 o ATS instalado en Steam", "Vérifiez que ETS2 ou ATS est installé dans Steam"],
        ["fs.map_building"] = ["Harita hazırlanıyor… %{0}", "Preparing map… {0}%", "Karte wird vorbereitet… {0} %", "Подготовка карты… {0}%", "Preparando o mapa… {0}%", "Preparando el mapa… {0} %", "Préparation de la carte… {0} %"],
        ["tray.map_update"] = ["Haritayı güncelle", "Update map", "Karte aktualisieren", "Обновить карту", "Atualizar mapa", "Actualizar mapa", "Mettre à jour la carte"],
        ["tray.map_rebuild"] = ["Haritayı yeniden oluştur", "Rebuild map", "Karte neu erstellen", "Пересоздать карту", "Recriar mapa", "Reconstruir mapa", "Reconstruire la carte"],

        // log window
        ["logs.title"] = ["Rig Buddy – Loglar", "Rig Buddy – Logs", "Rig Buddy – Protokolle", "Rig Buddy – Журналы", "Rig Buddy – Logs", "Rig Buddy – Registros", "Rig Buddy – Journaux"],
        ["logs.agent"] = ["Agent", "Agent", "Agent", "Агент", "Agente", "Agente", "Agent"],
        ["logs.telemetry"] = ["Telemetri", "Telemetry", "Telemetrie", "Телеметрия", "Telemetria", "Telemetría", "Télémétrie"],
        ["logs.map"] = ["Harita", "Map", "Karte", "Карта", "Mapa", "Mapa", "Carte"],
        ["logs.folder"] = ["Klasörü aç", "Open folder", "Ordner öffnen", "Открыть папку", "Abrir pasta", "Abrir carpeta", "Ouvrir le dossier"],
        ["logs.copy"] = ["Kopyala", "Copy", "Kopieren", "Копировать", "Copiar", "Copiar", "Copier"],
        ["logs.pause"] = ["Duraklat", "Pause", "Pausieren", "Пауза", "Pausar", "Pausar", "Pause"],
        ["logs.resume"] = ["Devam et", "Resume", "Fortsetzen", "Продолжить", "Continuar", "Reanudar", "Reprendre"],
        ["logs.empty"] = ["(henüz log yok)", "(no log yet)", "(noch kein Protokoll)", "(журнала пока нет)", "(ainda sem log)", "(aún no hay registro)", "(pas encore de journal)"],

        // supervisor
        ["setup.missing"] = [
            "Kurulum eksik ya da bozuk: Rig Buddy'yi yeniden kurun.", "Installation incomplete or damaged: reinstall Rig Buddy.",
            "Installation unvollständig oder beschädigt: Rig Buddy neu installieren.", "Установка неполная или повреждена: переустановите Rig Buddy.",
            "Instalação incompleta ou danificada: reinstale o Rig Buddy.", "Instalación incompleta o dañada: reinstala Rig Buddy.",
            "Installation incomplète ou endommagée : réinstallez Rig Buddy.",
        ],
        ["svc.server"] = ["Navigasyon sunucusu", "Navigation server", "Navigationsserver", "Сервер навигации", "Servidor de navegação", "Servidor de navegación", "Serveur de navigation"],
        ["svc.agent"] = ["Agent (araç, işler, radyo)", "Agent (vehicle, jobs, radio)", "Agent (Fahrzeug, Aufträge, Radio)", "Агент (машина, заказы, радио)", "Agente (veículo, fretes, rádio)", "Agente (vehículo, trabajos, radio)", "Agent (véhicule, missions, radio)"],
        ["svc.telemetry"] = ["Telemetri istemcisi", "Telemetry client", "Telemetrie-Client", "Клиент телеметрии", "Cliente de telemetria", "Cliente de telemetría", "Client de télémétrie"],
        ["stage.init"] = ["Başlatılıyor", "Starting", "Startet", "Запуск", "Iniciando", "Iniciando", "Démarrage"],
        ["stage.start"] = ["Navigasyon sunucusu başlatılıyor", "Starting the navigation server", "Navigationsserver startet", "Запуск сервера навигации", "Iniciando o servidor de navegação", "Iniciando el servidor de navegación", "Démarrage du serveur de navigation"],
        ["stage.read"] = ["Harita verisi okunuyor ({0})", "Reading map data ({0})", "Kartendaten werden gelesen ({0})", "Чтение данных карты ({0})", "Lendo dados do mapa ({0})", "Leyendo datos del mapa ({0})", "Lecture des données de carte ({0})"],
        ["stage.graph"] = ["Yol ağı yükleniyor ({0})", "Loading the road network ({0})", "Straßennetz wird geladen ({0})", "Загрузка дорожной сети ({0})", "Carregando a malha viária ({0})", "Cargando la red de carreteras ({0})", "Chargement du réseau routier ({0})"],
        ["stage.geometry"] = ["Yol geometrisi hesaplanıyor", "Computing road geometry", "Straßengeometrie wird berechnet", "Расчёт геометрии дорог", "Calculando a geometria das vias", "Calculando la geometría de las vías", "Calcul de la géométrie des routes"],
        ["stage.index"] = ["Arama dizinleri hazırlanıyor", "Building search indexes", "Suchindizes werden erstellt", "Построение поисковых индексов", "Criando índices de busca", "Creando índices de búsqueda", "Création des index de recherche"],
        ["stage.open"] = ["Sunucu açılıyor", "Opening the server", "Server wird geöffnet", "Открытие сервера", "Abrindo o servidor", "Abriendo el servidor", "Ouverture du serveur"],
        ["stage.telemetry"] = ["Telemetri istemcisi bağlanıyor", "Connecting the telemetry client", "Telemetrie-Client verbindet", "Подключение клиента телеметрии", "Conectando o cliente de telemetria", "Conectando el cliente de telemetría", "Connexion du client de télémétrie"],
        ["stage.ready"] = ["Hazır", "Ready", "Bereit", "Готово", "Pronto", "Listo", "Prêt"],
    };
}
