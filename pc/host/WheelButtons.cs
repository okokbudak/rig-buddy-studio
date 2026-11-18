// Buttons of the wheel (Moza or any other game controller Windows knows) that switch the screens of the tablets (the app's
// screens): next / previous. (The dashboard hardware's Nextion displays are RigHardware.exe's, rig-buddy-hardware.)
// The controllers are read with Raw Input (the HID reports themselves, as SimHub's DirectInput sees them): every button of
// every controller (a wheel rim has far more than 32), also while the game is in front (RIDEV_INPUTSINK). A binding is the
// device's USB id (VID/PID) and the button number, so it survives a different device order; the settings show the device's
// own name.

using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

static class WheelButtons
{
    /** The tablets' screens: next, previous. */
    public static readonly string[] Actions = ["tablet.next", "tablet.prev"];

    static Thread? _thread;
    static volatile TaskCompletionSource<string>? _learning;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public static void Start()
    {
        if (_thread != null) return;
        // the first version had one pair for both: those bindings switched the displays and the tablets together
        foreach (string old in new[] { "Wheel.next", "Wheel.prev" }) if (Settings.Get(old) != null) Settings.Set(old, null);
        _thread = new Thread(Loop) { IsBackground = true, Name = "wheel buttons" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /** "VID_346E&PID_0006|14": the device and the button bound to the action, or null. */
    public static string? Binding(string action) => Settings.Get("Wheel." + action) is { Length: > 0 } v ? v : null;

    /** "MOZA R12 · button 14" for the settings page. */
    public static string? Label(string action) =>
        Binding(action) is { } b && b.LastIndexOf('|') is int i and > 0 ? $"{DisplayName(b[..i])} · {L.T("wheel.button", b[(i + 1)..])}" : null;

    public static void Clear(string action) => Settings.Set("Wheel." + action, null);

    /** Waits for the next button press on any controller (up to `timeout`) and binds it to the action; null: nothing pressed. */
    public static async Task<string?> Learn(string action, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _learning = tcs;
        var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        _learning = null;
        if (done != tcs.Task) return null;
        Settings.Set("Wheel." + action, tcs.Task.Result);
        return Label(action);
    }

    /** The controllers Windows has now: "VID_346E&PID_0006  MOZA R12 Base  128 buttons" (for the log and tests). */
    public static List<string> List()
    {
        var list = new List<string>();
        uint n = 0, size = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
        GetRawInputDeviceList(null, ref n, size);
        var devs = new RAWINPUTDEVICELIST[n];
        if (n == 0 || GetRawInputDeviceList(devs, ref n, size) == uint.MaxValue) return list;
        foreach (var d in devs.Take((int)n))
        {
            if (d.dwType != RIM_TYPEHID) continue;
            var dev = Open(d.hDevice);
            if (dev == null) continue;
            list.Add($"{dev.UsbId}  {dev.Name}  {dev.Buttons} buttons");
        }
        return list;
    }

    // ---- the devices ----

    sealed class Device
    {
        public required string UsbId, Name;
        public required IntPtr Preparsed;
        public required int Buttons;
        public required bool ReportIds;
        public readonly Dictionary<byte, HashSet<ushort>> Down = [];
    }

    static readonly Dictionary<IntPtr, Device?> Devices = [];
    static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase);

    /** The name Windows shows for a controller ("VID_346E&PID_0024" -> "MOZA Multi-function Stalk"), or a readable stand-in. */
    static string DisplayName(string usbId)
    {
        lock (Names) if (Names.TryGetValue(usbId, out var n)) return n;
        foreach (var root in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
        {
            try
            {
                using var k = root.OpenSubKey($@"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\{usbId}");
                if (k?.GetValue("OEMName") is string n && n.Trim().Length > 0) return n.Trim();
            }
            catch (Exception) { }
        }
        if (usbId.StartsWith("VID_346E", StringComparison.OrdinalIgnoreCase)) return "MOZA";
        return usbId;
    }

    /** A game controller (joystick, gamepad, multi-axis: wheels are one of these) with buttons; null for anything else. */
    static Device? Open(IntPtr h)
    {
        var info = new RID_DEVICE_INFO { cbSize = (uint)Marshal.SizeOf<RID_DEVICE_INFO>() };
        uint size = info.cbSize;
        if (GetRawInputDeviceInfoW(h, RIDI_DEVICEINFO, ref info, ref size) == uint.MaxValue) return null;
        if (info.dwType != RIM_TYPEHID || info.hid.usUsagePage != 1 || info.hid.usUsage is not (4 or 5 or 8)) return null;

        uint chars = 0;
        GetRawInputDeviceInfoW(h, RIDI_DEVICENAME, IntPtr.Zero, ref chars);
        var nameBuf = Marshal.AllocHGlobal((int)chars * 2 + 2);
        string path;
        try { GetRawInputDeviceInfoW(h, RIDI_DEVICENAME, nameBuf, ref chars); path = Marshal.PtrToStringUni(nameBuf) ?? ""; }
        finally { Marshal.FreeHGlobal(nameBuf); }
        string usbId = Regex.Match(path, @"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase) is { Success: true } m
            ? $"VID_{m.Groups[1].Value.ToUpperInvariant()}&PID_{m.Groups[2].Value.ToUpperInvariant()}"
            : $"VID_{info.hid.dwVendorId:X4}&PID_{info.hid.dwProductId:X4}";

        uint psize = 0;
        GetRawInputDeviceInfoW(h, RIDI_PREPARSEDDATA, IntPtr.Zero, ref psize);
        if (psize == 0) return null;
        IntPtr pre = Marshal.AllocHGlobal((int)psize);
        if (GetRawInputDeviceInfoW(h, RIDI_PREPARSEDDATA, pre, ref psize) == uint.MaxValue) { Marshal.FreeHGlobal(pre); return null; }

        // the button usages (page 9) of the input reports, and whether the reports start with a report id
        int buttons = 0; bool ids = false;
        if (HidP_GetCaps(pre, out var caps) == HIDP_STATUS_SUCCESS && caps.NumberInputButtonCaps > 0)
        {
            ushort count = caps.NumberInputButtonCaps;
            var bc = new byte[count * ButtonCapsSize];
            if (HidP_GetButtonCaps(0, bc, ref count, pre) == HIDP_STATUS_SUCCESS)
                for (int i = 0; i < count; i++)
                {
                    int o = i * ButtonCapsSize;
                    if (bc[o + 2] != 0) ids = true;
                    if (BitConverter.ToUInt16(bc, o) != 9) continue;
                    int max = bc[o + 12] != 0 ? BitConverter.ToUInt16(bc, o + 58) : BitConverter.ToUInt16(bc, o + 56);
                    buttons = Math.Max(buttons, max);
                }
        }
        if (buttons == 0) { Marshal.FreeHGlobal(pre); return null; }

        string name = Product(path) ?? DisplayName(usbId);
        lock (Names) Names[usbId] = name;
        return new Device { UsbId = usbId, Name = name, Preparsed = pre, Buttons = buttons, ReportIds = ids };
    }

    /** The product name the device itself reports ("MOZA R12 Base"). */
    static string? Product(string path)
    {
        using var f = CreateFileW(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (f.IsInvalid) return null;
        var buf = new byte[256];
        if (!HidD_GetProductString(f, buf, buf.Length)) return null;
        string s = Encoding.Unicode.GetString(buf).Split('\0')[0].Trim();
        return s.Length > 0 ? s : null;
    }

    // ---- the message loop: a message-only window receives WM_INPUT from every controller ----

    static WndProc? _proc; // kept alive: the window calls it

    static void Loop()
    {
        try
        {
            _proc = OnMessage;
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(), lpfnWndProc = _proc,
                hInstance = GetModuleHandleW(null), lpszClassName = "RigBuddyWheelButtons",
            };
            RegisterClassExW(ref wc);
            IntPtr hwnd = CreateWindowExW(0, wc.lpszClassName, "", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero) { Console.WriteLine($"wheel: no window ({Marshal.GetLastWin32Error()})"); return; }

            var reg = new[] { (ushort)4, (ushort)5, (ushort)8 }.Select(u => new RAWINPUTDEVICE
            { usUsagePage = 1, usUsage = u, dwFlags = RIDEV_INPUTSINK | RIDEV_DEVNOTIFY, hwndTarget = hwnd }).ToArray();
            if (!RegisterRawInputDevices(reg, (uint)reg.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            { Console.WriteLine($"wheel: raw input not registered ({Marshal.GetLastWin32Error()})"); return; }
            foreach (var line in List()) Console.WriteLine("wheel: " + line);

            while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref msg); DispatchMessageW(ref msg); }
        }
        catch (Exception e) { Console.WriteLine($"wheel: {e.Message}"); }
    }

    static IntPtr OnMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == WM_INPUT) OnInput(lParam);
            else if (msg == WM_INPUT_DEVICE_CHANGE && wParam == GIDC_REMOVAL && Devices.Remove(lParam, out var d) && d != null)
                Marshal.FreeHGlobal(d.Preparsed);
        }
        catch (Exception e) { Console.WriteLine($"wheel: {e.Message}"); }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    static byte[] _buf = new byte[1024];
    static readonly ushort[] Usages = new ushort[256];

    static void OnInput(IntPtr hRaw)
    {
        // a wheel base sends its axes a thousand times a second: nothing is read while no button is bound or learned
        if (_learning == null && !Actions.Any(a => Binding(a) != null)) return;

        int header = Marshal.SizeOf<RAWINPUTHEADER>();
        uint size = 0;
        GetRawInputData(hRaw, RID_INPUT, null, ref size, (uint)header);
        if (size == 0) return;
        if (_buf.Length < size) _buf = new byte[size];
        if (GetRawInputData(hRaw, RID_INPUT, _buf, ref size, (uint)header) == uint.MaxValue) return;
        if (BitConverter.ToUInt32(_buf, 0) != RIM_TYPEHID) return;
        IntPtr h = (IntPtr)(IntPtr.Size == 8 ? BitConverter.ToInt64(_buf, 8) : BitConverter.ToInt32(_buf, 8));

        if (!Devices.TryGetValue(h, out var dev)) Devices[h] = dev = Open(h);
        if (dev == null) return;

        int reportSize = BitConverter.ToInt32(_buf, header), count = BitConverter.ToInt32(_buf, header + 4);
        for (int r = 0; r < count; r++)
        {
            int at = header + 8 + r * reportSize;
            var report = new byte[reportSize];
            Buffer.BlockCopy(_buf, at, report, 0, reportSize);
            uint n = (uint)Usages.Length;
            if (HidP_GetUsages(0, 9, 0, Usages, ref n, dev.Preparsed, report, (uint)reportSize) != HIDP_STATUS_SUCCESS) continue;

            byte id = dev.ReportIds ? report[0] : (byte)0;
            var now = new HashSet<ushort>(Usages.Take((int)n));
            // the first report only tells what is held already (a switch left on is not a press)
            if (dev.Down.TryGetValue(id, out var down))
                foreach (ushort b in now) if (!down.Contains(b)) OnPress($"{dev.UsbId}|{b}");
            dev.Down[id] = now;
        }
    }

    static void OnPress(string key)
    {
        if (_learning is { } l) { l.TrySetResult(key); return; }
        foreach (string action in Actions)
            if (string.Equals(Binding(action), key, StringComparison.OrdinalIgnoreCase)) _ = Send(action);
    }

    /** "tablet.*": every tablet (the agent tells the apps). */
    static async Task Send(string action)
    {
        string dir = action.EndsWith(".prev") ? "prev" : "next";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:62843/screen")
            { Content = new StringContent($"{{\"dir\":\"{dir}\"}}", Encoding.UTF8, "application/json") };
            using var res = await Http.SendAsync(req);
            string body = await res.Content.ReadAsStringAsync();
            Console.WriteLine($"wheel: {action} -> {(int)res.StatusCode} {body}");
        }
        catch (Exception e) { Console.WriteLine($"wheel: {action}: {e.Message}"); }
    }

    // ---- Win32 ----

    const uint WM_INPUT = 0x00FF, WM_INPUT_DEVICE_CHANGE = 0x00FE;
    static readonly IntPtr GIDC_REMOVAL = 2, HWND_MESSAGE = -3;
    const uint RID_INPUT = 0x10000003, RIDI_PREPARSEDDATA = 0x20000005, RIDI_DEVICENAME = 0x20000007, RIDI_DEVICEINFO = 0x2000000b;
    const uint RIM_TYPEHID = 2, RIDEV_INPUTSINK = 0x100, RIDEV_DEVNOTIFY = 0x2000;
    const int HIDP_STATUS_SUCCESS = 0x00110000, ButtonCapsSize = 72;

    delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public WndProc lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICE { public ushort usUsagePage, usUsage; public uint dwFlags; public IntPtr hwndTarget; }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICELIST { public IntPtr hDevice; public uint dwType; }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTHEADER { public uint dwType, dwSize; public IntPtr hDevice, wParam; }

    [StructLayout(LayoutKind.Sequential)]
    struct RID_DEVICE_INFO_HID { public uint dwVendorId, dwProductId, dwVersionNumber; public ushort usUsagePage, usUsage; }

    // the union's largest member (the keyboard's) is 24 bytes
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    struct RID_DEVICE_INFO { public uint cbSize, dwType; public RID_DEVICE_INFO_HID hid; }

    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
        public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateWindowExW(uint ex, string cls, string name, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern int GetMessageW(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string? name);
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devs, uint n, uint size);
    [DllImport("user32.dll", SetLastError = true)] static extern uint GetRawInputDeviceList([In, Out] RAWINPUTDEVICELIST[]? list, ref uint n, uint size);
    [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr h, uint cmd, [In, Out] byte[]? data, ref uint size, uint header);
    [DllImport("user32.dll")] static extern uint GetRawInputDeviceInfoW(IntPtr h, uint cmd, IntPtr data, ref uint size);
    [DllImport("user32.dll")] static extern uint GetRawInputDeviceInfoW(IntPtr h, uint cmd, ref RID_DEVICE_INFO data, ref uint size);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr pre, out HIDP_CAPS caps);
    [DllImport("hid.dll")] static extern int HidP_GetButtonCaps(int type, [In, Out] byte[] caps, ref ushort len, IntPtr pre);
    [DllImport("hid.dll")]
    static extern int HidP_GetUsages(int type, ushort page, ushort link, [In, Out] ushort[] usages, ref uint len, IntPtr pre, byte[] report, uint reportLen);
    [DllImport("hid.dll")] static extern bool HidD_GetProductString(Microsoft.Win32.SafeHandles.SafeFileHandle h, [In, Out] byte[] buf, int len);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr sec, uint mode, uint flags, IntPtr tmpl);
}
