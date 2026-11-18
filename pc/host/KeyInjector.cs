// Presses keys for the head unit's button box: the app shows buttons, a touch becomes "key down" / "key up" here and Windows
// delivers it to the game like a keyboard would. The key names are the game's own (the ones in controls.sii: h, space, lbracket,
// lshift, num4, f11 ...), a combination is joined with "+" (lshift+d).
//
// Safety, because a tablet is allowed to press keys on the gaming PC:
//  - only while the game's window is in front (otherwise the keys would land in a browser or a chat window),
//  - a key that is held longer than MaxHoldMs is let go (a lost connection must not leave a key stuck),
//  - everything is released when the program quits or the game is not in front any more.
// The scan codes are used, not virtual keys: DirectInput games read those.

using System.Diagnostics;
using System.Runtime.InteropServices;

static class KeyInjector
{
    const int MaxHoldMs = 30_000;
    static readonly string[] GameProcesses = { "eurotrucks2", "amtrucks" };

    // key name -> scan code; the 0xE0 prefix is stored as 0x100
    static readonly Dictionary<string, int> Codes = BuildCodes();
    static readonly Dictionary<string, DateTime> Held = new();   // combination -> when it went down
    static readonly object Gate = new();
    static System.Threading.Timer? _watchdog;

    static Dictionary<string, int> BuildCodes()
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["esc"] = 0x01, ["minus"] = 0x0C, ["equal"] = 0x0D, ["backspace"] = 0x0E, ["tab"] = 0x0F,
            ["lbracket"] = 0x1A, ["rbracket"] = 0x1B, ["enter"] = 0x1C, ["lctrl"] = 0x1D,
            ["semicolon"] = 0x27, ["apostrophe"] = 0x28, ["grave"] = 0x29, ["lshift"] = 0x2A, ["backslash"] = 0x2B,
            ["comma"] = 0x33, ["period"] = 0x34, ["slash"] = 0x35, ["rshift"] = 0x36, ["nummultiply"] = 0x37,
            ["lalt"] = 0x38, ["space"] = 0x39, ["caps"] = 0x3A, ["numlock"] = 0x45, ["scrollock"] = 0x46,
            ["num7"] = 0x47, ["num8"] = 0x48, ["num9"] = 0x49, ["numminus"] = 0x4A, ["num4"] = 0x4B, ["num5"] = 0x4C,
            ["num6"] = 0x4D, ["numplus"] = 0x4E, ["num1"] = 0x4F, ["num2"] = 0x50, ["num3"] = 0x51, ["num0"] = 0x52,
            ["numperiod"] = 0x53, ["f11"] = 0x57, ["f12"] = 0x58,
            // extended keys (E0 prefix)
            ["numenter"] = 0x11C, ["rctrl"] = 0x11D, ["numslash"] = 0x135, ["ralt"] = 0x138,
            ["home"] = 0x147, ["uarrow"] = 0x148, ["pgup"] = 0x149, ["larrow"] = 0x14B, ["rarrow"] = 0x14D,
            ["end"] = 0x14F, ["darrow"] = 0x150, ["pgdn"] = 0x151, ["ins"] = 0x152, ["del"] = 0x153,
        };
        const string row1 = "qwertyuiop", row2 = "asdfghjkl", row3 = "zxcvbnm";
        for (int i = 0; i < row1.Length; i++) d[row1[i].ToString()] = 0x10 + i;
        for (int i = 0; i < row2.Length; i++) d[row2[i].ToString()] = 0x1E + i;
        for (int i = 0; i < row3.Length; i++) d[row3[i].ToString()] = 0x2C + i;
        for (int i = 1; i <= 9; i++) d["key" + i] = 0x01 + i;   // 1..9 on the number row
        d["key0"] = 0x0B;
        for (int i = 1; i <= 10; i++) d["f" + i] = 0x3A + i;    // f1..f10
        return d;
    }

    /** "key down h", "key up h", "key tap lshift+d", "key release". Returns the reply line. */
    public static string Command(string verb, string? combo)
    {
        if (verb == "release") { ReleaseAll(); return "ok"; }
        if (verb == "front") return GameInFront() ? "ok" : "error: the game is not in front";   // the tablet dims its buttons
        if (combo == null) return "error: no key";
        var codes = new List<int>();
        foreach (var name in combo.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Codes.TryGetValue(name, out int code)) return $"error: unknown key '{name}'";
            codes.Add(code);
        }
        if (codes.Count == 0) return "error: no key";
        if (verb != "up" && Denied(combo)) return "error: that key combination is not allowed";

        if (verb == "up") { Up(combo, codes); return "ok"; }
        if (verb != "down" && verb != "tap") return $"error: unknown key command '{verb}'";
        if (!GameInFront()) { ReleaseAll(); return "error: the game is not in front"; }
        Down(combo, codes);
        if (verb == "tap") _ = Task.Delay(40).ContinueWith(_ => Up(combo, codes));
        return "ok";
    }

    /** Combinations that would take the player out of the game (Alt+F4, Alt+Tab ...): a tablet may not press those. */
    static bool Denied(string combo)
    {
        var names = combo.ToLowerInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries);
        bool alt = names.Contains("lalt") || names.Contains("ralt");
        bool ctrl = names.Contains("lctrl") || names.Contains("rctrl");
        return (alt && (names.Contains("f4") || names.Contains("tab") || names.Contains("esc") || names.Contains("space")))
            || (ctrl && names.Contains("esc"));
    }

    static void Down(string combo, List<int> codes)
    {
        lock (Gate)
        {
            if (Held.ContainsKey(combo)) return;   // already down (a repeat of the touch)
            Held[combo] = DateTime.UtcNow;
            foreach (var c in codes) Send(c, false);
            _watchdog ??= new System.Threading.Timer(_ => Watch(), null, 1000, 1000);
        }
    }

    static void Up(string combo, List<int> codes)
    {
        lock (Gate)
        {
            if (!Held.Remove(combo)) return;
            for (int i = codes.Count - 1; i >= 0; i--) Send(codes[i], true);
        }
    }

    public static void ReleaseAll()
    {
        lock (Gate)
        {
            foreach (var combo in Held.Keys.ToList())
            {
                var names = combo.Split('+', StringSplitOptions.RemoveEmptyEntries);
                for (int i = names.Length - 1; i >= 0; i--)
                    if (Codes.TryGetValue(names[i], out int c)) Send(c, true);
            }
            Held.Clear();
        }
    }

    static void Watch()
    {
        List<string> stale;
        lock (Gate)
        {
            if (Held.Count == 0) return;
            stale = Held.Where(h => (DateTime.UtcNow - h.Value).TotalMilliseconds > MaxHoldMs).Select(h => h.Key).ToList();
        }
        bool front = stale.Count > 0 || GameInFront();
        if (!front) { ReleaseAll(); return; }
        foreach (var combo in stale)
            Up(combo, combo.Split('+', StringSplitOptions.RemoveEmptyEntries).Select(n => Codes[n]).ToList());
    }

    static bool GameInFront()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            GetWindowThreadProcessId(hwnd, out uint pid);
            using var p = Process.GetProcessById((int)pid);
            return GameProcesses.Contains(p.ProcessName.ToLowerInvariant());
        }
        catch { return false; }
    }

    static void Send(int code, bool up)
    {
        uint flags = KEYEVENTF_SCANCODE | (up ? KEYEVENTF_KEYUP : 0u) | ((code & 0x100) != 0 ? KEYEVENTF_EXTENDEDKEY : 0u);
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = (ushort)(code & 0xFF), dwFlags = flags } },
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    const uint INPUT_KEYBOARD = 1, KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2, KEYEVENTF_SCANCODE = 0x8;

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public InputUnion u; }

    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
}
