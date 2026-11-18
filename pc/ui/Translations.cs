namespace RigBuddy.Ui;

/**
 * The window's texts in German, Russian, Portuguese, Spanish and French, keyed by the English text. Missing entries show
 * English. RigHardware.exe (rig-buddy-hardware) builds this file into itself and adds its own (Translations.Hardware.cs).
 */
static partial class Translations
{
    static Translations() => AddTexts(Table);
    static partial void AddTexts(Dictionary<string, string[]> table);

    static readonly string[] Codes = { "de", "ru", "pt", "es", "fr" };

    public static string? Of(string code, string en)
    {
        int i = Array.IndexOf(Codes, code);
        return i >= 0 && Table.TryGetValue(en, out var t) && !string.IsNullOrEmpty(t[i]) ? t[i] : null;
    }

    static readonly Dictionary<string, string[]> Table = new()
    {
        ["Sound mods"] = ["Sound-Mods", "Звуковые моды", "Mods de som", "Mods de sonido", "Mods de son"],
        ["EVENTS"] = ["EREIGNISSE", "СОБЫТИЯ", "EVENTOS", "EVENTOS", "ÉVÉNEMENTS"],
        ["PHONE APP (ANDROID)"] = ["HANDY-APP (ANDROID)", "ПРИЛОЖЕНИЕ ДЛЯ ТЕЛЕФОНА (ANDROID)", "APP DO TELEMÓVEL (ANDROID)", "APP DEL MÓVIL (ANDROID)", "APPLI TÉLÉPHONE (ANDROID)"],
        ["Open this address in the phone's browser, download and install."] = ["Öffne diese Adresse im Browser des Handys, lade herunter und installiere.", "Откройте этот адрес в браузере телефона, скачайте и установите.", "Abre este endereço no navegador do telemóvel, transfere e instala.", "Abre esta dirección en el navegador del móvil, descarga e instala.", "Ouvrez cette adresse dans le navigateur du téléphone, téléchargez et installez."],
        ["Copy app address"] = ["App-Adresse kopieren", "Копировать адрес приложения", "Copiar o endereço da app", "Copiar la dirección de la app", "Copier l'adresse de l'appli"],
        ["Check updates now"] = ["Jetzt nach Updates suchen", "Проверить обновления сейчас", "Procurar atualizações agora", "Buscar actualizaciones ahora", "Rechercher des mises à jour"],
        ["Checking…"] = ["Wird geprüft …", "Проверка…", "A verificar…", "Comprobando…", "Vérification…"],
        ["Could not check: {0}"] = ["Prüfung nicht möglich: {0}", "Не удалось проверить: {0}", "Não foi possível verificar: {0}", "No se pudo comprobar: {0}", "Vérification impossible : {0}"],
        ["New version available: {0}"] = ["Neue Version verfügbar: {0}", "Доступна новая версия: {0}", "Nova versão disponível: {0}", "Nueva versión disponible: {0}", "Nouvelle version disponible : {0}"],
        ["You are up to date ({0})."] = ["Du bist auf dem neuesten Stand ({0}).", "У вас последняя версия ({0}).", "Estás atualizado ({0}).", "Estás al día ({0}).", "Vous êtes à jour ({0})."],
        ["not set"] = ["nicht belegt", "не назначено", "não definido", "sin asignar", "non défini"],
        ["Assign…"] = ["Zuweisen …", "Назначить…", "Atribuir…", "Asignar…", "Associer…"],
        ["Press a button on the wheel… (10 s)"] = ["Drücke eine Taste am Lenkrad … (10 s)", "Нажмите кнопку на руле… (10 с)", "Carrega num botão do volante… (10 s)", "Pulsa un botón del volante… (10 s)", "Appuyez sur un bouton du volant… (10 s)"],
        ["Wheel buttons"] = ["Lenkradtasten", "Кнопки руля", "Botões do volante", "Botones del volante", "Boutons du volant"],
        ["Game"] = ["Spiel", "Игра", "Jogo", "Juego", "Jeu"],
        ["Sound levels"] = ["Lautstärken", "Громкость", "Volumes", "Volúmenes", "Volumes"],
        ["The mod's layers"] = ["Ebenen des Mods", "Слои мода", "Camadas do mod", "Capas del mod", "Couches du mod"],
        ["Sounds the mod put inside its engine or exhaust sound (e.g. the turbo whistle). These levels come on top of that sound's own; turbo layers follow the Turbo level too."] = ["Klänge, die der Mod in seinen Motor- oder Auspuffklang gelegt hat (z. B. das Turbopfeifen). Diese Lautstärken kommen zu der des Klangs selbst hinzu; Turbo-Ebenen folgen auch der Turbo-Lautstärke.", "Звуки, которые мод поместил внутрь звука двигателя или выхлопа (например, свист турбины). Эта громкость добавляется к громкости самого звука; слои турбины также следуют громкости турбины.", "Sons que o mod colocou dentro do som do motor ou do escape (p. ex. o assobio do turbo). Estes volumes somam-se ao do próprio som; as camadas de turbo também seguem o volume do turbo.", "Sonidos que el mod puso dentro del sonido del motor o del escape (p. ej. el silbido del turbo). Estos volúmenes se suman al del propio sonido; las capas de turbo también siguen el volumen del turbo.", "Sons que le mod a placés dans le son du moteur ou de l'échappement (p. ex. le sifflement du turbo). Ces volumes s'ajoutent à celui du son lui-même ; les couches turbo suivent aussi le volume du turbo."],
        ["No layers found in the selected mod yet. Start the engine in the game and drive a bit: Rig Buddy finds the layers while the game runs and shows them here. A mod without layers keeps this list empty."] = ["Im gewählten Mod wurden noch keine Ebenen gefunden. Starte im Spiel den Motor und fahr ein Stück: Rig Buddy findet die Ebenen während des Spiels und zeigt sie hier. Bei einem Mod ohne Ebenen bleibt die Liste leer.", "В выбранном моде слои пока не найдены. Заведите двигатель в игре и немного проедьте: Rig Buddy находит слои во время игры и показывает их здесь. У мода без слоёв список останется пустым.", "Ainda não foram encontradas camadas no mod escolhido. Liga o motor no jogo e conduz um pouco: o Rig Buddy encontra as camadas durante o jogo e mostra-as aqui. Num mod sem camadas a lista fica vazia.", "Aún no se han encontrado capas en el mod elegido. Arranca el motor en el juego y conduce un poco: Rig Buddy encuentra las capas mientras el juego se ejecuta y las muestra aquí. En un mod sin capas la lista queda vacía.", "Aucune couche trouvée dans le mod choisi pour l'instant. Démarrez le moteur dans le jeu et roulez un peu : Rig Buddy trouve les couches pendant le jeu et les affiche ici. Pour un mod sans couches, la liste reste vide."],
        ["Inside the exhaust"] = ["Im Auspuff", "Внутри выхлопа", "Dentro do escape", "Dentro del escape", "Dans l'échappement"],
        ["Inside the engine"] = ["Im Motor", "Внутри двигателя", "Dentro do motor", "Dentro del motor", "Dans le moteur"],
        [" · turbo"] = [" · Turbo", " · турбина", " · turbo", " · turbo", " · turbo"],
        ["Tablet: next"] = ["Tablet: weiter", "Планшет: далее", "Tablet: seguinte", "Tableta: siguiente", "Tablette : suivant"],
        ["Tablet: previous"] = ["Tablet: zurück", "Планшет: назад", "Tablet: anterior", "Tableta: anterior", "Tablette : précédent"],
        ["Bind buttons of your wheel, button box or any game controller Windows knows to the tablet's screen switching. Works with the game in front; every button can be used, the wheel rim's too."] = ["Weise die Tasten deines Lenkrads, deiner Button-Box oder eines beliebigen von Windows erkannten Controllers dem Bildschirmwechsel des Tablets zu. Funktioniert auch, wenn das Spiel im Vordergrund ist; jede Taste ist nutzbar, auch die am Lenkradkranz.", "Назначьте кнопки руля, кнопочной панели или любого игрового контроллера, который видит Windows, на переключение экранов планшета. Работает и когда игра на переднем плане; подходит любая кнопка, в том числе на ободе руля.", "Atribui botões do teu volante, da caixa de botões ou de qualquer comando que o Windows reconheça à troca de ecrãs do tablet. Funciona com o jogo em primeiro plano; qualquer botão serve, também os do aro do volante.", "Asigna botones de tu volante, de tu caja de botones o de cualquier mando que Windows reconozca al cambio de pantallas de la tableta. Funciona con el juego en primer plano; sirve cualquier botón, también los del aro del volante.", "Associez les boutons de votre volant, de votre boîtier de boutons ou de toute manette reconnue par Windows au changement d'écran de la tablette. Fonctionne avec le jeu au premier plan ; tout bouton peut servir, y compris ceux de la jante du volant."],
        ["Everything the game reports, by name. Click a row to copy its name."] = ["Alles, was das Spiel meldet, mit Namen. Ein Klick auf eine Zeile kopiert den Namen.", "Всё, что сообщает игра, по именам. Щелчок по строке копирует имя.", "Tudo o que o jogo comunica, por nome. Clicar numa linha copia o nome.", "Todo lo que informa el juego, por nombre. Un clic en una fila copia el nombre.", "Tout ce que le jeu transmet, par nom. Un clic sur une ligne copie le nom."],
        ["Overview"] = ["Übersicht", "Обзор", "Visão geral", "Resumen", "Aperçu"],
        ["Devices"] = ["Geräte", "Устройства", "Dispositivos", "Dispositivos", "Appareils"],
        ["System"] = ["System", "Система", "Sistema", "Sistema", "Système"],
        ["Logs"] = ["Protokolle", "Журналы", "Registos", "Registros", "Journaux"],
        ["Waiting for the game: start ETS2 or ATS with the telemetry plugin"] = ["Warte auf das Spiel: ETS2 oder ATS mit dem Telemetrie-Plugin starten", "Ожидание игры: запустите ETS2 или ATS с плагином телеметрии", "À espera do jogo: inicie o ETS2 ou ATS com o plugin de telemetria", "Esperando el juego: inicia ETS2 o ATS con el plugin de telemetría", "En attente du jeu : lancez ETS2 ou ATS avec le plugin de télémétrie"],
        ["Live telemetry"] = ["Live-Telemetrie", "Телеметрия в реальном времени", "Telemetria em direto", "Telemetría en vivo", "Télémétrie en direct"],
        ["Speed"] = ["Geschwindigkeit", "Скорость", "Velocidade", "Velocidad", "Vitesse"],
        ["RPM"] = ["Drehzahl", "Обороты", "Rotação", "Revoluciones", "Régime"],
        ["Gear"] = ["Gang", "Передача", "Mudança", "Marcha", "Rapport"],
        ["Fuel"] = ["Kraftstoff", "Топливо", "Combustível", "Combustible", "Carburant"],
        ["Game clock"] = ["Spieluhr", "Игровые часы", "Relógio do jogo", "Reloj del juego", "Horloge du jeu"],
        ["No devices yet. Add one on the Devices page."] = ["Noch keine Geräte. Füge eines auf der Seite „Geräte“ hinzu.", "Устройств пока нет. Добавьте на странице «Устройства».", "Ainda sem dispositivos. Adicione um na página Dispositivos.", "Aún no hay dispositivos. Añade uno en la página Dispositivos.", "Aucun appareil pour l'instant. Ajoutez-en un dans la page Appareils."],
        ["Remove"] = ["Entfernen", "Удалить", "Remover", "Quitar", "Supprimer"],
        ["none"] = ["keiner", "нет", "nenhum", "ninguno", "aucun"],
        ["Gauges"] = ["Anzeigen", "Приборы", "Mostradores", "Indicadores", "Jauges"],
        ["Each row sends one value: name, formula (e.g. truck.speed.kph), unit and scale."] = ["Jede Zeile sendet einen Wert: Name, Formel (z. B. truck.speed.kph), Einheit und Skala.", "Каждая строка отправляет одно значение: имя, формула (напр. truck.speed.kph), единица и шкала.", "Cada linha envia um valor: nome, fórmula (ex.: truck.speed.kph), unidade e escala.", "Cada fila envía un valor: nombre, fórmula (p. ej. truck.speed.kph), unidad y escala.", "Chaque ligne envoie une valeur : nom, formule (ex. truck.speed.kph), unité et échelle."],
        ["In the order of the board's motor sockets. The scale comes from the board's own settings."] = ["In der Reihenfolge der Motoranschlüsse der Platine. Die Skala stammt aus den Einstellungen der Platine.", "В порядке моторных разъёмов платы. Шкала берётся из настроек самой платы.", "Na ordem das tomadas de motor da placa. A escala vem das definições da própria placa.", "En el orden de los zócalos de motor de la placa. La escala viene de los ajustes de la propia placa.", "Dans l'ordre des prises moteur de la carte. L'échelle vient des réglages de la carte."],
        ["Test"] = ["Test", "Проверка", "Testar", "Probar", "Tester"],
        ["Add gauge"] = ["Anzeige hinzufügen", "Добавить прибор", "Adicionar mostrador", "Añadir indicador", "Ajouter une jauge"],
        ["Gauge {0}"] = ["Anzeige {0}", "Прибор {0}", "Mostrador {0}", "Indicador {0}", "Jauge {0}"],
        ["The phone connects to this address"] = ["Das Handy verbindet sich mit dieser Adresse", "Телефон подключается по этому адресу", "O telemóvel liga-se a este endereço", "El móvil se conecta a esta dirección", "Le téléphone se connecte à cette adresse"],
        ["Copy"] = ["Kopieren", "Копировать", "Copiar", "Copiar", "Copier"],
        ["Services"] = ["Dienste", "Службы", "Serviços", "Servicios", "Services"],
        ["Restart"] = ["Neu starten", "Перезапустить", "Reiniciar", "Reiniciar", "Redémarrer"],
        ["Map data"] = ["Kartendaten", "Данные карты", "Dados do mapa", "Datos del mapa", "Données de carte"],
        ["Build"] = ["Erstellen", "Построить", "Criar", "Crear", "Créer"],
        ["Update"] = ["Aktualisieren", "Обновить", "Atualizar", "Actualizar", "Mettre à jour"],
        ["Phones connected: {0}"] = ["Verbundene Handys: {0}", "Подключено телефонов: {0}", "Telemóveis ligados: {0}", "Móviles conectados: {0}", "Téléphones connectés : {0}"],
        ["Exit"] = ["Beenden", "Выход", "Sair", "Salir", "Quitter"],
        ["Open logs"] = ["Protokolle öffnen", "Открыть журналы", "Abrir registos", "Abrir registros", "Ouvrir les journaux"],
        ["Settings"] = ["Einstellungen", "Настройки", "Definições", "Ajustes", "Paramètres"],
        ["Both"] = ["Beide", "Оба", "Ambos", "Ambos", "Les deux"],
        ["Game and map"] = ["Spiel und Karte", "Игра и карта", "Jogo e mapa", "Juego y mapa", "Jeu et carte"],
        ["Map data is built only for the chosen game."] = ["Kartendaten werden nur für das gewählte Spiel erstellt.", "Данные карты строятся только для выбранной игры.", "Os dados do mapa são criados apenas para o jogo escolhido.", "Los datos del mapa se crean solo para el juego elegido.", "Les données de carte ne sont créées que pour le jeu choisi."],
        ["General"] = ["Allgemein", "Общие", "Geral", "General", "Général"],
        ["Tools"] = ["Werkzeuge", "Инструменты", "Ferramentas", "Herramientas", "Outils"],
        ["Fade"] = ["Überblenden", "Плавный переход", "Desvanecer", "Fundido", "Fondu"],
        ["Waiting"] = ["Wartet", "Ожидание", "A aguardar", "Esperando", "En attente"],
        ["Width"] = ["Breite", "Ширина", "Largura", "Ancho", "Largeur"],
        ["Height"] = ["Höhe", "Высота", "Altura", "Alto", "Hauteur"],
        ["HOME"] = ["STARTSEITE", "ГЛАВНАЯ", "INÍCIO", "INICIO", "ACCUEIL"],
        ["SYSTEM"] = ["SYSTEM", "СИСТЕМА", "SISTEMA", "SISTEMA", "SYSTÈME"],
        ["TELEMETRY: "] = ["TELEMETRIE: ", "ТЕЛЕМЕТРИЯ: ", "TELEMETRIA: ", "TELEMETRÍA: ", "TÉLÉMÉTRIE : "],
        ["RUNNING"] = ["LÄUFT", "РАБОТАЕТ", "ATIVA", "ACTIVA", "ACTIVE"],
        ["WAITING"] = ["WARTET", "ОЖИДАНИЕ", "À ESPERA", "EN ESPERA", "EN ATTENTE"],
        ["TELEMETRY STREAM"] = ["TELEMETRIE-STROM", "ПОТОК ТЕЛЕМЕТРИИ", "FLUXO DE TELEMETRIA", "FLUJO DE TELEMETRÍA", "FLUX DE TÉLÉMÉTRIE"],
        ["HARDWARE RACK"] = ["HARDWARE-RACK", "СТОЙКА ОБОРУДОВАНИЯ", "RACK DE HARDWARE", "RACK DE HARDWARE", "BAIE MATÉRIELLE"],
        ["MOBILE DEVICES"] = ["MOBILE GERÄTE", "МОБИЛЬНЫЕ УСТРОЙСТВА", "DISPOSITIVOS MÓVEIS", "DISPOSITIVOS MÓVILES", "APPAREILS MOBILES"],
        ["LIVE TELEMETRY INSPECTOR"] = ["LIVE-TELEMETRIE-INSPEKTOR", "ИНСПЕКТОР ТЕЛЕМЕТРИИ", "INSPETOR DE TELEMETRIA EM DIRETO", "INSPECTOR DE TELEMETRÍA EN VIVO", "INSPECTEUR DE TÉLÉMÉTRIE EN DIRECT"],
        ["Running"] = ["Läuft", "Работает", "Ativa", "Activa", "Active"],
        ["devices online"] = ["Geräte online", "устройств в сети", "dispositivos ligados", "dispositivos en línea", "appareils en ligne"],
        ["TELEMETRY SERVICE"] = ["TELEMETRIE-DIENST", "СЛУЖБА ТЕЛЕМЕТРИИ", "SERVIÇO DE TELEMETRIA", "SERVICIO DE TELEMETRÍA", "SERVICE DE TÉLÉMÉTRIE"],
        ["{0} values"] = ["{0} Werte", "значений: {0}", "{0} valores", "{0} valores", "{0} valeurs"],
        ["Pause"] = ["Pause", "Пауза", "Pausa", "Pausa", "Pause"],
        ["Clear"] = ["Leeren", "Очистить", "Limpar", "Borrar", "Effacer"],
        ["Game data"] = ["Spieldaten", "Данные игры", "Dados do jogo", "Datos del juego", "Données du jeu"],
        ["Search: speed, rpm, fuel, brake…"] = ["Suchen: Tempo, Drehzahl, Kraftstoff, Bremse…", "Поиск: скорость, обороты, топливо, тормоз…", "Pesquisar: velocidade, rotação, combustível, travão…", "Buscar: velocidad, revoluciones, combustible, freno…", "Rechercher : vitesse, régime, carburant, frein…"],
        ["Showing {0} of {1}, keep typing to narrow it down"] = ["{0} von {1} angezeigt, weiter tippen zum Eingrenzen", "Показано {0} из {1}, продолжайте вводить для сужения", "A mostrar {0} de {1}, continue a escrever para filtrar", "Mostrando {0} de {1}, sigue escribiendo para acotar", "{0} sur {1} affichés, continuez à taper pour affiner"],
        ["Copied {0}"] = ["{0} kopiert", "Скопировано: {0}", "Copiado: {0}", "Copiado: {0}", "Copié : {0}"],
        ["INSPECTOR"] = ["INSPEKTOR", "ИНСПЕКТОР", "INSPETOR", "INSPECTOR", "INSPECTEUR"],
        ["Live values"] = ["Live-Werte", "Живые значения", "Valores em direto", "Valores en vivo", "Valeurs en direct"],
        ["Events"] = ["Ereignisse", "События", "Eventos", "Eventos", "Événements"],
        ["{0} lines"] = ["{0} Zeilen", "строк: {0}", "{0} linhas", "{0} líneas", "{0} lignes"],
        ["Receiving game data"] = ["Spieldaten werden empfangen", "Данные игры поступают", "A receber dados do jogo", "Recibiendo datos del juego", "Réception des données du jeu"],
        ["Game data stopped"] = ["Spieldaten unterbrochen", "Данные игры прекратились", "Dados do jogo interrompidos", "Datos del juego detenidos", "Données du jeu interrompues"],
        ["Left"] = ["Links", "Слева", "Esquerda", "Izquierda", "Gauche"],
        ["Right"] = ["Rechts", "Справа", "Direita", "Derecha", "Droite"],
        ["Background"] = ["Hintergrund", "Фон", "Fundo", "Fondo", "Arrière-plan"],
        ["Starting…"] = ["Starte…", "Запуск…", "A iniciar…", "Iniciando…", "Démarrage…"],
    };
}
