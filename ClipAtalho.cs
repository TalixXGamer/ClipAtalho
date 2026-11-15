// ClipAtalho - gerenciador de área de transferência portátil + expansão de texto.
// Versão V19 15-11-2026. Compilar: dê dois cliques no "Compilar ClipAtalho V19 15-11-2026.bat" (usa o csc.exe que já vem no Windows).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Net;
using System.Text;
using System.Threading;

static class ClipAtalho
{
    // ------------------------------------------------------------------ Win32
    delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    delegate IntPtr HookProcDelegate(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DRAWITEMSTRUCT
    {
        public uint CtlType, CtlID, itemID, itemAction, itemState;
        public IntPtr hwndItem, hDC;
        public RECT rcItem;
        public IntPtr itemData;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct GdiplusStartupInput
    {
        public uint GdiplusVersion;
        public IntPtr DebugEventCallback;
        public int SuppressBackgroundThread;
        public int SuppressExternalCodecs;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    const string U = "user32.dll", K = "kernel32.dll", G = "gdi32.dll";

    [DllImport(U, CharSet = CharSet.Unicode)] static extern ushort RegisterClassExW(ref WNDCLASSEX wc);
    [DllImport(U, CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowExW(uint exStyle, string cls, string name, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport(U)] static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport(U)] static extern int GetMessageW(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport(U)] static extern bool TranslateMessage(ref MSG msg);
    [DllImport(U)] static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport(U)] static extern void PostQuitMessage(int code);
    [DllImport(U)] static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport(U, SetLastError = true)] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport(U)] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport(U, SetLastError = true)] static extern IntPtr SetWindowsHookExW(int id, IntPtr proc, IntPtr hMod, uint threadId);
    [DllImport(U)] static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport(U)] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport(U)] static extern short GetAsyncKeyState(int vk);
    [DllImport(U, SetLastError = true)] static extern uint SendInput(uint n, [In] INPUT[] inputs, int size);
    [DllImport(U)] static extern bool OpenClipboard(IntPtr hWnd);
    [DllImport(U)] static extern bool CloseClipboard();
    [DllImport(U)] static extern bool EmptyClipboard();
    [DllImport(U)] static extern IntPtr GetClipboardData(uint fmt);
    [DllImport(U)] static extern IntPtr SetClipboardData(uint fmt, IntPtr h);
    [DllImport(U)] static extern bool IsClipboardFormatAvailable(uint fmt);
    [DllImport(U, CharSet = CharSet.Unicode)] static extern uint RegisterClipboardFormatW(string name);
    [DllImport(U)] static extern IntPtr GetClipboardOwner();
    [DllImport(U)] static extern bool AddClipboardFormatListener(IntPtr hWnd);
    [DllImport(U)] static extern IntPtr SetTimer(IntPtr hWnd, IntPtr id, uint ms, IntPtr proc);
    [DllImport(U)] static extern bool KillTimer(IntPtr hWnd, IntPtr id);
    [DllImport(U)] static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport(U)] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport(U)] static extern bool InvalidateRect(IntPtr hWnd, IntPtr rc, bool erase);
    [DllImport(U, EntryPoint = "InvalidateRect")] static extern bool InvalidateRectArea(IntPtr hWnd, ref RECT rc, bool erase);
    [DllImport(U)] static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT ps);
    [DllImport(U)] static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
    [DllImport(U)] static extern int FillRect(IntPtr hdc, ref RECT rc, IntPtr brush);
    [DllImport(U)] static extern int FrameRect(IntPtr hdc, ref RECT rc, IntPtr brush);
    [DllImport(U, CharSet = CharSet.Unicode)] static extern int DrawTextW(IntPtr hdc, string text, int count, ref RECT rc, uint format);
    [DllImport(U)] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport(U)] static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport(U)] static extern IntPtr GetForegroundWindow();
    [DllImport(U)] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport(U)] static extern bool GetWindowRect(IntPtr hWnd, out RECT rc);
    [DllImport(U)] static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO gti);
    [DllImport(U)] static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);
    [DllImport(U)] static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport(U)] static extern bool GetMonitorInfoW(IntPtr mon, ref MONITORINFO mi);
    [DllImport(U)] static extern bool GetCursorPos(out POINT pt);
    [DllImport(U)] static extern IntPtr CreatePopupMenu();
    [DllImport(U, CharSet = CharSet.Unicode)] static extern bool AppendMenuW(IntPtr menu, uint flags, IntPtr id, string text);
    [DllImport(U)] static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hWnd, IntPtr rc);
    [DllImport(U)] static extern bool DestroyMenu(IntPtr menu);
    [DllImport(U)] static extern IntPtr LoadIconW(IntPtr inst, IntPtr name);
    [DllImport(U)] static extern IntPtr LoadCursorW(IntPtr inst, IntPtr name);
    [DllImport(U, CharSet = CharSet.Unicode)] static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    [DllImport(U, CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessageW(string name);
    [DllImport(U)] static extern bool SetProcessDPIAware();
    [DllImport(K, CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string name);
    [DllImport(K)] static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport(K)] static extern IntPtr GlobalLock(IntPtr h);
    [DllImport(K)] static extern bool GlobalUnlock(IntPtr h);
    [DllImport(K)] static extern UIntPtr GlobalSize(IntPtr h);
    [DllImport(K)] static extern IntPtr GlobalFree(IntPtr h);
    [DllImport(G)] static extern IntPtr CreateSolidBrush(uint color);
    [DllImport(G, CharSet = CharSet.Unicode)] static extern IntPtr CreateFontW(int h, int w, int esc, int orient, int weight, uint italic, uint underline, uint strike, uint charset, uint outPrec, uint clipPrec, uint quality, uint pitch, string face);
    [DllImport(G)] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport(G)] static extern bool DeleteObject(IntPtr obj);
    [DllImport(G)] static extern int SetBkMode(IntPtr hdc, int mode);
    [DllImport(G)] static extern uint SetTextColor(IntPtr hdc, uint color);
    [DllImport(G)] static extern int GetDeviceCaps(IntPtr hdc, int index);
    [DllImport(U)] static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport(U, CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")] static extern IntPtr SendMessageStr(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);
    [DllImport(U, CharSet = CharSet.Unicode)] static extern bool SetWindowTextW(IntPtr hWnd, string text);
    [DllImport(U, CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport(U)] static extern int GetWindowTextLengthW(IntPtr hWnd);
    [DllImport(U)] static extern bool IsDialogMessageW(IntPtr hDlg, ref MSG msg);
    [DllImport(U)] static extern bool AdjustWindowRectEx(ref RECT rc, uint style, bool menu, uint exStyle);
    [DllImport(U)] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport(U)] static extern int GetSystemMetrics(int index);
    [DllImport(U)] static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport(U)] static extern IntPtr GetDlgItem(IntPtr hDlg, int id);
    [DllImport(U)] static extern bool EnableWindow(IntPtr hWnd, bool enable);
    [DllImport(U)] static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport(U)] static extern bool GetClientRect(IntPtr hWnd, out RECT rc);
    [DllImport(U, EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);
    [DllImport(U, EntryPoint = "SetWindowLongW")] static extern int SetWindowLong32(IntPtr hWnd, int index, int value);
    [DllImport(U)] static extern IntPtr CallWindowProcW(IntPtr prev, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport(G)] static extern uint SetBkColor(IntPtr hdc, uint color);
    [DllImport(U)] static extern bool DestroyIcon(IntPtr icon);
    [DllImport(U)] static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int w, int h, uint step, IntPtr brush, uint flags);
    [DllImport(G)] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport(G)] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport(G)] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport(G)] static extern bool DeleteDC(IntPtr hdc);
    const string GP = "gdiplus.dll";
    [DllImport(GP)] static extern int GdiplusStartup(out IntPtr token, ref GdiplusStartupInput input, IntPtr output);
    [DllImport(GP)] static extern int GdipCreateFromHDC(IntPtr hdc, out IntPtr graphics);
    [DllImport(GP)] static extern int GdipDeleteGraphics(IntPtr graphics);
    [DllImport(GP)] static extern int GdipSetSmoothingMode(IntPtr graphics, int mode);
    [DllImport(GP)] static extern int GdipCreatePath(int fillMode, out IntPtr path);
    [DllImport(GP)] static extern int GdipAddPathArc(IntPtr path, float x, float y, float w, float h, float start, float sweep);
    [DllImport(GP)] static extern int GdipClosePathFigure(IntPtr path);
    [DllImport(GP)] static extern int GdipDeletePath(IntPtr path);
    [DllImport(GP)] static extern int GdipCreateSolidFill(uint argb, out IntPtr brush);
    [DllImport(GP)] static extern int GdipDeleteBrush(IntPtr brush);
    [DllImport(GP)] static extern int GdipFillPath(IntPtr graphics, IntPtr brush, IntPtr path);
    [DllImport(GP)] static extern int GdipCreatePen1(uint argb, float width, int unit, out IntPtr pen);
    [DllImport(GP)] static extern int GdipDeletePen(IntPtr pen);
    [DllImport(GP)] static extern int GdipDrawPath(IntPtr graphics, IntPtr pen, IntPtr path);
    [DllImport(GP)] static extern int GdipCreateBitmapFromScan0(int w, int h, int stride, int format, IntPtr scan0, out IntPtr bitmap);
    [DllImport(GP)] static extern int GdipGetImageGraphicsContext(IntPtr image, out IntPtr graphics);
    [DllImport(GP)] static extern int GdipCreateHICONFromBitmap(IntPtr bitmap, out IntPtr hicon);
    [DllImport(GP)] static extern int GdipDisposeImage(IntPtr image);
    [DllImport(GP)] static extern int GdipCreateBitmapFromGdiDib(IntPtr bmi, IntPtr bits, out IntPtr bitmap);
    [DllImport(GP)] static extern int GdipGetImageWidth(IntPtr image, out uint w);
    [DllImport(GP)] static extern int GdipGetImageHeight(IntPtr image, out uint h);
    [DllImport(GP)] static extern int GdipDrawImageRectI(IntPtr graphics, IntPtr image, int x, int y, int w, int h);
    [DllImport(GP)] static extern int GdipSetInterpolationMode(IntPtr graphics, int mode);
    [DllImport(GP)] static extern int GdipGraphicsClear(IntPtr graphics, uint argb);
    [DllImport(GP, CharSet = CharSet.Unicode)] static extern int GdipSaveImageToFile(IntPtr image, string file, ref Guid encoder, IntPtr prms);
    [DllImport(GP, CharSet = CharSet.Unicode)] static extern int GdipCreateBitmapFromFile(string file, out IntPtr bitmap);
    [DllImport(GP)] static extern int GdipCreateHBITMAPFromBitmap(IntPtr bitmap, out IntPtr hbm, uint background);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);
    [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS m);
    [DllImport(G)] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [DllImport(U)] static extern int SetWindowRgn(IntPtr hWnd, IntPtr rgn, bool redraw);
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }
    [DllImport(U)] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint flags);
    [DllImport(U)] static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte alpha, uint flags);
    [DllImport(U)] static extern bool IsIconic(IntPtr hWnd);
    [DllImport(U)] static extern IntPtr SetCapture(IntPtr hWnd);
    [DllImport(U)] static extern bool ReleaseCapture();
    [DllImport(G, CharSet = CharSet.Unicode)] static extern bool GetTextExtentPoint32W(IntPtr hdc, string s, int len, out SIZE size);
    [DllImport(G)] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdiplus.dll")] static extern int GdipCreateBitmapFromHBITMAP(IntPtr hbm, IntPtr hpal, out IntPtr bitmap);
    [StructLayout(LayoutKind.Sequential)] struct GpRect { public int X, Y, Width, Height; }
    [StructLayout(LayoutKind.Sequential)]
    class GpBitmapData { public uint Width, Height; public int Stride, PixelFormat; public IntPtr Scan0, Reserved; }
    [DllImport("gdiplus.dll")] static extern int GdipBitmapLockBits(IntPtr bmp, ref GpRect rect, uint flags, int format, [In, Out] GpBitmapData data);
    [DllImport("gdiplus.dll")] static extern int GdipBitmapUnlockBits(IntPtr bmp, [In, Out] GpBitmapData data);
    [DllImport(U)] static extern IntPtr GetParent(IntPtr hWnd);
    [DllImport(U)] static extern IntPtr GetFocus();
    [DllImport("ole32.dll")] static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")] static extern void CoTaskMemFree(IntPtr p);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct BROWSEINFO
    {
        public IntPtr hwndOwner, pidlRoot, pszDisplayName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn, lParam;
        public int iImage;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHBrowseForFolderW(ref BROWSEINFO bi);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder path);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner, hInstance;
        public string lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter, nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir, lpstrTitle;
        public int Flags;
        public short nFileOffset, nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData, lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved, FlagsEx;
    }
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint DragQueryFileW(IntPtr hDrop, uint index, StringBuilder file, uint cch);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIconW(uint msg, ref NOTIFYICONDATA data);

    const uint WM_DESTROY = 0x0002, WM_PAINT = 0x000F, WM_MOUSEACTIVATE = 0x0021, WM_KEYDOWN = 0x0100,
        WM_SYSKEYDOWN = 0x0104, WM_TIMER = 0x0113, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205,
        WM_HOTKEY = 0x0312, WM_COMMAND = 0x0111, WM_CLOSE = 0x0010, WM_CLIPBOARDUPDATE = 0x031D,
        WM_TRAY = 0x8001, WM_SHOWPOPUP = 0x8002, WM_HIDEPOPUP = 0x8003, WM_FEEDBACKDONE = 0x8010;
    const int TIMER_WAITMODS = 1, TIMER_RESTORE = 2, TIMER_POPUP = 3, TIMER_CONFIG = 4, TIMER_FADE = 5,
        TIMER_WAVE = 6, TIMER_CLIPREAD = 7, TIMER_ARM = 8;
    const uint CF_UNICODETEXT = 13;
    const int VK_BACK = 0x08, VK_TAB = 0x09, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12,
        VK_ESCAPE = 0x1B, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    const int MODE_NONE = 0, MODE_SUGGEST = 1, MODE_HISTORY = 2;
    const int ID_LIST = 10, MAX_CLIPS = 20;
    const int CMD_SHOW = 1, CMD_EDIT = 2, CMD_DIR = 3, CMD_PAUSE = 4, CMD_CLEAR = 5, CMD_EXIT = 6, CMD_MANAGE = 7, CMD_HELP = 8;
    const int IDC_LIST = 100, IDC_EDIT = 101, IDC_UP = 102, IDC_DOWN = 103, IDC_PIN = 104, IDC_NEW = 105, IDC_DEL = 106;

    class Clip
    {
        public string Text;
        public bool Pinned;
        public IntPtr Image;          // imagem GDI+ (só nos clips de imagem)
        public IntPtr Thumb;          // miniatura leve, gerada uma vez (listas)
        public DateTime Added = DateTime.Now; // quando entrou na lista (autoexclusão)
        public bool Copied;                   // entrou por Ctrl+C (não por edição)
        public int W, H;
        public string Hash, ImgFile;  // Hash != null => clip de imagem
        public bool IsImage { get { return Hash != null; } }
        public Clip(string text, bool pinned) { Text = text; Pinned = pinned; }
    }

    // ------------------------------------------------------------------ estado
    static IntPtr hInst, hwndMain, hwndPopup, hHook, hFont, hFontBold;
    static double scale = 1.0;
    static WndProcDelegate mainProcRef, popupProcRef, mgrProcRef; // mantidos vivos (GC)
    static IntPtr hwndMgr, hList, hEdit, hUiFont, hUiBold, hSmallFont, hTitleFont, hBigFont, hBgBrush, hWhiteBrush, hAppIcon;
    static bool gdipOK;

    // Paleta (tema claro). COLORREF = 0x00BBGGRR (texto GDI); ARGB = 0xAARRGGBB (formas GDI+).
    // Paleta: definida por ApplyTheme() conforme o tema (claro/escuro) e a cor escolhida.
    // C_* = 0x00BBGGRR (texto GDI); A_* = 0xAARRGGBB (formas GDI+). A_WHITE é a cor dos cartões.
    const uint C_WHITE = 0x00FFFFFF; // texto sobre a cor de destaque
    static uint C_TEXT, C_GRAY, C_ACCENT, C_RED, C_BG, C_CARD, C_LINE;
    static uint A_WHITE, A_BORDER, A_BTNBORDER, A_SEL, A_ACCENT, A_ACCENT_DOWN, A_DOWN, A_BG, A_TEXT;
    static bool cfgDark;
    static int cfgColor, appliedTheme = -1;
    static readonly string[] ColorNames = { "verde", "azul", "roxo", "rosa", "laranja", "grafite" };
    static readonly uint[] ColorRgb = { 0x0F9D7A, 0x2563EB, 0x7C3AED, 0xDB2777, 0xEA580C, 0x475569 };

    static Clip mgrSel, editClip;
    static bool mgrLoading;
    static HookProcDelegate hookProcRef;

    static string exeDir, iniPath, clipsPath, logPath;
    static DateTime iniMod;

    static uint cfgMods = 2;
    static string cfgModsLabel = "Ctrl";
    static bool cfgExpand = true, cfgSaveAll = true, iniCreated, cfgImages = true, cfgListPos;
    static int cfgAutoDel; // minutos (0 = nunca, -1 = toda meia-noite)
    static DateTime autoDelDay = DateTime.Today;
    static readonly string[] AutoDelKeys = { "nunca", "30min", "1h", "2h", "24h", "meianoite" };
    static readonly string[] AutoDelNames = { "Nunca", "Depois de 30 minutos", "Depois de 1 hora", "Depois de 2 horas", "Depois de 24 horas", "Toda meia-noite" };
    static readonly int[] AutoDelMinutes = { 0, 30, 60, 120, 1440, -1 };
    static int cfgListX, cfgListY;
    static Dictionary<string, string> cfgAbbr = new Dictionary<string, string>();
    static List<string> cfgAbbrOrder = new List<string>();  // ordem dos atalhos na lista
    static Dictionary<string, DateTime> cfgAbbrDate = new Dictionary<string, DateTime>(); // data de criação de cada atalho
    static List<string> cfgAbbrEst = new List<string>(); // atalhos com data estimada (criados antes da V14)
    static List<string> cfgAbbrLoose = new List<string>();  // atalhos NÃO fixados (linha começa com "-" no .ini)

    static List<Clip> clips = new List<Clip>();
    static bool paused;

    static StringBuilder typed = new StringBuilder();
    static DateTime lastKey;
    static IntPtr lastFg;
    static int popMode = MODE_NONE;
    static string popAbbr = "";
    static List<string> popLines = new List<string>();
    static int popHover = -1; // linha da lista rápida sob o ponteiro do mouse (-2 = botão X)
    static HookProcDelegate mouseProcRef;
    static IntPtr hMouseHook;

    // Clique em qualquer lugar fora da lista rápida (ou da sugestão) fecha a janela.
    static IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode == 0 && popMode != MODE_NONE)
            {
                uint m = (uint)wParam.ToInt64();
                if (m == 0x0201 || m == 0x0204 || m == 0x0207 || m == 0x020B) // botão esquerdo, direito, meio, laterais
                {
                    int x = Marshal.ReadInt32(lParam), y = Marshal.ReadInt32(lParam, 4);
                    RECT rc;
                    if (!GetWindowRect(hwndPopup, out rc) || x < rc.Left || x >= rc.Right || y < rc.Top || y >= rc.Bottom)
                        PostMessageW(hwndMain, WM_HIDEPOPUP, IntPtr.Zero, IntPtr.Zero);
                }
            }
        }
        catch { }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    static string pendText = "";
    static int pendBS;
    static DateTime pendDeadline;
    static string savedClip;
    static bool restoring;

    static uint fmtExclude, msgTaskbar;
    static NOTIFYICONDATA nid;
    static bool trayReady;
    static bool[] registeredOK = new bool[21]; // 1..10 = teclado principal, 11..20 = teclado numérico

    // Registro de diagnóstico (ClipAtalho.log). Nunca grava o conteúdo dos clips, só tamanhos.
    static void Log(string s)
    {
        try { File.AppendAllText(logPath, DateTime.Now.ToString("dd/MM HH:mm:ss.fff") + "  " + s + "\r\n"); }
        catch { }
    }

    static int Px(int v) { return (int)(v * scale + 0.5); }
    static bool KeyDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
    static bool IsModifierVk(int vk)
    {
        return vk == 0x10 || vk == 0x11 || vk == 0x12 || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C;
    }

    // ------------------------------------------------------------------ configuração
    const string DefaultIni =
@"; ===== ClipAtalho - configuração e clips =====
; Este arquivo guarda as configurações E a lista de clips (seção [clips], no final).
; Para usar em outro PC, leve o .exe do ClipAtalho + este ClipAtalho.ini juntos. Não apague este arquivo.
; Ao salvar alterações nas configurações, o programa recarrega sozinho em até 2 segundos.

[geral]
; Teclas modificadoras dos atalhos (combine Ctrl, Shift, Alt, Win).
; Ctrl+1..9 cola o clip da posição 1..9; Ctrl+0 mostra a lista rápida.
; Se atrapalhar atalhos de outros programas (Excel, navegador), troque para: modificadores=Ctrl+Shift
modificadores=Ctrl
; Guardar em disco também os clips não fixados (sim/nao). Os fixados são sempre guardados.
salvar_historico=sim
; Aparência: tema = claro ou escuro; cor = verde, azul, roxo, rosa, laranja ou grafite
tema=escuro
cor=verde
; Expansão de texto (digitar o atalho e apertar Tab): sim ou nao
expansao=sim
; Clips de imagem (prints e figuras copiadas): sim ou nao. As imagens ficam na pasta ""ClipAtalho imagens"".
imagens=sim
; Apagar sozinho os clips NÃO fixados: nunca, 30min, 1h, 2h, 24h ou meianoite (os fixados nunca são apagados)
autoexclusao=nunca

[atalhos]
; atalho=texto   (cadastre e edite pela tela ""Atalhos por Expansão"")
; No texto, \n representa quebra de linha. Texto guardado em arquivo: prst=@prst.txt
";

    // Exemplos que vêm num .ini novo (o .ini de quem já usa o programa não é alterado).
    static readonly string[] DefaultClips = {
        "🎉 Parabéns por começar a usar o ClipAtalho!\nEste texto foi colado com Ctrl+1, sem abrir nada e sem copiar de novo.\nAgora experimente o Ctrl+2 (o que é o ClipAtalho) e o Ctrl+3 (manual rápido).",

        "ClipAtalho: escreva menos e faça mais ⚡\n\nVocê digita as mesmas coisas todo dia? Um “Bom dia, tudo bem?”, um “E aí, como estão as coisas?”...\n\nCom o ClipAtalho, você cria um atalho curto para a frase que quiser. Por exemplo, você digita b1, aperta TAB quando a sugestão aparecer, e a frase completa surge na tela. ✨ Você escolhe tanto o atalho quanto a frase.\n\nFunciona também com textos maiores: nome completo, assinatura, respostas prontas e até mesmo aquele textão pra quem gosta 😅\n\nE tem mais:\n🔹 CTRL+1 até CTRL+9: você escolhe os textos que mais usa e coloca cada um em uma dessas teclas. Depois é só apertar e o texto é colado na hora, sem abrir navegador, procurar documento ou copiar de novo.\n🔹 CTRL+0: abre sua lista de textos. Se esquecer qual número é qual, é só olhar e clicar.\n\nPara que serve:\n✅ Saudações e mensagens do dia a dia\n✅ Respostas padrão do trabalho\n✅ Assinaturas, endereços e dados repetitivos\n✅ Prompts favoritos de IA, como aplicar estilo 3D em uma foto, por exemplo\n\nPor que vale a pena:\nÉ como o Win+V do Windows, só que melhor e do seu jeito. Ele fica quietinho na bandeja do Windows e aparece quando você precisa. Menos digitação, menos cliques e mais tempo livre.\n\nTeste em menos de 10 segundos: abra o programa, abra um Bloco de Notas e aperte CTRL+3.\nA lista já vem com exemplos. Depois é só apagar tudo e montar a sua.\n\nTesta e me fala como funcionou por aí 😉",

        "📘 MANUAL RÁPIDO DO CLIPATALHO\n\n" +
        "COLAR COM ATALHO\n" +
        "• Ctrl+1 a Ctrl+9: cola o texto da posição 1 a 9 (vale também no teclado numérico).\n" +
        "• Ctrl+0: abre a lista com todos os textos. Clique para colar ou aperte 1 a 9. A lixeira apaga um item e “Limpar” (dois cliques) apaga os não fixados. Fecha com Esc ou com um clique fora.\n\n" +
        "COMO OS TEXTOS ENTRAM NA LISTA\n" +
        "• Tudo que você copia (Ctrl+C) entra sozinho na lista, inclusive imagens e prints.\n" +
        "• Para criar à mão: clique no ícone do ClipAtalho perto do relógio › “+ Novo” › digite › “Salvar”.\n\n" +
        "FIXAR (★)\n" +
        "• Um clip fixado não sai do lugar e fica sempre guardado. Os não fixados vão descendo a cada nova cópia.\n\n" +
        "ATALHOS POR EXPANSÃO\n" +
        "• Cadastre um atalho curto e o texto dele (ex.: bd1 = Bom dia! Tudo bem?).\n" +
        "• Em qualquer programa, digite o atalho logo depois de um espaço: aparece a sugestão. Aperte Tab e o texto entra no lugar. Esc ignora.\n" +
        "• Já vem com exemplos: b1, bd1, bt1, bn1, obg, agd, att, end1, ia3d e cla.\n\n" +
        "ORGANIZAR\n" +
        "• Arraste para reordenar e use o botão direito para mais opções.\n" +
        "• “Salvar” pergunta se é uma alteração ou um item novo.\n" +
        "• Errou? Ctrl+Z desfaz e Ctrl+Y refaz.\n\n" +
        "CONFIGURAÇÕES\n" +
        "• Troque o Ctrl por outra combinação, escolha tema claro ou escuro, a cor e se inicia com o Windows.\n" +
        "• Dados e backup: backup, restaurar, apagar sozinho os não fixados, pasta compartilhada entre computadores e padrão de fábrica.\n\n" +
        "DÚVIDAS OU SUGESTÕES\n" +
        "• Ajuda › “Enviar sugestão / reclamação”.\n\n" +
        "Desenvolvido por TalixXGamer",

        "Bom dia! Tudo bem? 😊",
        "Obrigado pelo retorno! Qualquer dúvida, estou à disposição.",
        "Atenciosamente,\n[Seu nome]\n[Seu cargo] | [Sua empresa]\n[Seu telefone]",
        "Rua Exemplo, 123 – Apto 45\nBairro Centro – Cidade/UF\nCEP 00000-000",
        "Transforme esta foto em uma ilustração 3D no estilo de animação, com cores vibrantes, iluminação suave e fundo desfocado, mantendo as feições e a pose originais.",
        "Este é o clip nº 9, um exemplo. Apague os exemplos e coloque aqui o texto que você mais usa.\nDica: no Ctrl+0, passe o mouse no item e clique na lixeira."
    };

    static readonly string[][] DefaultAbbr = {
        new string[] { "b1", "Bom dia, tudo bem?" },
        new string[] { "bd1", "Bom dia! Tudo bem?" },
        new string[] { "bt1", "Boa tarde! Tudo bem?" },
        new string[] { "bn1", "Boa noite! Tudo bem?" },
        new string[] { "obg", "Obrigado pelo retorno! Qualquer dúvida, estou à disposição." },
        new string[] { "agd", "Fico no aguardo do seu retorno. Obrigado!" },
        new string[] { "att", "Atenciosamente,\n[Seu nome]\n[Seu cargo] | [Sua empresa]\n[Seu telefone]" },
        new string[] { "end1", "Rua Exemplo, 123 – Apto 45, Bairro Centro – Cidade/UF, CEP 00000-000" },
        new string[] { "ia3d", "Transforme esta foto em uma ilustração 3D no estilo de animação, com cores vibrantes, iluminação suave e fundo desfocado, mantendo as feições e a pose originais." },
        new string[] { "cla", "ClipAtalho: escreva menos e faça mais ⚡" }
    };

    static string DefaultAbbrSection()
    {
        StringBuilder sb = new StringBuilder();
        foreach (string[] a in DefaultAbbr) sb.Append(a[0]).Append('=').Append(EscAbbr(a[1])).Append("\r\n");
        sb.Append("\r\n[atalhos_criacao]\r\n");
        string now = DateTime.Now.ToString("yyyyMMddHHmmss");
        foreach (string[] a in DefaultAbbr) sb.Append(a[0]).Append('=').Append(now).Append("\r\n");
        return sb.ToString();
    }

    static string DefaultClipsSection()
    {
        StringBuilder sb = new StringBuilder("\r\n[clips]\r\n");
        foreach (string t in DefaultClips) sb.Append("fixo=").Append(EscClip(t)).Append("\r\n");
        return sb.ToString();
    }

    static string ModsLabel(uint mods)
    {
        List<string> names = new List<string>();
        if ((mods & 2) != 0) names.Add("Ctrl");
        if ((mods & 4) != 0) names.Add("Shift");
        if ((mods & 1) != 0) names.Add("Alt");
        if ((mods & 8) != 0) names.Add("Win");
        return string.Join("+", names.ToArray());
    }

    // Grava (ou cria) uma chave do .ini preservando o restante do arquivo.
    static void SetIniValue(string section, string key, string value)
    {
        try
        {
            string text = ReadIni() ?? DefaultIni;
            List<string> lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            if (lines.Count > 0) lines[0] = lines[0].TrimStart((char)0xFEFF);
            string cur = "";
            int secLine = -1;
            bool done = false;
            for (int i = 0; i < lines.Count && !done; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("[") && t.EndsWith("]"))
                {
                    cur = t.Substring(1, t.Length - 2).Trim().ToLowerInvariant();
                    if (cur == section) secLine = i;
                    continue;
                }
                if (cur != section || t.Length == 0 || t[0] == ';' || t[0] == '#') continue;
                int eq = t.IndexOf('=');
                if (eq < 1) continue;
                if (t.Substring(0, eq).Trim().ToLowerInvariant() == key)
                {
                    lines[i] = key + "=" + value;
                    done = true;
                }
            }
            if (!done)
            {
                if (secLine >= 0) lines.Insert(secLine + 1, key + "=" + value);
                else
                {
                    lines.Insert(0, "[" + section + "]");
                    lines.Insert(1, key + "=" + value);
                    lines.Insert(2, "");
                }
            }
            WriteIni(string.Join("\r\n", lines.ToArray()));
        }
        catch (Exception e) { Log("FALHA ao gravar configuração no .ini: " + e.Message); }
    }

    static void ParseMods(string s, out uint mods, out string label)
    {
        uint m = 0;
        List<string> names = new List<string>();
        foreach (string raw in s.Split('+'))
        {
            string t = raw.Trim().ToLowerInvariant();
            if ((t == "ctrl" || t == "control") && (m & 2) == 0) { m |= 2; names.Add("Ctrl"); }
            else if (t == "shift" && (m & 4) == 0) { m |= 4; names.Add("Shift"); }
            else if (t == "alt" && (m & 1) == 0) { m |= 1; names.Add("Alt"); }
            else if (t == "win" && (m & 8) == 0) { m |= 8; names.Add("Win"); }
        }
        if (m == 0) { mods = 2; label = "Ctrl"; return; }
        mods = m;
        label = string.Join("+", names.ToArray());
    }

    static bool ValidAbbr(string s)
    {
        if (s.Length == 0 || s.Length > 32) return false;
        foreach (char c in s)
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))) return false;
        return true;
    }

    static string Unescape(string s)
    {
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[i + 1];
                if (n == 'n') { b.Append("\r\n"); i++; continue; }
                if (n == 't') { b.Append('\t'); i++; continue; }
                if (n == '\\') { b.Append('\\'); i++; continue; }
            }
            b.Append(s[i]);
        }
        return b.ToString();
    }

    public static void ParseConfig(string text)
    {
        cfgAbbr = new Dictionary<string, string>();
        cfgAbbrOrder = new List<string>();
        cfgAbbrDate = new Dictionary<string, DateTime>();
        cfgAbbrEst = new List<string>();
        cfgAbbrLoose = new List<string>();
        cfgDark = true;
        cfgColor = 0;
        cfgExpand = true;
        cfgSaveAll = true;
        cfgImages = true;
        cfgListPos = false;
        cfgAutoDel = 0;
        ParseMods("", out cfgMods, out cfgModsLabel);
        string section = "";
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim().Trim((char)0xFEFF).Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
            if (line[0] == '[' && line[line.Length - 1] == ']')
            {
                section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq < 1) continue;
            string key = line.Substring(0, eq).Trim().ToLowerInvariant();
            string val = line.Substring(eq + 1).Trim();
            if (section == "geral")
            {
                if (key == "modificadores") ParseMods(val, out cfgMods, out cfgModsLabel);
                else if (key == "tema") cfgDark = val.ToLowerInvariant() != "claro";
                else if (key == "cor") cfgColor = Math.Max(0, Array.IndexOf(ColorNames, val.ToLowerInvariant()));
                else if (key == "expansao")
                {
                    string v = val.ToLowerInvariant();
                    cfgExpand = (v == "sim" || v == "s" || v == "1" || v == "on");
                }
                else if (key == "autoexclusao")
                {
                    int ai = Array.IndexOf(AutoDelKeys, val.ToLowerInvariant());
                    cfgAutoDel = ai > 0 ? AutoDelMinutes[ai] : 0;
                }
                else if (key == "lista_pos")
                {
                    string[] xy = val.Split(',');
                    int lx, ly;
                    if (xy.Length == 2 && int.TryParse(xy[0].Trim(), out lx) && int.TryParse(xy[1].Trim(), out ly))
                    {
                        cfgListPos = true;
                        cfgListX = lx;
                        cfgListY = ly;
                    }
                }
                else if (key == "imagens")
                {
                    string v = val.ToLowerInvariant();
                    cfgImages = (v == "sim" || v == "s" || v == "1" || v == "on");
                }
                else if (key == "salvar_historico")
                {
                    string v = val.ToLowerInvariant();
                    cfgSaveAll = (v == "sim" || v == "s" || v == "1" || v == "on");
                }
            }
            else if (section == "atalhos_criacao")
            {
                DateTime dt;
                bool est = val.StartsWith("~"); // "~" = data estimada
                if (DateTime.TryParseExact(est ? val.Substring(1) : val, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out dt))
                {
                    cfgAbbrDate[key] = dt;
                    if (est) cfgAbbrEst.Add(key);
                }
            }
            else if (section == "atalhos")
            {
                bool loose = key.StartsWith("-"); // "-atalho=texto" = não fixado
                if (loose) key = key.Substring(1).Trim();
                if (ValidAbbr(key) && val.Length > 0)
                {
                    if (!cfgAbbr.ContainsKey(key)) cfgAbbrOrder.Add(key);
                    cfgAbbr[key] = val.StartsWith("@") ? val : Unescape(val);
                    cfgAbbrLoose.Remove(key);
                    if (loose) cfgAbbrLoose.Add(key);
                }
            }
        }
    }

    static string NormalizeText(string s)
    {
        s = s.Trim((char)0xFEFF).Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n');
        return s.Replace("\n", "\r\n");
    }

    static string ReadTextFile(string path)
    {
        byte[] b;
        try { b = File.ReadAllBytes(path); }
        catch { return null; }
        try { return new UTF8Encoding(false, true).GetString(b); }
        catch { return Encoding.Default.GetString(b); } // arquivo ANSI (Bloco de Notas antigo)
    }

    // ------------------------------------------------------------------ arquivo de dados (.ini)
    static bool syncHold;      // conflito pendente ("perguntar ao fechar"): as gravações ficam na memória
    static string iniCache, lastWritten;
    static bool dataOn, dataMissing, askingConflict;
    static string dataDir = "";
    static int conflictMode; // 0 = recarregar sozinho, 1 = perguntar na hora, 2 = perguntar ao fechar
    static readonly string[] ConflictKeys = { "auto", "perguntar", "aofechar" };
    static readonly string[] ConflictNames = { "Carregar a versão nova sozinho", "Perguntar na hora o que fazer", "Perguntar ao fechar o programa" };
    static string LocalIniPath { get { return Path.Combine(exeDir, "ClipAtalho.local.ini"); } }

    static string ReadIni()
    {
        if (syncHold && iniCache != null) return iniCache;
        return ReadTextFile(iniPath);
    }

    static void WriteIni(string text)
    {
        lastWritten = text;
        if (syncHold) { iniCache = text; return; }
        File.WriteAllText(iniPath, text, new UTF8Encoding(true));
        try { iniMod = File.GetLastWriteTimeUtc(iniPath); } catch { } // não recarrega por causa da própria gravação
    }

    // Configuração só deste computador (fica ao lado do .exe): pasta de dados e o que fazer em conflitos.
    static void LoadLocal()
    {
        dataOn = false;
        dataDir = "";
        conflictMode = 0;
        string t = ReadTextFile(LocalIniPath);
        if (t == null) return;
        foreach (string raw in t.Split('\n'))
        {
            string l = raw.Trim().TrimStart((char)0xFEFF);
            int eq = l.IndexOf('=');
            if (eq < 1 || l.StartsWith(";")) continue;
            string k = l.Substring(0, eq).Trim().ToLowerInvariant(), v = l.Substring(eq + 1).Trim();
            if (k == "usar_pasta") dataOn = v.ToLowerInvariant() == "sim";
            else if (k == "pasta") dataDir = v;
            else if (k == "conflito") conflictMode = Math.Max(0, Array.IndexOf(ConflictKeys, v.ToLowerInvariant()));
        }
        if (dataDir.Length == 0) dataOn = false;
    }

    static void SaveLocal()
    {
        try
        {
            File.WriteAllText(LocalIniPath, "; Configuração só deste computador (não vai para a pasta de dados)\r\n[local]\r\nusar_pasta=" +
                (dataOn ? "sim" : "nao") + "\r\npasta=" + dataDir + "\r\nconflito=" + ConflictKeys[conflictMode] + "\r\n", new UTF8Encoding(true));
        }
        catch (Exception e) { Log("FALHA ao gravar ClipAtalho.local.ini: " + e.Message); }
    }

    static void ApplyDataPaths()
    {
        dataMissing = false;
        string dir = exeDir;
        if (dataOn)
        {
            if (Directory.Exists(dataDir)) dir = dataDir;
            else { dataMissing = true; Log("Pasta de dados indisponível (" + dataDir + "): usando a pasta do programa"); }
        }
        iniPath = Path.Combine(dir, "ClipAtalho.ini");
    }

    // Recarrega tudo do .ini (configurações, clips e atalhos).
    static void ReloadAll()
    {
        foreach (Clip c in clips) if (!InUndoHistory(c)) DropImage(c);
        clips = new List<Clip>();
        mgrSel = null;
        editClip = null;
        LoadConfig();
        LoadClips();
        EnsureAbbrDates();
        if (hwndMgr != IntPtr.Zero && IsWindowVisible(hwndMgr))
        {
            editDirty = false;
            expDirty = false;
            RefreshList();
            RefreshX(true);
            RepaintAll();
        }
        RefreshPopupList();
    }

    static string Stamp() { return DateTime.Now.ToString("dd-MM-yyyy HHmm"); }

    static void CopyIniAside(string label)
    {
        try { if (File.Exists(iniPath)) File.Copy(iniPath, Path.Combine(DataDir, "ClipAtalho (" + label + " " + Stamp() + ").ini"), true); }
        catch { }
    }

    // O .ini mudou fora do programa (outro computador na pasta de dados, ou editado à mão).
    static void OnIniChangedOutside()
    {
        if (syncHold || askingConflict) return;
        if (!dataOn || dataMissing) { LoadConfig(); return; } // editado à mão: comportamento de sempre
        Log("ClipAtalho.ini alterado em outro computador");
        if (conflictMode == 0) { ReloadAll(); return; }
        if (conflictMode == 1)
        {
            askingConflict = true;
            try { iniMod = File.GetLastWriteTimeUtc(iniPath); } catch { }
            dlgEscResult = 1;
            dlgPrimary = 1;
            int r = AskDialog("Dados alterados em outro computador",
                "Os clips/atalhos da pasta de dados foram alterados em outro computador.\nCarregar a versão nova, ou manter a deste computador? (a outra fica guardada como cópia)",
                "Carregar a nova", "Manter a minha", null);
            askingConflict = false;
            if (r == 2 && lastWritten != null)
            {
                CopyIniAside("outro computador");
                WriteIni(lastWritten);
            }
            else ReloadAll();
            return;
        }
        syncHold = true; // decide ao fechar: até lá, nada é gravado na pasta compartilhada
        iniCache = lastWritten ?? ReadTextFile(iniPath);
        Balloon("ClipAtalho", "Os dados foram alterados em outro computador. Ao fechar o programa, você escolhe qual versão fica.");
    }

    // Conflito pendente: pergunta ao sair (ou, se o Windows estiver desligando, guarda a sua versão como cópia).
    static void ResolveHold(bool canAsk)
    {
        if (!syncHold) return;
        syncHold = false;
        string mine = iniCache;
        iniCache = null;
        if (mine == null) return;
        if (!canAsk)
        {
            try { File.WriteAllText(Path.Combine(DataDir, "ClipAtalho (este computador " + Stamp() + ").ini"), mine, new UTF8Encoding(true)); } catch { }
            return;
        }
        dlgEscResult = 2;
        dlgPrimary = 1;
        int r = AskDialog("Qual versão fica?",
            "Os dados foram alterados em outro computador enquanto o ClipAtalho estava aberto aqui.\nQual versão deve ficar na pasta de dados? (a que sair fica guardada como cópia)",
            "A deste computador", "A do outro computador", null);
        if (r == 1)
        {
            CopyIniAside("outro computador");
            WriteIni(mine);
        }
        else
        {
            try { File.WriteAllText(Path.Combine(DataDir, "ClipAtalho (este computador " + Stamp() + ").ini"), mine, new UTF8Encoding(true)); } catch { }
        }
    }

    static void LoadConfig()
    {
        try
        {
            if (!File.Exists(iniPath)) iniCreated = true; // primeira execução: liga "Iniciar com o Windows"
            if (!File.Exists(iniPath))
                WriteIni(DefaultIni.Replace("\r\n", "\n").Replace("\n", "\r\n") + DefaultAbbrSection() + DefaultClipsSection());
            iniMod = File.GetLastWriteTimeUtc(iniPath);
        }
        catch { }
        ParseConfig(ReadIni() ?? "");
        typed.Length = 0;
        if (appliedTheme != cfgColor * 2 + (cfgDark ? 1 : 0)) ApplyTheme();
        RegisterHotkeys();
    }

    static string AbbrText(string abbr)
    {
        string v;
        if (!cfgAbbr.TryGetValue(abbr, out v)) return null;
        if (v.StartsWith("@"))
        {
            string p = v.Substring(1).Trim();
            try { if (!Path.IsPathRooted(p)) p = Path.Combine(exeDir, p); }
            catch { p = null; }
            string t = p == null ? null : ReadTextFile(p);
            if (t != null) v = NormalizeText(t); // se o arquivo não existe, vale o texto como está
        }
        return v.Length > 0 ? v : null;
    }

    static void RegisterHotkeys()
    {
        for (int i = 1; i <= 20; i++)
            if (registeredOK[i]) { UnregisterHotKey(hwndMain, i); registeredOK[i] = false; }
        List<string> failed = new List<string>();
        for (int i = 1; i <= 20; i++)
        {
            int digit = (i - 1) % 10 + 1;           // 1..9 e 10 (= tecla 0)
            if (digit == 10) digit = 0;
            uint vk = i <= 10 ? (uint)('0' + digit) : (uint)(0x60 + digit); // 0x60 = VK_NUMPAD0
            registeredOK[i] = RegisterHotKey(hwndMain, i, cfgMods | 0x4000, vk);
            if (!registeredOK[i])
                failed.Add((i <= 10 ? "" : "Num") + digit + "(erro " + Marshal.GetLastWin32Error() + ")");
        }
        Log("Atalhos " + cfgModsLabel + "+0..9: " + (failed.Count == 0 ? "todos registrados"
            : "NAO registrados: " + string.Join(", ", failed.ToArray()) + " -> usando modo alternativo (gancho de teclado)"));
        SetTip();
    }

    // ------------------------------------------------------------------ lista de clips
    // Novo texto copiado entra na primeira posição NÃO fixada; clips fixados não saem do lugar.
    static bool Same(Clip a, Clip b)
    {
        return a.IsImage == b.IsImage && (a.IsImage ? a.Hash == b.Hash : a.Text == b.Text);
    }

    public static void AddCopied(string t) { AddCopiedClip(new Clip(t, false)); }

    // Novo item copiado entra na primeira posição NÃO fixada; clips fixados não saem do lugar.
    static void AddCopiedClip(Clip cand)
    {
        cand.Copied = true;
        foreach (Clip c in clips) if (c.Pinned && Same(c, cand)) { DropImage(cand); return; }
        Clip front = null;
        foreach (Clip c in clips) if (!c.Pinned && Same(c, cand)) { front = c; break; }
        if (front == null) front = cand;
        else DropImage(cand);
        List<Clip> un = new List<Clip>();
        un.Add(front);
        int pinned = 0;
        foreach (Clip c in clips)
        {
            if (c.Pinned) pinned++;
            else if (c != front && !Same(c, cand)) un.Add(c);
        }
        int maxUn = Math.Max(0, MAX_CLIPS - pinned);
        if (un.Count > maxUn) un.RemoveRange(maxUn, un.Count - maxUn);
        List<Clip> res = new List<Clip>();
        int total = pinned + un.Count, u = 0;
        for (int i = 0; i < total; i++)
        {
            if (i < clips.Count && clips[i].Pinned) res.Add(clips[i]);
            else if (u < un.Count) res.Add(un[u++]);
        }
        for (int i = total; i < clips.Count; i++) if (clips[i].Pinned) res.Add(clips[i]);
        while (u < un.Count) res.Add(un[u++]);
        clips = res;
        ReleaseUnusedImages();
        if (cfgSaveAll) SaveClips();
        RefreshList();
    }

    // ------------------------------------------------------------------ clips de imagem
    const uint CF_BITMAP = 2, CF_DIB = 8, CF_HDROP = 15;
    static List<Clip> imgClips = new List<Clip>(); // clips que têm imagem carregada na memória
    static Guid PngEncoder = new Guid("557CF406-1A04-11D3-9A73-0000F81EF32E");
    static string DataDir { get { return Path.GetDirectoryName(iniPath); } }
    static string ImgDir { get { return Path.Combine(DataDir, "ClipAtalho imagens"); } }

    static void DropImage(Clip c)
    {
        if (c == null) return;
        if (c.Thumb != IntPtr.Zero) { GdipDisposeImage(c.Thumb); c.Thumb = IntPtr.Zero; }
        if (c.Image == IntPtr.Zero) return;
        GdipDisposeImage(c.Image);
        c.Image = IntPtr.Zero;
        imgClips.Remove(c);
    }

    // Miniatura pequena, feita uma única vez: as listas desenham só ela (o original fica para colar).
    static IntPtr MakeThumb(IntPtr img, int w, int h)
    {
        int maxW = Px(140), maxH = Px(90);
        double sc = Math.Min(1.0, Math.Min((double)maxW / w, (double)maxH / h));
        int tw = Math.Max(1, (int)(w * sc)), th = Math.Max(1, (int)(h * sc));
        IntPtr t, g;
        if (GdipCreateBitmapFromScan0(tw, th, 0, 0x00021808, IntPtr.Zero, out t) != 0) return IntPtr.Zero;
        if (GdipGetImageGraphicsContext(t, out g) != 0) { GdipDisposeImage(t); return IntPtr.Zero; }
        GdipGraphicsClear(g, 0xFFFFFFFF);
        GdipSetInterpolationMode(g, 7);
        GdipDrawImageRectI(g, img, 0, 0, tw, th);
        GdipDeleteGraphics(g);
        return t;
    }

    static IntPtr ThumbOf(Clip c) { return c.Thumb != IntPtr.Zero ? c.Thumb : c.Image; }

    // Libera da memória as imagens de clips que saíram da lista.
    static void ReleaseUnusedImages()
    {
        foreach (Clip c in imgClips.ToArray())
            if (!clips.Contains(c) && !InUndoHistory(c)) DropImage(c);
    }

    static string HashBytes(byte[] data)
    {
        ulong h = 14695981039346656037UL; // FNV-1a 64 bits
        for (int i = 0; i < data.Length; i++) { h ^= data[i]; h *= 1099511628211UL; }
        return h.ToString("x16");
    }

    // Cópia independente da imagem (24 bits, fundo branco onde houver transparência).
    static IntPtr CopyImage(IntPtr src, int w, int h)
    {
        IntPtr dst, g;
        if (GdipCreateBitmapFromScan0(w, h, 0, 0x00021808, IntPtr.Zero, out dst) != 0) return IntPtr.Zero;
        if (GdipGetImageGraphicsContext(dst, out g) != 0) { GdipDisposeImage(dst); return IntPtr.Zero; }
        GdipGraphicsClear(g, 0xFFFFFFFF);
        GdipDrawImageRectI(g, src, 0, 0, w, h);
        GdipDeleteGraphics(g);
        return dst;
    }

    static Clip ImageClipFrom(IntPtr src, string hash, bool pinned)
    {
        uint w, h;
        if (GdipGetImageWidth(src, out w) != 0 || GdipGetImageHeight(src, out h) != 0) return null;
        if (w == 0 || h == 0 || w > 20000 || h > 20000) return null;
        IntPtr img = CopyImage(src, (int)w, (int)h);
        if (img == IntPtr.Zero) return null;
        Clip c = new Clip("", pinned);
        c.Image = img;
        c.Thumb = MakeThumb(img, (int)w, (int)h);
        c.W = (int)w;
        c.H = (int)h;
        c.Hash = hash;
        imgClips.Add(c);
        return c;
    }

    // Lê a imagem da área de transferência (formato DIB, o mesmo do Print Screen e do Win+Shift+S).
    // A área de transferência fica aberta só pelo tempo de copiar os dados: o histórico do Win+V também precisa dela.
    static Clip CaptureClipboardImage()
    {
        if (!gdipOK || !IsClipboardFormatAvailable(CF_DIB) || !OpenClip()) return null;
        byte[] raw = null;
        try
        {
            IntPtr h = GetClipboardData(CF_DIB);
            if (h != IntPtr.Zero)
            {
                ulong size = GlobalSize(h).ToUInt64();
                if (size >= 40 && size <= 200UL * 1024 * 1024)
                {
                    IntPtr p = GlobalLock(h);
                    if (p != IntPtr.Zero)
                    {
                        try
                        {
                            raw = new byte[(int)size];
                            Marshal.Copy(p, raw, 0, raw.Length);
                        }
                        finally { GlobalUnlock(h); }
                    }
                }
            }
        }
        catch (Exception e) { Log("Falha ao ler imagem copiada: " + e.Message); raw = null; }
        finally { CloseClipboard(); }
        if (raw == null) return null;
        try
        {
            int biSize = BitConverter.ToInt32(raw, 0), bpp = BitConverter.ToInt16(raw, 14);
            int comp = BitConverter.ToInt32(raw, 16), clrUsed = BitConverter.ToInt32(raw, 32);
            long off = biSize;
            if (comp == 3 && biSize == 40) off += 12; // máscaras de cor (BI_BITFIELDS)
            int colors = clrUsed;
            if (colors == 0 && bpp > 0 && bpp <= 8) colors = 1 << bpp;
            off += colors * 4L;
            if (off >= raw.Length) return null;
            GCHandle gh = GCHandle.Alloc(raw, GCHandleType.Pinned);
            try
            {
                IntPtr p = gh.AddrOfPinnedObject(), src;
                if (GdipCreateBitmapFromGdiDib(p, new IntPtr(p.ToInt64() + off), out src) != 0) return null;
                try { return ImageClipFrom(src, HashBytes(raw), false); }
                finally { GdipDisposeImage(src); }
            }
            finally { gh.Free(); }
        }
        catch (Exception e) { Log("Falha ao processar imagem copiada: " + e.Message); return null; }
    }

    static readonly string[] ImgExt = { ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".bmp", ".dib", ".gif", ".tif", ".tiff", ".ico" };

    static bool LooksLikeLink(string t)
    {
        string s = t.Trim().ToLowerInvariant();
        return s.Length > 0 && s.IndexOf('\n') < 0 && s.IndexOf(' ') < 0 &&
            (s.StartsWith("http://") || s.StartsWith("https://") || s.StartsWith("data:") || s.StartsWith("file:"));
    }

    // Arquivos de imagem copiados no Explorador (Ctrl+C sobre o arquivo).
    static List<string> ClipboardImageFiles()
    {
        List<string> files = new List<string>();
        if (!IsClipboardFormatAvailable(CF_HDROP) || !OpenClip()) return files;
        try
        {
            IntPtr h = GetClipboardData(CF_HDROP);
            if (h == IntPtr.Zero) return files;
            uint n = DragQueryFileW(h, 0xFFFFFFFF, null, 0);
            for (uint i = 0; i < n && i < 50; i++)
            {
                StringBuilder sb = new StringBuilder(1024);
                if (DragQueryFileW(h, i, sb, (uint)sb.Capacity) == 0) continue;
                string f = sb.ToString();
                if (Array.IndexOf(ImgExt, Path.GetExtension(f).ToLowerInvariant()) >= 0) files.Add(f);
            }
        }
        catch { }
        finally { CloseClipboard(); }
        return files;
    }

    static bool HasImageFiles() { return IsClipboardFormatAvailable(CF_HDROP) && ClipboardImageFiles().Count > 0; }

    static Clip LoadImageFileClip(string path)
    {
        try
        {
            FileInfo fi = new FileInfo(path);
            if (!fi.Exists || fi.Length > 100L * 1024 * 1024) return null;
            string hash = HashBytes(File.ReadAllBytes(path));
            IntPtr src;
            if (GdipCreateBitmapFromFile(path, out src) != 0) { Log("Não foi possível abrir a imagem " + Path.GetFileName(path)); return null; }
            try { return ImageClipFrom(src, hash, false); }
            finally { GdipDisposeImage(src); }
        }
        catch { return null; }
    }

    // Segundo caminho: o bitmap que o Windows gera a partir de qualquer imagem copiada.
    static Clip CaptureClipboardBitmap()
    {
        if (!gdipOK || !IsClipboardFormatAvailable(CF_BITMAP) || !OpenClip()) return null;
        try
        {
            IntPtr hbm = GetClipboardData(CF_BITMAP);
            if (hbm == IntPtr.Zero) return null;
            IntPtr src;
            if (GdipCreateBitmapFromHBITMAP(hbm, IntPtr.Zero, out src) != 0) return null;
            try { return ImageClipFrom(src, "b" + DateTime.Now.Ticks.ToString("x"), false); }
            finally { GdipDisposeImage(src); }
        }
        catch { return null; }
        finally { CloseClipboard(); }
    }

    static List<Clip> CaptureClipboardImages()
    {
        List<Clip> res = new List<Clip>();
        if (!gdipOK) return res;
        foreach (string f in ClipboardImageFiles())
        {
            Clip c = LoadImageFileClip(f);
            if (c != null) res.Add(c);
            if (res.Count >= 10) break;
        }
        if (res.Count > 0) return res;
        Clip img = CaptureClipboardImage();
        if (img == null) img = CaptureClipboardBitmap();
        if (img != null) res.Add(img);
        return res;
    }

    static uint fmtPng;

    // Imagem no formato DIB (24 bits, de baixo para cima), como o Print Screen entrega.
    static IntPtr MakeDibGlobal(IntPtr img, int w, int h)
    {
        GpRect r = new GpRect();
        r.Width = w;
        r.Height = h;
        GpBitmapData bd = new GpBitmapData();
        if (GdipBitmapLockBits(img, ref r, 1, 0x00021808, bd) != 0) return IntPtr.Zero; // leitura, 24 bits
        try
        {
            int row = (w * 3 + 3) & ~3;
            long total = 40L + (long)row * h;
            IntPtr hg = GlobalAlloc(2, new UIntPtr((ulong)total));
            if (hg == IntPtr.Zero) return IntPtr.Zero;
            IntPtr p = GlobalLock(hg);
            if (p == IntPtr.Zero) { GlobalFree(hg); return IntPtr.Zero; }
            byte[] hdr = new byte[40];
            BitConverter.GetBytes(40).CopyTo(hdr, 0);
            BitConverter.GetBytes(w).CopyTo(hdr, 4);
            BitConverter.GetBytes(h).CopyTo(hdr, 8);
            BitConverter.GetBytes((short)1).CopyTo(hdr, 12);
            BitConverter.GetBytes((short)24).CopyTo(hdr, 14);
            BitConverter.GetBytes(row * h).CopyTo(hdr, 20);
            Marshal.Copy(hdr, 0, p, 40);
            byte[] line = new byte[row];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride), line, 0, w * 3);
                Marshal.Copy(line, 0, new IntPtr(p.ToInt64() + 40 + (long)(h - 1 - y) * row), row);
            }
            GlobalUnlock(hg);
            return hg;
        }
        catch { return IntPtr.Zero; }
        finally { GdipBitmapUnlockBits(img, bd); }
    }

    // A mesma imagem em PNG (navegadores, WhatsApp, Office e outros preferem este formato).
    static IntPtr MakePngGlobal(IntPtr img)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "clipatalho_" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            Guid enc = PngEncoder;
            if (GdipSaveImageToFile(img, tmp, ref enc, IntPtr.Zero) != 0) return IntPtr.Zero;
            byte[] data = File.ReadAllBytes(tmp);
            IntPtr hg = GlobalAlloc(2, new UIntPtr((ulong)data.Length));
            if (hg == IntPtr.Zero) return IntPtr.Zero;
            IntPtr p = GlobalLock(hg);
            if (p == IntPtr.Zero) { GlobalFree(hg); return IntPtr.Zero; }
            Marshal.Copy(data, 0, p, data.Length);
            GlobalUnlock(hg);
            return hg;
        }
        catch { return IntPtr.Zero; }
        finally { try { File.Delete(tmp); } catch { } }
    }

    static bool SetClipboardImage(Clip c)
    {
        if (c.Image == IntPtr.Zero) return false;
        IntPtr dib = MakeDibGlobal(c.Image, c.W, c.H);
        if (dib == IntPtr.Zero) return false;
        IntPtr png = fmtPng != 0 ? MakePngGlobal(c.Image) : IntPtr.Zero;
        if (!OpenClip())
        {
            GlobalFree(dib);
            if (png != IntPtr.Zero) GlobalFree(png);
            return false;
        }
        try
        {
            EmptyClipboard();
            if (SetClipboardData(CF_DIB, dib) == IntPtr.Zero)
            {
                GlobalFree(dib);
                if (png != IntPtr.Zero) GlobalFree(png);
                return false;
            }
            if (png != IntPtr.Zero && SetClipboardData(fmtPng, png) == IntPtr.Zero) GlobalFree(png);
            if (fmtExclude != 0) // não entra no histórico do Win+V
            {
                IntPtr h2 = GlobalAlloc(2, new UIntPtr(4));
                if (h2 != IntPtr.Zero && SetClipboardData(fmtExclude, h2) == IntPtr.Zero) GlobalFree(h2);
            }
            return true;
        }
        finally { CloseClipboard(); }
    }

    // Grava a imagem como PNG na pasta "ClipAtalho imagens" (uma vez só).
    static bool EnsureImageFile(Clip c)
    {
        try
        {
            if (c.ImgFile == null) c.ImgFile = "img_" + c.Hash + ".png";
            string path = Path.Combine(ImgDir, c.ImgFile);
            if (File.Exists(path)) return true;
            if (c.Image == IntPtr.Zero) return false;
            Directory.CreateDirectory(ImgDir);
            Guid enc = PngEncoder;
            return GdipSaveImageToFile(c.Image, path, ref enc, IntPtr.Zero) == 0;
        }
        catch (Exception e) { Log("Falha ao gravar imagem: " + e.Message); return false; }
    }

    static Clip LoadImageClip(string name, bool pinned)
    {
        try
        {
            name = Path.GetFileName(name.Trim());
            string path = Path.Combine(ImgDir, name);
            if (name.Length == 0 || !File.Exists(path)) return null;
            string hash = name.ToLowerInvariant();
            if (hash.StartsWith("img_") && hash.EndsWith(".png")) hash = hash.Substring(4, hash.Length - 8);
            Clip c = null;
            IntPtr src;
            if (gdipOK && GdipCreateBitmapFromFile(path, out src) == 0)
            {
                try { c = ImageClipFrom(src, hash, pinned); } // cópia na memória: o arquivo fica livre
                finally { GdipDisposeImage(src); }
            }
            if (c == null) { c = new Clip("", pinned); c.Hash = hash; } // mantém a referência mesmo sem conseguir abrir
            c.ImgFile = name;
            return c;
        }
        catch { return null; }
    }

    // Apaga da pasta as imagens que não pertencem mais a nenhum clip salvo.
    static void CleanImageFolder(List<string> used)
    {
        try
        {
            if (!Directory.Exists(ImgDir)) return;
            foreach (string f in Directory.GetFiles(ImgDir, "img_*.png"))
                if (!used.Contains(Path.GetFileName(f).ToLowerInvariant())) File.Delete(f);
        }
        catch { }
    }

    static void DrawImageFit(IntPtr hdc, IntPtr img, int x, int y, int w, int h)
    {
        if (img == IntPtr.Zero || !gdipOK || w <= 0 || h <= 0) return;
        uint iw, ih;
        if (GdipGetImageWidth(img, out iw) != 0 || GdipGetImageHeight(img, out ih) != 0 || iw == 0 || ih == 0) return;
        double sc = Math.Min(1.0, Math.Min((double)w / iw, (double)h / ih));
        int dw = Math.Max(1, (int)(iw * sc)), dh = Math.Max(1, (int)(ih * sc));
        IntPtr g;
        if (GdipCreateFromHDC(hdc, out g) != 0) return;
        GdipSetInterpolationMode(g, 7); // bicúbica de alta qualidade
        GdipDrawImageRectI(g, img, x + (w - dw) / 2, y + (h - dh) / 2, dw, dh);
        GdipDeleteGraphics(g);
    }

    static string EscClip(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    static string UnescClip(string s)
    {
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[i + 1];
                if (n == 'n') { b.Append('\n'); i++; continue; }
                if (n == 'r') { b.Append('\r'); i++; continue; }
                if (n == 't') { b.Append('\t'); i++; continue; }
                if (n == '\\') { b.Append('\\'); i++; continue; }
            }
            b.Append(s[i]);
        }
        return b.ToString();
    }

    // Os clips ficam na seção [clips] do ClipAtalho.ini (um arquivo só para levar junto com o .exe).
    public static void SaveClips()
    {
        ReleaseUnusedImages();
        try
        {
            string text = ReadIni() ?? DefaultIni;
            List<string> keep = new List<string>();
            bool inClips = false;
            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string t = raw.Trim().Trim((char)0xFEFF).Trim();
                if (t.StartsWith("[") && t.EndsWith("]")) inClips = t.ToLowerInvariant() == "[clips]";
                if (!inClips) keep.Add(raw.TrimStart((char)0xFEFF));
            }
            while (keep.Count > 0 && keep[keep.Count - 1].Trim().Length == 0) keep.RemoveAt(keep.Count - 1);
            StringBuilder sb = new StringBuilder();
            foreach (string l in keep) sb.Append(l).Append("\r\n");
            sb.Append("\r\n[clips]\r\n");
            sb.Append("; Lista de clips, na ordem das posições. Seção gravada pelo programa:\r\n");
            sb.Append("; edite pela janela \"Gerenciar clips\" (alterações feitas aqui à mão podem ser sobrescritas).\r\n");
            List<string> used = new List<string>();
            foreach (Clip c in clips)
            {
                if (!c.Pinned && !cfgSaveAll) continue;
                if (c.IsImage)
                {
                    if (!EnsureImageFile(c)) continue;
                    sb.Append(c.Pinned ? "fixo_img=" : "normal_img=").Append(c.ImgFile).Append("\r\n");
                    used.Add(c.ImgFile.ToLowerInvariant());
                    continue;
                }
                if (c.Text.Length == 0) continue;
                sb.Append(c.Pinned ? "fixo=" : "normal=").Append(EscClip(c.Text)).Append("\r\n");
            }
            WriteIni(sb.ToString());
            CleanImageFolder(used);
        }
        catch (Exception e) { Log("FALHA ao salvar clips no .ini: " + e.Message); }
    }

    public static void LoadClips()
    {
        bool found = false;
        string text = ReadIni();
        if (text != null)
        {
            bool inClips = false;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r').TrimStart((char)0xFEFF);
                string t = line.Trim();
                if (t.StartsWith("[") && t.EndsWith("]"))
                {
                    inClips = t.ToLowerInvariant() == "[clips]";
                    if (inClips) found = true;
                    continue;
                }
                if (!inClips || t.Length == 0 || t[0] == ';') continue;
                int eq = line.IndexOf('=');
                if (eq < 1 || clips.Count >= MAX_CLIPS) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                if (key == "fixo_img" || key == "normal_img")
                {
                    Clip ic = LoadImageClip(line.Substring(eq + 1), key == "fixo_img");
                    if (ic != null) clips.Add(ic);
                    continue;
                }
                if (key != "fixo" && key != "normal") continue;
                clips.Add(new Clip(UnescClip(line.Substring(eq + 1)), key == "fixo"));
            }
        }
        if (found) return;

        // Migração da versão anterior (arquivo ClipAtalho.clips) para dentro do .ini
        string old = ReadTextFile(clipsPath);
        if (old == null) return;
        foreach (string raw in old.Split('\n'))
        {
            string line = raw.TrimEnd('\r').TrimStart((char)0xFEFF);
            if (line.Length < 3 || line[1] != '\t' || clips.Count >= MAX_CLIPS) continue;
            clips.Add(new Clip(UnescClip(line.Substring(2)), line[0] == 'F'));
        }
        SaveClips();
        try { File.Delete(clipsPath); } catch { }
        Log("Clips migrados de ClipAtalho.clips para o ClipAtalho.ini: " + clips.Count);
    }

    static string Preview(string s, int max)
    {
        s = s.Replace("\r\n", " ↵ ").Replace("\n", " ↵ ").Replace("\t", " ").Trim();
        if (s.Length > max) s = s.Substring(0, max) + "…";
        return s;
    }

    // ------------------------------------------------------------------ área de transferência
    static bool OpenClip()
    {
        for (int i = 0; i < 8; i++)
        {
            if (OpenClipboard(hwndMain)) return true;
            Thread.Sleep(8);
        }
        return false;
    }

    static string GetClipboardText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;
        if (!OpenClip()) return null;
        try
        {
            IntPtr h = GetClipboardData(CF_UNICODETEXT);
            if (h == IntPtr.Zero) return null;
            ulong size = GlobalSize(h).ToUInt64();
            if (size > 4UL * 1024 * 1024) return null; // ignora textos gigantes
            IntPtr p = GlobalLock(h);
            if (p == IntPtr.Zero) return null;
            try
            {
                string s = Marshal.PtrToStringUni(p, (int)(size / 2));
                int z = s.IndexOf('\0');
                return z >= 0 ? s.Substring(0, z) : s;
            }
            finally { GlobalUnlock(h); }
        }
        finally { CloseClipboard(); }
    }

    // Grava o texto marcando-o para não entrar no histórico do Windows (Win+V).
    static bool SetClipboardText(string s)
    {
        char[] chars = (s.Replace("\0", "") + "\0").ToCharArray();
        IntPtr h = GlobalAlloc(2, new UIntPtr((uint)(chars.Length * 2)));
        if (h == IntPtr.Zero) return false;
        IntPtr p = GlobalLock(h);
        if (p == IntPtr.Zero) { GlobalFree(h); return false; }
        Marshal.Copy(chars, 0, p, chars.Length);
        GlobalUnlock(h);
        if (!OpenClip()) { GlobalFree(h); return false; }
        try
        {
            EmptyClipboard();
            if (SetClipboardData(CF_UNICODETEXT, h) == IntPtr.Zero) { GlobalFree(h); return false; }
            if (fmtExclude != 0)
            {
                IntPtr h2 = GlobalAlloc(2, new UIntPtr(4));
                if (h2 != IntPtr.Zero && SetClipboardData(fmtExclude, h2) == IntPtr.Zero) GlobalFree(h2);
            }
            return true;
        }
        finally { CloseClipboard(); }
    }

    // Cada cópia é lida ~0,35 s depois, para não disputar a área de transferência com o histórico do Win+V.
    static void OnClipboardUpdate()
    {
        if (GetClipboardOwner() == hwndMain) return; // alteração feita por nós
        SetTimer(hwndMain, (IntPtr)TIMER_CLIPREAD, 350, IntPtr.Zero);
    }

    static void ProcessClipboard()
    {
        KillTimer(hwndMain, (IntPtr)TIMER_CLIPREAD);
        if (GetClipboardOwner() == hwndMain) return;                              // alteração feita por nós
        if (fmtExclude != 0 && IsClipboardFormatAvailable(fmtExclude)) return;    // ex.: gerenciador de senhas
        string t = GetClipboardText();
        bool hasImg = cfgImages && (IsClipboardFormatAvailable(CF_DIB) || IsClipboardFormatAvailable(CF_BITMAP) || HasImageFiles());
        bool onlyLink = t != null && LooksLikeLink(t); // ex.: navegador que copia a imagem junto com o endereço dela
        if (t != null && t.Trim().Length > 0 && !(hasImg && onlyLink)) AddCopied(t); // havendo texto, ele tem preferência (ex.: células do Excel)
        else if (hasImg)
        {
            List<Clip> imgs = CaptureClipboardImages();
            if (imgs.Count == 0) { Log("Imagem copiada não pôde ser lida"); return; }
            for (int i = imgs.Count - 1; i >= 0; i--) AddCopiedClip(imgs[i]); // a primeira fica no topo
        }
        else return;
        if (restoring) // usuário copiou algo novo antes da restauração: não sobrescrever
        {
            restoring = false;
            KillTimer(hwndMain, (IntPtr)TIMER_RESTORE);
        }
    }

    // ------------------------------------------------------------------ colagem
    static Clip pendImg;

    static void PasteClip(Clip c)
    {
        if (c == null) return;
        if (c.IsImage)
        {
            if (c.Image == IntPtr.Zero) return;
            StartPaste("", 0);
            pendImg = c;
        }
        else if (c.Text.Length > 0) StartPaste(c.Text, 0);
    }

    static void StartPaste(string text, int backspaces)
    {
        pendImg = null;
        pendText = text;
        pendBS = backspaces;
        pendDeadline = DateTime.UtcNow.AddSeconds(3);
        SetTimer(hwndMain, (IntPtr)TIMER_WAITMODS, 15, IntPtr.Zero);
    }

    static void OnWaitMods()
    {
        bool ctrl = KeyDown(VK_CONTROL);
        bool others = KeyDown(VK_SHIFT) || KeyDown(VK_MENU) || KeyDown(VK_LWIN) || KeyDown(VK_RWIN);
        if (others || (ctrl && pendBS > 0))
        {
            if (DateTime.UtcNow < pendDeadline) return;      // espera soltar as teclas
            KillTimer(hwndMain, (IntPtr)TIMER_WAITMODS);     // desistiu
            Log("Colar: cancelado (teclas modificadoras continuaram pressionadas por 3 s)");
            return;
        }
        KillTimer(hwndMain, (IntPtr)TIMER_WAITMODS);
        DoPaste(ctrl);
    }

    static INPUT KeyInput(int vk, bool up)
    {
        INPUT i = new INPUT();
        i.type = 1;
        i.u.ki.wVk = (ushort)vk;
        i.u.ki.dwFlags = up ? 2u : 0u;
        return i;
    }

    static void DoPaste(bool ctrlHeld)
    {
        if (!restoring) savedClip = GetClipboardText();
        string what = pendImg != null ? "imagem " + pendImg.W + "x" + pendImg.H : pendText.Length + " caracteres";
        if (!(pendImg != null ? SetClipboardImage(pendImg) : SetClipboardText(pendText)))
        {
            Log("Colar: FALHA ao gravar na área de transferência (" + what + ")");
            return;
        }
        List<INPUT> ins = new List<INPUT>();
        for (int i = 0; i < pendBS; i++) { ins.Add(KeyInput(VK_BACK, false)); ins.Add(KeyInput(VK_BACK, true)); }
        if (!ctrlHeld) ins.Add(KeyInput(VK_CONTROL, false)); // Ctrl já pressionado: basta o V
        ins.Add(KeyInput('V', false));
        ins.Add(KeyInput('V', true));
        if (!ctrlHeld) ins.Add(KeyInput(VK_CONTROL, true));
        INPUT[] arr = ins.ToArray();
        uint sent = SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT)));
        Log("Colar: " + what + ", Ctrl pressionado=" + ctrlHeld + ", teclas enviadas=" + sent + "/" + arr.Length +
            (sent == arr.Length ? "" : " ERRO " + Marshal.GetLastWin32Error()));
        if (savedClip != null)
        {
            restoring = true;
            SetTimer(hwndMain, (IntPtr)TIMER_RESTORE, pendImg != null ? 1500u : 700u, IntPtr.Zero);
        }
    }

    static void OnRestore()
    {
        KillTimer(hwndMain, (IntPtr)TIMER_RESTORE);
        if (restoring)
        {
            restoring = false;
            if (savedClip != null) SetClipboardText(savedClip);
        }
    }

    // ------------------------------------------------------------------ bandeja
    static void AddTray()
    {
        nid = new NOTIFYICONDATA();
        nid.cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA));
        nid.hWnd = hwndMain;
        nid.uID = 1;
        nid.uFlags = 1 | 2 | 4; // MESSAGE | ICON | TIP
        nid.uCallbackMessage = WM_TRAY;
        nid.hIcon = hAppIcon != IntPtr.Zero ? hAppIcon : LoadIconW(IntPtr.Zero, (IntPtr)32512);
        nid.szTip = "ClipAtalho";
        nid.szInfo = "";
        nid.szInfoTitle = "";
        trayReady = Shell_NotifyIconW(0, ref nid);
    }

    static string Fit(string s, int max) { return s.Length > max ? s.Substring(0, max) : s; }

    static void SetTip()
    {
        if (!trayReady) return;
        nid.uFlags = 4;
        nid.szTip = Fit(paused ? "ClipAtalho (pausado)"
            : "ClipAtalho - " + cfgModsLabel + "+1..9 cola, " + cfgModsLabel + "+0 lista. Clique: gerenciar", 127);
        Shell_NotifyIconW(1, ref nid);
    }

    static void Balloon(string title, string text)
    {
        if (!trayReady) return;
        nid.uFlags = 0x10; // NIF_INFO
        nid.szInfoTitle = Fit(title, 63);
        nid.szInfo = Fit(text, 255);
        nid.dwInfoFlags = 1;
        Shell_NotifyIconW(1, ref nid);
    }

    static void TrayMenu()
    {
        List<MI> mi = new List<MI>();
        mi.Add(Item(CMD_MANAGE, "Abrir o ClipAtalho (gerenciar clips)"));
        mi.Add(Item(CMD_SHOW, "Lista rápida  (" + cfgModsLabel + "+0)"));
        mi.Add(Item(CMD_EDIT, "Configurações"));
        mi.Add(Item(CMD_HELP, "Ajuda"));
        mi.Add(Sep());
        MI pz = Item(CMD_PAUSE, "Pausar");
        pz.Check = paused;
        mi.Add(pz);
        mi.Add(Item(CMD_CLEAR, "Limpar clips não fixados"));
        mi.Add(Item(CMD_DIR, "Abrir pasta do programa"));
        mi.Add(Sep());
        mi.Add(Item(CMD_EXIT, "Sair"));
        POINT pt;
        GetCursorPos(out pt);
        int cmd = ShowMenu(mi, pt.X, pt.Y, IntPtr.Zero);
        switch (cmd)
        {
            case CMD_MANAGE: OpenManager(0); break;
            case CMD_EDIT: OpenManager(2); break;
            case CMD_HELP: OpenManager(3); break;
            case CMD_SHOW: ShowHistory(); break;
            case CMD_DIR:
                try { Process.Start("explorer.exe", "\"" + exeDir + "\""); } catch { }
                break;
            case CMD_PAUSE:
                paused = !paused;
                HidePopup();
                SetTip();
                RepaintAll();
                break;
            case CMD_CLEAR:
                ClearUnpinned();
                break;
            case CMD_EXIT:
                if (hwndMgr != IntPtr.Zero && IsWindowVisible(hwndMgr) && !ResolveDirty(true)) break;
                ResolveHold(true);
                Shell_NotifyIconW(2, ref nid);
                PostQuitMessage(0);
                break;
        }
    }

    // ------------------------------------------------------------------ menus desenhados pelo programa
    class MI { public int Id; public string Text; public bool Check, Off, Sep; }
    static MI Item(int id, string text) { MI m = new MI(); m.Id = id; m.Text = text; return m; }
    static MI Sep() { MI m = new MI(); m.Sep = true; m.Text = ""; return m; }
    static List<MI> menuItems = new List<MI>();
    static IntPtr hMenuWnd;
    static int menuHover = -1, menuResult;
    static bool menuDone;
    const int MENU_ROW = 32, MENU_SEP = 9, MENU_PAD = 6;

    static int MenuItemTop(int i)
    {
        int y = Px(MENU_PAD);
        for (int k = 0; k < i && k < menuItems.Count; k++) y += Px(menuItems[k].Sep ? MENU_SEP : MENU_ROW);
        return y;
    }

    static int MenuItemAt(int y)
    {
        int top = Px(MENU_PAD);
        for (int k = 0; k < menuItems.Count; k++)
        {
            int hh = Px(menuItems[k].Sep ? MENU_SEP : MENU_ROW);
            if (y >= top && y < top + hh) return (menuItems[k].Sep || menuItems[k].Off) ? -1 : k;
            top += hh;
        }
        return -1;
    }

    // Menu no padrão da janela principal (tema, cantos arredondados, sombra e fade). Devolve o Id escolhido ou 0.
    static int ShowMenu(List<MI> items, int x, int y, IntPtr returnTo)
    {
        if (hMenuWnd != IntPtr.Zero || items.Count == 0) return 0;
        menuItems = items;
        menuHover = -1;
        menuResult = 0;
        menuDone = false;
        int maxW = 0;
        IntPtr dc = GetDC(IntPtr.Zero);
        IntPtr oldF = SelectObject(dc, hUiFont);
        foreach (MI it in items)
        {
            if (it.Sep) continue;
            SIZE sz;
            if (GetTextExtentPoint32W(dc, it.Text, it.Text.Length, out sz)) maxW = Math.Max(maxW, sz.cx);
        }
        SelectObject(dc, oldF);
        ReleaseDC(IntPtr.Zero, dc);
        int w = maxW + Px(64), h = MenuItemTop(items.Count) + Px(MENU_PAD);
        POINT at = new POINT();
        at.X = x;
        at.Y = y;
        MONITORINFO mon = new MONITORINFO();
        mon.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        if (GetMonitorInfoW(MonitorFromPoint(at, 2), ref mon))
        {
            if (x + w > mon.rcWork.Right) x = mon.rcWork.Right - w;
            if (y + h > mon.rcWork.Bottom) y = Math.Max(mon.rcWork.Top, y - h); // abre para cima se não couber
            if (x < mon.rcWork.Left) x = mon.rcWork.Left;
            if (y < mon.rcWork.Top) y = mon.rcWork.Top;
        }
        hMenuWnd = CreateWindowExW(0x80 | 0x8 | 0x80000, "ClipAtalhoMenu", "", 0x80000000, x, y, w, h,
            IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
        if (hMenuWnd == IntPtr.Zero) return 0;
        IntPtr hm = hMenuWnd;
        RoundWindow(hm, w, h);
        AttachShade(hm, NewShadeWindow(true));
        FadeWindow(hm, true);
        ShowWindow(hm, 5);
        PlaceShade(hm);
        SetForegroundWindow(hm);
        MSG msg;
        while (!menuDone)
        {
            if (GetMessageW(out msg, IntPtr.Zero, 0, 0) <= 0) { PostQuitMessage(0); break; }
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        FadeOutAndWait(hm);
        DropShade(hm);
        winAlpha.Remove(hm);
        DestroyWindow(hm);
        hMenuWnd = IntPtr.Zero;
        if (returnTo != IntPtr.Zero && IsWindowVisible(returnTo)) SetForegroundWindow(returnTo);
        return menuResult;
    }

    static void MenuStep(int dir, IntPtr hwnd)
    {
        int n = menuItems.Count, i = menuHover;
        for (int k = 0; k < n; k++)
        {
            i = ((i < 0 ? (dir > 0 ? -1 : 0) : i) + dir + n) % n;
            if (!menuItems[i].Sep && !menuItems[i].Off) { menuHover = i; break; }
        }
        InvalidateRect(hwnd, IntPtr.Zero, false);
    }

    static void PaintMenu(IntPtr hwnd)
    {
        PAINTSTRUCT ps;
        IntPtr hdc = BeginPaint(hwnd, out ps);
        RECT rc;
        GetClientRect(hwnd, out rc);
        int w = rc.Right, h = rc.Bottom;
        IntPtr mem = CreateCompatibleDC(hdc), bmp = CreateCompatibleBitmap(hdc, w, h), old = SelectObject(mem, bmp);
        FillRect(mem, ref rc, hWhiteBrush);
        RoundRect(mem, 0, 0, w, h, Px(WIN_R), 0, A_BTNBORDER);
        IntPtr line = CreateSolidBrush(C_LINE);
        for (int i = 0; i < menuItems.Count; i++)
        {
            MI it = menuItems[i];
            int top = MenuItemTop(i);
            if (it.Sep)
            {
                RECT ln = new RECT();
                ln.Left = Px(14);
                ln.Right = w - Px(14);
                ln.Top = top + Px(MENU_SEP) / 2;
                ln.Bottom = ln.Top + 1;
                FillRect(mem, ref ln, line);
                continue;
            }
            int bottom = top + Px(MENU_ROW);
            if (i == menuHover) RoundRect(mem, Px(6), top + Px(2), w - Px(12), Px(MENU_ROW) - Px(4), Px(8), A_SEL, 0);
            if (it.Check) DrawTextAt(mem, "✓", hUiBold, C_ACCENT, Px(10), top, Px(34), bottom, 0x25);
            DrawTextAt(mem, it.Text, hUiFont, it.Off ? C_GRAY : C_TEXT, Px(36), top, w - Px(16), bottom, 0x8024);
        }
        DeleteObject(line);
        BitBlt(hdc, 0, 0, w, h, mem, 0, 0, 0x00CC0020);
        SelectObject(mem, old);
        DeleteObject(bmp);
        DeleteDC(mem);
        EndPaint(hwnd, ref ps);
    }

    static IntPtr MenuProc(IntPtr hwnd, uint m, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (m)
            {
                case 0x0014: // WM_ERASEBKGND: tudo é pintado em WM_PAINT
                    return (IntPtr)1;
                case WM_PAINT:
                    PaintMenu(hwnd);
                    return IntPtr.Zero;
                case 0x0200: // WM_MOUSEMOVE
                {
                    int i = MenuItemAt((short)((lParam.ToInt64() >> 16) & 0xFFFF));
                    if (i != menuHover) { menuHover = i; InvalidateRect(hwnd, IntPtr.Zero, false); }
                    return IntPtr.Zero;
                }
                case WM_LBUTTONUP:
                {
                    int i = MenuItemAt((short)((lParam.ToInt64() >> 16) & 0xFFFF));
                    if (i >= 0) { menuResult = menuItems[i].Id; menuDone = true; }
                    return IntPtr.Zero;
                }
                case WM_KEYDOWN:
                {
                    int vk = (int)wParam.ToInt64();
                    if (vk == VK_ESCAPE) menuDone = true;
                    else if (vk == 0x26 || vk == 0x28) MenuStep(vk == 0x28 ? 1 : -1, hwnd);
                    else if (vk == 0x0D && menuHover >= 0) { menuResult = menuItems[menuHover].Id; menuDone = true; }
                    return IntPtr.Zero;
                }
                case 0x0006: // WM_ACTIVATE: clicou fora do menu
                    if ((wParam.ToInt64() & 0xFFFF) == 0) menuDone = true;
                    break;
            }
        }
        catch { }
        return DefWindowProcW(hwnd, m, wParam, lParam);
    }

    // Janela de sombra: só desenho, ignora o mouse.
    static IntPtr ShadeProc(IntPtr hwnd, uint m, IntPtr wParam, IntPtr lParam)
    {
        if (m == 0x0084) return (IntPtr)(-1); // HTTRANSPARENT
        return DefWindowProcW(hwnd, m, wParam, lParam);
    }

    // ------------------------------------------------------------------ janela de sugestão / lista
    static List<Clip> popClips = new List<Clip>(); // clip de cada linha da lista rápida
    static bool popNumbered, popDrag, popHoverTrash, popClearArmed;
    static Clip popTrashArmed;
    static POINT popDragStart;
    static RECT popDragRect;
    static float[] popScale = new float[0]; // 0 = tamanho normal, 1 = ampliado (efeito jukebox)
    static IntPtr[] popFonts = new IntPtr[4];
    static IntPtr popMem, popMemBmp, popMemOld, hIconFont;
    static int popMemW, popMemH;
    const int POP_ROW = 30;

    static int PopupHeight() { return Px(16) + popLines.Count * Px(POP_ROW); }

    static void BuildHistoryLines()
    {
        popLines = new List<string>();
        popClips = new List<Clip>();
        if (clips.Count == 0)
        {
            popLines.Add("Lista vazia — copie algo (Ctrl+C) primeiro.");
            popLines.Add("Esc fecha");
            popNumbered = false;
        }
        else
        {
            popLines.Add("Clique para colar  ·  1–9 cola  ·  arraste aqui para mover");
            for (int i = 0; i < clips.Count; i++) // todos os clips, fixados ou não
            {
                Clip c = clips[i];
                popClips.Add(c);
                popLines.Add((c.Pinned ? "★ " : "") + (c.IsImage ? "Imagem  ·  " + c.W + " × " + c.H + " px" : Preview(c.Text, 140)));
            }
            popNumbered = true;
        }
        popScale = new float[popLines.Count];
    }

    static void ShowHistory()
    {
        BuildHistoryLines();
        popMode = MODE_HISTORY;
        ShowPopup(20000);
    }

    // Lista rápida aberta e a lista de clips mudou (lixeira, limpar, autoexclusão): refaz sem mudar de lugar.
    static void RefreshPopupList()
    {
        if (popMode != MODE_HISTORY || hwndPopup == IntPtr.Zero) return;
        BuildHistoryLines();
        popHover = -1;
        popHoverTrash = false;
        RECT r;
        GetWindowRect(hwndPopup, out r);
        int w = Px(540), h = PopupHeight();
        SetWindowPos(hwndPopup, IntPtr.Zero, r.Left, r.Top, w, h, 0x4 | 0x10);
        RoundWindow(hwndPopup, w, h);
        PlaceShade(hwndPopup);
        InvalidateRect(hwndPopup, IntPtr.Zero, false);
    }

    static void InvalidatePopRow(int i)
    {
        if (i < 0 || i >= popLines.Count) return;
        RECT r = new RECT();
        r.Left = 0;
        r.Right = Px(540);
        r.Top = Px(8) + i * Px(POP_ROW) - Px(2);
        r.Bottom = r.Top + Px(POP_ROW) + Px(4);
        InvalidateRectArea(hwndPopup, ref r, false);
    }

    // Efeito jukebox: o item sob o cursor cresce aos poucos e o anterior volta aos poucos (uma "onda").
    static void OnWaveTimer()
    {
        bool moving = false;
        for (int i = 1; i < popScale.Length; i++)
        {
            float target = (i == popHover && popNumbered) ? 1f : 0f, v = popScale[i];
            if (v == target) continue;
            if (target == 0f) { popScale[i] = 0f; InvalidatePopRow(i); continue; } // o anterior volta na hora
            v += (target - v) * 0.35f;
            if (Math.Abs(target - v) < 0.03f) v = target;
            popScale[i] = v;
            InvalidatePopRow(i);
            if (v != target) moving = true;
        }
        if (!moving) KillTimer(hwndMain, (IntPtr)TIMER_WAVE);
    }

    // ------------------------------------------------------------------ desenho (cantos arredondados com GDI+)
    static void FillRound(IntPtr g, float x, float y, float w, float h, float r, uint fill, uint border)
    {
        if (r * 2 > w) r = w / 2;
        if (r * 2 > h) r = h / 2;
        float d = r * 2;
        IntPtr path;
        if (GdipCreatePath(0, out path) != 0) return;
        GdipAddPathArc(path, x, y, d, d, 180, 90);
        GdipAddPathArc(path, x + w - d, y, d, d, 270, 90);
        GdipAddPathArc(path, x + w - d, y + h - d, d, d, 0, 90);
        GdipAddPathArc(path, x, y + h - d, d, d, 90, 90);
        GdipClosePathFigure(path);
        IntPtr brush;
        if (GdipCreateSolidFill(fill, out brush) == 0)
        {
            GdipFillPath(g, brush, path);
            GdipDeleteBrush(brush);
        }
        IntPtr pen;
        if (border != 0 && GdipCreatePen1(border, 1f, 2, out pen) == 0)
        {
            GdipDrawPath(g, pen, path);
            GdipDeletePen(pen);
        }
        GdipDeletePath(path);
    }

    static void RoundRect(IntPtr hdc, int x, int y, int w, int h, int r, uint fill, uint border)
    {
        IntPtr g;
        if (!gdipOK || GdipCreateFromHDC(hdc, out g) != 0) return;
        GdipSetSmoothingMode(g, 4); // anti-serrilhado
        FillRound(g, x, y, w - 1, h - 1, r, fill, border);
        GdipDeleteGraphics(g);
    }

    static void DrawTextAt(IntPtr hdc, string text, IntPtr font, uint color, int l, int t, int r, int b, uint format)
    {
        RECT rc = new RECT();
        rc.Left = l;
        rc.Top = t;
        rc.Right = r;
        rc.Bottom = b;
        SelectObject(hdc, font);
        SetTextColor(hdc, color);
        SetBkMode(hdc, 1);
        DrawTextW(hdc, text, -1, ref rc, format | 0x800);
    }

    // Ícone do programa desenhado em tamanho grande (tela Ajuda).
    static void DrawAppIcon(IntPtr hdc, int x, int y, int size)
    {
        IntPtr g;
        if (!gdipOK || GdipCreateFromHDC(hdc, out g) != 0) return;
        GdipSetSmoothingMode(g, 4);
        FillRound(g, x, y, size, size, size * 0.24f, A_ACCENT, 0);
        float bh = size * 0.11f;
        for (int k = 0; k < 3; k++)
            FillRound(g, x + size * 0.25f, y + size * (0.26f + 0.19f * k), size * (k == 2 ? 0.32f : 0.5f), bh, bh / 2, 0xFFFFFFFF, 0);
        GdipDeleteGraphics(g);
    }

    // Ícone do programa: quadrado arredondado verde com três linhas (uma "lista").
    static IntPtr MakeIcon(int size)
    {
        IntPtr bmp, g, icon;
        if (!gdipOK || GdipCreateBitmapFromScan0(size, size, 0, 0x26200A, IntPtr.Zero, out bmp) != 0) return IntPtr.Zero;
        if (GdipGetImageGraphicsContext(bmp, out g) != 0) { GdipDisposeImage(bmp); return IntPtr.Zero; }
        GdipSetSmoothingMode(g, 4);
        FillRound(g, 1, 1, size - 2, size - 2, size * 0.24f, A_ACCENT, 0);
        float bh = size * 0.11f;
        for (int k = 0; k < 3; k++)
            FillRound(g, size * 0.25f, size * (0.26f + 0.19f * k), size * (k == 2 ? 0.32f : 0.5f), bh, bh / 2, 0xFFFFFFFF, 0);
        GdipDeleteGraphics(g);
        if (GdipCreateHICONFromBitmap(bmp, out icon) != 0) icon = IntPtr.Zero;
        GdipDisposeImage(bmp);
        return icon;
    }

    // ------------------------------------------------------------------ janela principal (Clips / Configurações / Ajuda)
    const int IDC_SAVE = 107, IDC_CLEAR = 108, IDC_NAV = 110, IDC_MOD = 120, IDC_T_STARTUP = 124, IDC_T_SAVEALL = 125,
        IDC_T_EXPAND = 126, IDC_RESTORE = 127, IDC_OPENINI = 128, IDC_OPENDIR = 129, IDC_HELPTEXT = 130, IDC_INSTA = 131, IDC_DLG = 200,
        IDC_XLIST = 140, IDC_XABBR = 141, IDC_XTEXT = 142, IDC_XSAVE = 143, IDC_XNEW = 144, IDC_XDEL = 145,
        IDC_XUP = 146, IDC_XDOWN = 147, IDC_XPIN = 148, IDC_XCLEAR = 149, IDC_THEME = 150, IDC_COLOR = 152, IDC_DELPIN = 160, IDC_T_IMAGES = 161, IDC_XSAVENEW = 162, IDC_XSORT = 163, IDC_CLOSE = 164,
        IDC_SEG = 180, IDC_BK_CLIPS = 182, IDC_BK_X = 183, IDC_BK_ALL = 184, IDC_RESTOREBK = 185, IDC_AUTODEL = 186,
        IDC_T_DATADIR = 187, IDC_DATADIR = 188, IDC_CONFLICT = 189, IDC_FACTORY = 190, IDC_FEEDBACK = 191,
        IDC_UNDO = 192, IDC_REDO = 193, IDC_XUNDO = 194, IDC_XREDO = 195,
        IDC_FB_SUG = 210, IDC_FB_REC = 211, IDC_FB_MAIL = 212, IDC_FB_MSG = 213;
    const string RUNKEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string INSTAGRAM = "https://www.instagram.com/talespsg";
    const string VERSION = "1.0.0 15-11-2026";
    static readonly string[] PageTitles = { "Seus clips", "Atalhos por Expansão", "Configurações", "Ajuda", "Configurações" };
    static readonly string[] ModNames = { "Ctrl", "Shift", "Alt", "Win" };
    static readonly uint[] ModBits = { 2, 4, 1, 8 };

    static int page;
    static List<IntPtr> ctrlHandles = new List<IntPtr>();
    static List<int> ctrlPages = new List<int>();
    static bool editDirty, startupOn, dragging;
    static int dragFrom = -1, dragTarget = -1, dragStartY;
    static WndProcDelegate listProcRef, dlgProcRef;
    static IntPtr oldListProc, hDlg, hHelp, hAccentBrush, hHeadFont;
    static int dlgResult, dlgEscResult = 2, dlgPrimary = 3;
    static bool dlgFeedback, fbComplaint;
    static IntPtr hFbMail, hFbMsg;
    static bool dlgWarn; // mostra o ícone de alerta e destaca o 1º botão em vermelho
    static string dlgMessage = "";
    static string[] dlgLabels = new string[3];

    static IntPtr Child(IntPtr parent, string cls, string text, uint style, int x, int y, int w, int h, int id, int pg)
    {
        IntPtr c = CreateWindowExW(0, cls, text, 0x40000000 | 0x10000000 | style,
            Px(x), Px(y), Px(w), Px(h), parent, (IntPtr)id, hInst, IntPtr.Zero);
        SendMessageW(c, 0x30, hUiFont, (IntPtr)1); // WM_SETFONT
        if (pg >= -1) { ctrlHandles.Add(c); ctrlPages.Add(pg); }
        return c;
    }

    static IntPtr Btn(int x, int y, int w, int h, int id, int pg)
    {
        return Child(hwndMgr, "BUTTON", "", 0x10000 | 0xB, x, y, w, h, id, pg); // BS_OWNERDRAW
    }

    static void CreateManager()
    {
        // Janela com moldura do Windows (sombra, arraste, Alt+Tab), mas sem a barra de título:
        // a área do programa ocupa a janela inteira (ver WM_NCCALCSIZE) e os cantos são arredondados.
        const uint style = 0x00CA0000 | 0x02000000;
        int w = Px(820), h = Px(540);
        int x = Math.Max(0, (GetSystemMetrics(0) - w) / 2), y = Math.Max(0, (GetSystemMetrics(1) - h) / 2);
        // A janela da sombra é a "dona" da janela principal: assim fica sempre logo atrás dela.
        IntPtr mgrShade = NewShadeWindow(false);
        hwndMgr = CreateWindowExW(0x10000 | 0x40000, "ClipAtalhoManager", "ClipAtalho", style,
            x, y, w, h, mgrShade, IntPtr.Zero, hInst, IntPtr.Zero);
        if (hwndMgr == IntPtr.Zero) return;
        AttachShade(hwndMgr, mgrShade);
        RoundWindow(hwndMgr, w, h);
        SetWindowPos(hwndMgr, IntPtr.Zero, 0, 0, 0, 0, 0x1 | 0x2 | 0x4 | 0x20); // aplica o novo tamanho de moldura
        if (hAppIcon != IntPtr.Zero)
        {
            SendMessageW(hwndMgr, 0x80, (IntPtr)0, hAppIcon); // WM_SETICON
            SendMessageW(hwndMgr, 0x80, (IntPtr)1, hAppIcon);
        }
        ApplyWindowTheme(hwndMgr, C_BG);

        // navegação (sempre visível)
        Btn(342, 14, 62, 32, IDC_NAV, -1);
        Btn(410, 14, 160, 32, IDC_NAV + 1, -1);
        Btn(576, 14, 116, 32, IDC_NAV + 2, -1);
        Btn(698, 14, 62, 32, IDC_NAV + 3, -1);
        Btn(772, 14, 32, 32, IDC_CLOSE, -1); // fechar (X)

        // página 0: clips
        hList = Child(hwndMgr, "LISTBOX", "", 0x00200000 | 0x10000 | 0x1 | 0x10 | 0x40 | 0x100, 16, 78, 336, 400, IDC_LIST, 0);
        SendMessageW(hList, 0x01A0, IntPtr.Zero, (IntPtr)Px(58)); // LB_SETITEMHEIGHT
        listProcRef = ListProc; // intercepta o mouse da lista (arrastar e botão direito)
        IntPtr fp = Marshal.GetFunctionPointerForDelegate(listProcRef);
        oldListProc = IntPtr.Size == 8 ? SetWindowLongPtr64(hList, -4, fp) : (IntPtr)SetWindowLong32(hList, -4, fp.ToInt32());
        hEdit = Child(hwndMgr, "EDIT", "", 0x00200000 | 0x10000 | 0x4 | 0x40 | 0x1000, 376, 112, 414, 354, IDC_EDIT, 0);
        Btn(16, 486, 34, 36, IDC_UP, 0);
        Btn(54, 486, 34, 36, IDC_DOWN, 0);
        Btn(92, 486, 136, 36, IDC_CLEAR, 0);
        Btn(232, 486, 120, 36, IDC_DELPIN, 0);
        Btn(362, 486, 124, 36, IDC_SAVE, 0);
        Btn(494, 486, 98, 36, IDC_NEW, 0);
        Btn(600, 486, 98, 36, IDC_PIN, 0);
        Btn(706, 486, 98, 36, IDC_DEL, 0);

        // página 1: atalhos por expansão
        hXList = Child(hwndMgr, "LISTBOX", "", 0x00200000 | 0x10000 | 0x1 | 0x10 | 0x40 | 0x100, 16, 114, 336, 364, IDC_XLIST, 1);
        Btn(16, 78, 336, 30, IDC_XSORT, 1);
        SendMessageW(hXList, 0x01A0, IntPtr.Zero, (IntPtr)Px(58)); // LB_SETITEMHEIGHT
        xListProcRef = XListProc;
        IntPtr xfp = Marshal.GetFunctionPointerForDelegate(xListProcRef);
        oldXListProc = IntPtr.Size == 8 ? SetWindowLongPtr64(hXList, -4, xfp) : (IntPtr)SetWindowLong32(hXList, -4, xfp.ToInt32());
        hXAbbr = Child(hwndMgr, "EDIT", "", 0x10000 | 0x10 | 0x80, 378, 114, 196, 20, IDC_XABBR, 1); // minúsculas, uma linha
        SendMessageW(hXAbbr, 0x00C5, (IntPtr)32, IntPtr.Zero); // EM_SETLIMITTEXT
        hXText = Child(hwndMgr, "EDIT", "", 0x00200000 | 0x10000 | 0x4 | 0x40 | 0x1000, 378, 180, 410, 282, IDC_XTEXT, 1);
        Btn(16, 486, 40, 36, IDC_XUP, 1);
        Btn(62, 486, 40, 36, IDC_XDOWN, 1);
        Btn(108, 486, 244, 36, IDC_XCLEAR, 1);
        Btn(362, 486, 80, 36, IDC_XSAVE, 1);
        Btn(450, 486, 100, 36, IDC_XSAVENEW, 1);
        Btn(558, 486, 72, 36, IDC_XNEW, 1);
        Btn(638, 486, 84, 36, IDC_XPIN, 1);
        Btn(730, 486, 74, 36, IDC_XDEL, 1);

        // página 2: configurações
        for (int i = 0; i < 4; i++) Btn(36 + i * 84, 134, 76, 30, IDC_MOD + i, 2);
        Btn(36, 198, 76, 30, IDC_THEME, 2);
        Btn(120, 198, 76, 30, IDC_THEME + 1, 2);
        for (int i = 0; i < ColorRgb.Length; i++) Btn(322 + i * 38, 198, 30, 30, IDC_COLOR + i, 2);
        Btn(36, 256, 740, 30, IDC_T_STARTUP, 2);
        Btn(36, 288, 740, 30, IDC_T_SAVEALL, 2);
        Btn(36, 320, 740, 30, IDC_T_EXPAND, 2);
        Btn(36, 352, 740, 30, IDC_T_IMAGES, 2);
        Btn(36, 416, 170, 34, IDC_OPENINI, 2);
        Btn(214, 416, 210, 34, IDC_OPENDIR, 2);
        Btn(556, 46, 120, 28, IDC_SEG, 24);      // Configurações: "Geral" | "Dados e backup"
        Btn(682, 46, 122, 28, IDC_SEG + 1, 24);

        // página 4: dados e backup (sub-página de Configurações)
        Btn(36, 132, 176, 32, IDC_BK_CLIPS, 4);
        Btn(218, 132, 196, 32, IDC_BK_X, 4);
        Btn(420, 132, 160, 32, IDC_BK_ALL, 4);
        Btn(586, 132, 190, 32, IDC_RESTOREBK, 4);
        Btn(540, 182, 236, 32, IDC_AUTODEL, 4);
        Btn(36, 252, 740, 30, IDC_T_DATADIR, 4);
        Btn(36, 308, 150, 32, IDC_DATADIR, 4);
        Btn(414, 308, 362, 32, IDC_CONFLICT, 4);
        Btn(36, 398, 260, 34, IDC_FACTORY, 4);

        // desfazer / refazer nos cartões de edição
        Btn(734, 82, 28, 24, IDC_UNDO, 0);
        Btn(766, 82, 28, 24, IDC_REDO, 0);
        Btn(734, 82, 28, 24, IDC_XUNDO, 1);
        Btn(766, 82, 28, 24, IDC_XREDO, 1);

        // página 3: ajuda
        Btn(738, 88, 44, 44, IDC_INSTA, 3); // botão só com ícone
        Btn(500, 96, 226, 36, IDC_FEEDBACK, 3);
        hHelp = Child(hwndMgr, "EDIT", "", 0x00200000 | 0x4 | 0x40 | 0x800, 34, 186, 756, 324, IDC_HELPTEXT, 3);
    }

    // ---- tema e cores
    static uint Ref(uint rgb) { return ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF); }

    static uint Mix(uint a, uint b, int pct) // pct% de b sobre a
    {
        uint r = 0;
        for (int sh = 0; sh <= 16; sh += 8)
        {
            uint ca = (a >> sh) & 0xFF, cb = (b >> sh) & 0xFF;
            r |= (uint)((ca * (100 - pct) + cb * pct) / 100) << sh;
        }
        return r;
    }

    static void ApplyWindowTheme(IntPtr hwnd, uint captionRef)
    {
        try
        {
            int dark = cfgDark ? 1 : 0, caption = (int)captionRef; // barra de título no tema (Windows 11)
            DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
            DwmSetWindowAttribute(hwnd, 35, ref caption, 4);
        }
        catch { }
    }

    static void ApplyTheme()
    {
        uint accent = ColorRgb[cfgColor];
        uint text, gray, bg, card, border, btnBorder, down, red;
        if (cfgDark) { text = 0xF3F4F6; gray = 0x9CA3AF; bg = 0x1B1D22; card = 0x272B33; border = 0x383D48; btnBorder = 0x4B515E; down = 0x3A404C; red = 0xF87171; }
        else { text = 0x111827; gray = 0x6B7280; bg = 0xF3F4F6; card = 0xFFFFFF; border = 0xE5E7EB; btnBorder = 0xD1D5DB; down = 0xE5E7EB; red = 0xDC2626; }
        C_TEXT = Ref(text);
        C_GRAY = Ref(gray);
        C_RED = Ref(red);
        C_BG = Ref(bg);
        C_CARD = Ref(card);
        C_LINE = Ref(btnBorder);
        C_ACCENT = Ref(cfgDark ? Mix(accent, 0xFFFFFF, 45) : accent); // texto na cor de destaque (mais claro no escuro)
        A_TEXT = 0xFF000000 | text;
        A_WHITE = 0xFF000000 | card;
        A_BORDER = 0xFF000000 | border;
        A_BTNBORDER = 0xFF000000 | btnBorder;
        A_DOWN = 0xFF000000 | down;
        A_BG = 0xFF000000 | bg;
        A_ACCENT = 0xFF000000 | accent;
        A_ACCENT_DOWN = 0xFF000000 | Mix(accent, 0x000000, 22);
        A_SEL = 0xFF000000 | Mix(card, accent, cfgDark ? 30 : 12);
        appliedTheme = cfgColor * 2 + (cfgDark ? 1 : 0);

        if (hBgBrush != IntPtr.Zero) DeleteObject(hBgBrush);
        if (hWhiteBrush != IntPtr.Zero) DeleteObject(hWhiteBrush);
        if (hAccentBrush != IntPtr.Zero) DeleteObject(hAccentBrush);
        hBgBrush = CreateSolidBrush(C_BG);
        hWhiteBrush = CreateSolidBrush(C_CARD); // pincel dos cartões
        hAccentBrush = CreateSolidBrush(Ref(accent));

        IntPtr oldIcon = hAppIcon;
        hAppIcon = MakeIcon(32);
        if (trayReady && hAppIcon != IntPtr.Zero)
        {
            nid.uFlags = 2; // NIF_ICON
            nid.hIcon = hAppIcon;
            Shell_NotifyIconW(1, ref nid);
        }
        if (hwndMgr != IntPtr.Zero)
        {
            if (hAppIcon != IntPtr.Zero)
            {
                SendMessageW(hwndMgr, 0x80, (IntPtr)0, hAppIcon);
                SendMessageW(hwndMgr, 0x80, (IntPtr)1, hAppIcon);
            }
            ApplyWindowTheme(hwndMgr, C_BG);
        }
        if (oldIcon != IntPtr.Zero && oldIcon != hAppIcon) DestroyIcon(oldIcon);
        RepaintAll();
        if (hwndPopup != IntPtr.Zero) InvalidateRect(hwndPopup, IntPtr.Zero, true);
        if (hwndMgr != IntPtr.Zero && IsWindowVisible(hwndMgr)) PlaceShade(hwndMgr); // sombra na intensidade do tema
    }

    // ------------------------------------------------------------------ cantos, sombra e fade
    const int WIN_R = 16; // raio dos cantos de todas as janelas (px a 100%)
    class Shade { public IntPtr Hwnd, Dc, Bmp, Old; public int W, H, Theme = -1; public byte Alpha = 255; }
    static Dictionary<IntPtr, Shade> shades = new Dictionary<IntPtr, Shade>();
    static Dictionary<IntPtr, int> winAlpha = new Dictionary<IntPtr, int>();
    class Fade { public IntPtr Hwnd; public int From, To; public DateTime Start; }
    static List<Fade> fades = new List<Fade>();
    const double FADE_MS = 150;
    static WndProcDelegate shadeProcRef, menuProcRef;
    static int ShadeMargin { get { return Px(18); } }

    static void RoundWindow(IntPtr hwnd, int w, int h)
    {
        try
        {
            int pref = 1; // DWMWCP_DONOTROUND: o recorte abaixo define o arredondamento
            DwmSetWindowAttribute(hwnd, 33, ref pref, 4);
        }
        catch { }
        SetWindowRgn(hwnd, CreateRoundRectRgn(0, 0, w + 1, h + 1, Px(WIN_R * 2), Px(WIN_R * 2)), true);
    }

    static IntPtr NewShadeWindow(bool topmost)
    {
        return CreateWindowExW(0x80000 | 0x20 | 0x80 | 0x08000000 | (topmost ? 0x8u : 0u), "ClipAtalhoShadow", "", 0x80000000,
            0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
    }

    static void AttachShade(IntPtr target, IntPtr shadeWnd)
    {
        if (target == IntPtr.Zero || shadeWnd == IntPtr.Zero) return;
        Shade s = new Shade();
        s.Hwnd = shadeWnd;
        shades[target] = s;
    }

    static void FreeShadeBitmap(Shade s)
    {
        if (s.Dc != IntPtr.Zero)
        {
            if (s.Old != IntPtr.Zero) SelectObject(s.Dc, s.Old);
            DeleteDC(s.Dc);
        }
        if (s.Bmp != IntPtr.Zero) DeleteObject(s.Bmp);
        s.Dc = s.Bmp = s.Old = IntPtr.Zero;
        s.W = s.H = 0;
    }

    // Desenha a sombra (borrada, com os mesmos cantos arredondados) num bitmap com transparência.
    static void RenderShade(Shade s, int tw, int th)
    {
        FreeShadeBitmap(s);
        int m = ShadeMargin, w = tw + 2 * m, h = th + 2 * m;
        BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
        bi.biSize = 40;
        bi.biWidth = w;
        bi.biHeight = -h;
        bi.biPlanes = 1;
        bi.biBitCount = 32;
        IntPtr screen = GetDC(IntPtr.Zero), bits;
        s.Dc = CreateCompatibleDC(screen);
        s.Bmp = CreateDIBSection(screen, ref bi, 0, out bits, IntPtr.Zero, 0);
        ReleaseDC(IntPtr.Zero, screen);
        if (s.Bmp == IntPtr.Zero || bits == IntPtr.Zero) { FreeShadeBitmap(s); return; }
        s.Old = SelectObject(s.Dc, s.Bmp);
        int[] px = new int[w * h];
        float r = Px(WIN_R), maxA = cfgDark ? 120f : 60f, sig = m * 0.45f, oy = Px(4);
        float cx0 = m + tw / 2f, cy0 = m + oy + th / 2f, hw = Math.Max(0, tw / 2f - r), hh = Math.Max(0, th / 2f - r);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Math.Max(Math.Abs(x + 0.5f - cx0) - hw, 0f), dy = Math.Max(Math.Abs(y + 0.5f - cy0) - hh, 0f);
                float d = (float)Math.Sqrt(dx * dx + dy * dy) - r;
                float a = d <= 0 ? maxA : maxA * (float)Math.Exp(-(d * d) / (2 * sig * sig));
                px[y * w + x] = ((int)a & 0xFF) << 24; // preto pré-multiplicado
            }
        Marshal.Copy(px, 0, bits, px.Length);
        s.W = w;
        s.H = h;
        s.Theme = appliedTheme;
    }

    static void PushShade(Shade s, RECT rc)
    {
        if (s.Dc == IntPtr.Zero) return;
        POINT dst = new POINT();
        dst.X = rc.Left - ShadeMargin;
        dst.Y = rc.Top - ShadeMargin;
        SIZE sz = new SIZE();
        sz.cx = s.W;
        sz.cy = s.H;
        POINT src = new POINT();
        BLENDFUNCTION bf = new BLENDFUNCTION();
        bf.SourceConstantAlpha = s.Alpha;
        bf.AlphaFormat = 1; // AC_SRC_ALPHA
        UpdateLayeredWindow(s.Hwnd, IntPtr.Zero, ref dst, ref sz, s.Dc, ref src, 0, ref bf, 2); // ULW_ALPHA
    }

    // Coloca a sombra logo atrás da janela (e a redesenha se o tamanho ou o tema mudou).
    static void PlaceShade(IntPtr target)
    {
        Shade s;
        if (!shades.TryGetValue(target, out s)) return;
        RECT rc;
        if (!GetWindowRect(target, out rc)) return;
        int tw = rc.Right - rc.Left, th = rc.Bottom - rc.Top;
        if (tw + 2 * ShadeMargin != s.W || th + 2 * ShadeMargin != s.H || s.Theme != appliedTheme) RenderShade(s, tw, th);
        int a;
        s.Alpha = (byte)(winAlpha.TryGetValue(target, out a) ? a : 255);
        PushShade(s, rc);
        SetWindowPos(s.Hwnd, target, 0, 0, 0, 0, 0x1 | 0x2 | 0x10 | 0x40); // logo abaixo da janela, sem ativar, visível
    }

    static void HideShade(IntPtr target)
    {
        Shade s;
        if (shades.TryGetValue(target, out s)) ShowWindow(s.Hwnd, 0);
    }

    static void DropShade(IntPtr target)
    {
        Shade s;
        if (!shades.TryGetValue(target, out s)) return;
        FreeShadeBitmap(s);
        DestroyWindow(s.Hwnd);
        shades.Remove(target);
    }

    static void SetWinAlpha(IntPtr hwnd, int a)
    {
        a = Math.Max(0, Math.Min(255, a));
        winAlpha[hwnd] = a;
        SetLayeredWindowAttributes(hwnd, 0, (byte)a, 2); // LWA_ALPHA
        Shade s;
        if (shades.TryGetValue(hwnd, out s) && IsWindowVisible(s.Hwnd))
        {
            s.Alpha = (byte)a;
            RECT rc;
            if (GetWindowRect(hwnd, out rc)) PushShade(s, rc);
        }
    }

    // Aparece ou some gradualmente. Ao sumir, esconde a janela (e a sombra) no final.
    static void FadeWindow(IntPtr hwnd, bool show)
    {
        int cur;
        if (!winAlpha.TryGetValue(hwnd, out cur)) cur = show ? 0 : 255;
        if (show && !IsWindowVisible(hwnd)) cur = 0;
        fades.RemoveAll(delegate(Fade f) { return f.Hwnd == hwnd; });
        SetWinAlpha(hwnd, cur);
        Fade nf = new Fade();
        nf.Hwnd = hwnd;
        nf.From = cur;
        nf.To = show ? 255 : 0;
        nf.Start = DateTime.UtcNow;
        fades.Add(nf);
        SetTimer(hwndMain, (IntPtr)TIMER_FADE, 15, IntPtr.Zero);
    }

    static bool IsFading(IntPtr hwnd) { return fades.Exists(delegate(Fade f) { return f.Hwnd == hwnd; }); }

    static void OnFadeTimer()
    {
        DateTime now = DateTime.UtcNow;
        foreach (Fade f in fades.ToArray())
        {
            double t = Math.Min(1.0, (now - f.Start).TotalMilliseconds / FADE_MS);
            double e = 1 - (1 - t) * (1 - t); // desacelera no final
            SetWinAlpha(f.Hwnd, (int)(f.From + (f.To - f.From) * e));
            if (t >= 1)
            {
                fades.Remove(f);
                if (f.To == 0) { ShowWindow(f.Hwnd, 0); HideShade(f.Hwnd); }
            }
        }
        if (fades.Count == 0) KillTimer(hwndMain, (IntPtr)TIMER_FADE);
    }

    // Some gradualmente e só então devolve o controle (caixas de pergunta e menus).
    static void FadeOutAndWait(IntPtr hwnd)
    {
        FadeWindow(hwnd, false);
        DateTime limit = DateTime.UtcNow.AddMilliseconds(600);
        MSG msg;
        while (IsFading(hwnd) && DateTime.UtcNow < limit)
        {
            if (GetMessageW(out msg, IntPtr.Zero, 0, 0) <= 0) { PostQuitMessage(0); break; }
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        fades.RemoveAll(delegate(Fade f) { return f.Hwnd == hwnd; });
    }

    static void Info(string text)
    {
        if (hwndMgr != IntPtr.Zero && IsWindowVisible(hwndMgr)) AskDialog("ClipAtalho", text, "OK", null, null);
        else MessageBoxW(IntPtr.Zero, text, "ClipAtalho", 0x40);
    }

    static void RepaintAll()
    {
        if (hwndMgr == IntPtr.Zero) return;
        InvalidateRect(hwndMgr, IntPtr.Zero, true);
        foreach (IntPtr c in ctrlHandles) InvalidateRect(c, IntPtr.Zero, true);
    }

    static void ShowPage(int p)
    {
        page = p;
        for (int i = 0; i < ctrlHandles.Count; i++)
            ShowWindow(ctrlHandles[i], (ctrlPages[i] == p || ctrlPages[i] == -1 || (ctrlPages[i] == 24 && (p == 2 || p == 4))) ? 5 : 0);
        if (p == 0) RefreshList();
        if (p == 1) RefreshX(true);
        if (p == 2 || p == 4) startupOn = StartupEnabled();
        if (p == 3) SetWindowTextW(hHelp, HelpText());
        UpdateEditVisibility();
        RepaintAll();
    }

    static void NavTo(int p)
    {
        if (p == page) return;
        if (!ResolveDirty(false)) return;
        ShowPage(p);
    }

    static string HelpText()
    {
        string m = cfgModsLabel;
        return ("O QUE É\n" +
            "O ClipAtalho guarda uma lista de textos (os \"clips\") e cola qualquer um deles direto onde você estiver digitando, com um atalho de teclado, sem abrir nenhuma janela.\n\n" +
            "COLAR COM ATALHO\n" +
            "•  " + m + "+1 até " + m + "+9: cola o clip da posição 1 a 9.\n" +
            "•  Vale nos números de cima do teclado e no teclado numérico (com Num Lock ligado).\n" +
            "•  " + m + "+0: mostra a lista rápida com todos os clips (fixados e recentes). Clique em um item para colar, ou aperte 1 a 9. A lixeira que aparece no item sob o mouse o apaga (num clip fixado, clique duas vezes para confirmar), e \"Limpar\" (clicado duas vezes) apaga todos os não fixados. Fecha com Esc, com o X ou com um clique fora dela. A tecla Print Screen (e o Win+Shift+S) não a fecha, para você poder capturá-la. Para mudar a lista de lugar, arraste a faixa de cima: ela volta a abrir onde você deixou.\n\n" +
            "COMO OS CLIPS ENTRAM NA LISTA\n" +
            "•  Tudo que você copiar (Ctrl+C) entra sozinho na primeira posição livre, como \"Recente\".\n" +
            "•  Com \"Clips de imagem\" ligado em Configurações, imagens copiadas de qualquer forma também entram: Print Screen, Win+Shift+S, \"Copiar imagem\" no navegador ou em outros programas e arquivos de imagem copiados no Explorador. Aparecem com miniatura, colam com os mesmos atalhos e não são editáveis.\n" +
            "•  Para criar um clip à mão: botão \"+ Novo\", digite o texto e clique em \"Salvar\".\n\n" +
            "CLIPS FIXADOS (★)\n" +
            "•  Um clip fixado não sai da posição quando você copia coisas novas e continua guardado ao fechar o programa.\n" +
            "•  Os não fixados são temporários: vão descendo na lista a cada nova cópia e, por padrão, não são guardados ao fechar.\n\n" +
            "ORGANIZAR A LISTA (tela \"Clips\")\n" +
            "•  Editar: selecione o clip, altere o texto à direita e clique em \"Salvar\". Sem salvar, a alteração não vale.\n" +
            "•  Reordenar: arraste o clip com o mouse ou use as setas ↑ ↓.\n" +
            "•  Botão direito sobre um clip: fixar/soltar, mover ou excluir.\n" +
            "•  \"Limpar não fixados\": apaga de uma vez todos os clips temporários.\n" +
            "•  \"Apagar Fixados\" (botão vermelho): apaga todos os clips fixados, depois de confirmar.\n" +
            "•  \"Salvar\" num clip que já existe pergunta: gravar a alteração nele ou salvar como um clip novo (o original fica como estava).\n" +
            "•  Desfazer / refazer: botões ↶ ↷ no canto do cartão, ou Ctrl+Z / Ctrl+Y (fora do campo de texto em edição). Vale para editar, mover, fixar, excluir, limpar e restaurar.\n\n" +
            "ATALHOS POR EXPANSÃO (textos prontos chamados por uma abreviação)\n" +
            "•  Cadastre na tela \"Atalhos por Expansão\": um atalho curto (ex.: bn1) e o texto correspondente (ex.: Boa noite. Tudo bem?).\n" +
            "•  Em qualquer programa, digite o atalho: aparece uma sugestão com o texto. Aperte Tab para inserir — o atalho digitado é apagado e o texto entra no lugar.\n" +
            "•  Esc ignora a sugestão; continuar digitando também a descarta.\n" +
            "•  O atalho só pode ter letras sem acento e números, e deve ser digitado \"solto\": logo depois de um espaço, Enter ou pontuação.\n" +
            "•  A lista de atalhos se organiza como a de clips: arrastar com o mouse, setas ↑ ↓, botão direito, fixar/soltar e \"Limpar não fixados\" (apaga só os atalhos que você soltou).\n" +
            "•  \"Salvar Novo\": grava o que está nos campos como um atalho novo (com outro nome), sem alterar o atalho que você abriu. O \"Salvar\" também pergunta se é alteração ou atalho novo.\n" +
            "•  Desfazer / refazer: botões ↶ ↷ ou Ctrl+Z / Ctrl+Y, como na tela de clips.\n" +
            "•  Se o nome digitado já pertence a outro atalho, aparece em vermelho \"Atalho já em uso\".\n" +
            "•  \"Reorganizar lista\": por data de criação (mais antigos ou mais recentes), A → Z, Z → A ou agrupando famílias (atalhos com o mesmo começo, como end1…end7 ou prst, prdbz1, pr3d). Dá para desfazer a última reorganização.\n" +
            "•  Para funcionar, a opção \"Expansão de texto\" precisa estar ligada em Configurações.\n\n" +
            "CONFIGURAÇÕES\n" +
            "•  Teclas do atalho: troque Ctrl por outra combinação (por exemplo Ctrl+Shift) se o atalho atrapalhar outro programa. Enquanto o ClipAtalho está aberto, o atalho escolhido deixa de valer nos outros programas (Ctrl+1, por exemplo, é \"Formatar células\" no Excel).\n" +
            "•  Iniciar junto com o Windows.\n" +
            "•  Guardar também os clips não fixados.\n" +
            "•  Ligar ou desligar a expansão de texto.\n" +
            "•  Clips de imagem (ligado por padrão).\n" +
            "•  Aparência: tema claro ou escuro e a cor da janela.\n\n" +
            "DADOS E BACKUP (Configurações › Dados e backup)\n" +
            "•  Backup: gera listas em texto (.txt), uma de clips e outra de expansões. O \"Backup completo\" gera as duas e também um arquivo de configurações (.ini: teclas, tema, cor e opções). Entre um item e outro ficam duas linhas em branco. Nos clips, a 1ª linha é o número (★ = fixado); nas expansões, a 1ª linha é o atalho (com - na frente = não fixado) e depois vem o texto. Imagens vão numa pasta \"... - imagens\" ao lado.\n" +
            "•  Restaurar backup: escolha um ou mais arquivos (pode marcar os 3 do backup completo juntos). Nas listas, dá para substituir a atual ou adicionar os itens a ela; as configurações são aplicadas depois de confirmar.\n" +
            "•  Apagar sozinho os clips não fixados: nunca, depois de 30 min, 1 h, 2 h, 24 h ou toda meia-noite. Os fixados nunca são apagados.\n" +
            "•  Pasta de dados: guarda clips e atalhos numa pasta da rede, OneDrive ou Google Drive, para usar os mesmos em vários computadores. Se o arquivo mudar em outro computador, o ClipAtalho pode carregar a versão nova sozinho, perguntar na hora ou perguntar ao fechar; a versão que não ficar é guardada como cópia na pasta.\n" +
            "•  Restaurar padrão de fábrica: apaga tudo deste computador (com sugestão de backup antes). Se houver pasta de dados, ela só é desligada, sem ser apagada.\n\n" +
            "SUGESTÕES E RECLAMAÇÕES\n" +
            "•  Botão \"Enviar sugestão / reclamação\" no alto desta página: a mensagem vai direto para o desenvolvedor (precisa de internet). O e-mail é opcional.\n\n" +
            "ÍCONE PERTO DO RELÓGIO\n" +
            "•  Clique: abre esta janela.  Botão direito: menu com Pausar e Sair.\n" +
            "•  Fechar esta janela não encerra o programa: os atalhos continuam funcionando.\n\n" +
            "LEVAR PARA OUTRO COMPUTADOR\n" +
            "•  Use o backup, ou (na versão portátil) copie a pasta \"ClipAtalho\" inteira, com o .exe e o ClipAtalho.ini juntos. No app da Microsoft Store, os dados ficam em %LOCALAPPDATA%\\ClipAtalho. O .ini guarda as configurações e os clips: não o apague. Se usar clips de imagem, leve também a pasta \"ClipAtalho imagens\".\n").Replace("\n", "\r\n");
    }

    // ---- iniciar com o Windows (registro do usuário atual, não exige administrador)
    static string ExePath() { return System.Reflection.Assembly.GetExecutingAssembly().Location; }

    [DllImport(K, CharSet = CharSet.Unicode)] static extern int GetCurrentPackageFullName(ref int length, StringBuilder name);
    static bool packaged; // instalado pela Microsoft Store (pacote MSIX)

    static bool IsPackaged()
    {
        try
        {
            int len = 0;
            return GetCurrentPackageFullName(ref len, null) == 122; // ERROR_INSUFFICIENT_BUFFER = tem pacote
        }
        catch { return false; } // Windows antigo, sem pacotes
    }

    static bool StartupEnabled()
    {
        if (packaged) return true; // no app da loja, quem controla é o Windows (Configurações › Aplicativos › Inicialização)
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RUNKEY))
                return k != null && k.GetValue("ClipAtalho") != null;
        }
        catch { return false; }
    }

    static void SetStartup(bool on)
    {
        if (packaged) return;
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RUNKEY, true))
            {
                if (k == null) return;
                if (on) k.SetValue("ClipAtalho", "\"" + ExePath() + "\"");
                else k.DeleteValue("ClipAtalho", false);
            }
            Log("Iniciar com o Windows: " + (on ? "ligado" : "desligado"));
        }
        catch (Exception e) { Log("FALHA ao alterar 'Iniciar com o Windows': " + e.Message); }
    }

    // ---- caixa de pergunta com até 3 botões personalizados (devolve 1, 2 ou 3; fechar = 2)
    static string dlgTitle = "";

    static int AskDialog(string title, string message, string b1, string b2, string b3)
    {
        dlgLabels = new string[] { b1, b2, b3 };
        dlgMessage = message;
        dlgTitle = title;
        dlgResult = 0;
        const uint style = 0x80000000 | 0x02000000; // sem barra de título: o programa desenha tudo
        int w = Px(480), h = Px(196);
        RECT o;
        int x = (GetSystemMetrics(0) - w) / 2, y = (GetSystemMetrics(1) - h) / 2;
        if (hwndMgr != IntPtr.Zero && GetWindowRect(hwndMgr, out o))
        {
            x = o.Left + (o.Right - o.Left - w) / 2;
            y = o.Top + (o.Bottom - o.Top - h) / 2;
        }
        hDlg = CreateWindowExW(0x10000 | 0x80000, "ClipAtalhoDialog", title, style, x, y, w, h, hwndMgr, IntPtr.Zero, hInst, IntPtr.Zero);
        if (hDlg == IntPtr.Zero) return 2;
        IntPtr me = hDlg;
        RoundWindow(me, w, h);
        AttachShade(me, NewShadeWindow(false));
        int[] widths = new int[3];
        int total = 0;
        for (int i = 0; i < 3; i++)
        {
            if (dlgLabels[i] == null) continue;
            widths[i] = Math.Max(96, 34 + dlgLabels[i].Length * 8);
            total += widths[i] + 8;
        }
        int bx = 480 - 18 - (total - 8);
        for (int i = 0; i < 3; i++)
        {
            if (dlgLabels[i] == null) continue;
            Child(me, "BUTTON", "", 0x10000 | 0xB, bx, 142, widths[i], 36, IDC_DLG + 1 + i, -2);
            bx += widths[i] + 8;
        }
        if (hwndMgr != IntPtr.Zero) EnableWindow(hwndMgr, false);
        FadeWindow(me, true);
        ShowWindow(me, 5);
        PlaceShade(me);
        SetForegroundWindow(me);
        MSG msg;
        while (dlgResult == 0)
        {
            int g = GetMessageW(out msg, IntPtr.Zero, 0, 0);
            if (g <= 0) { PostQuitMessage(0); dlgResult = 2; break; }
            if (IsDialogMessageW(me, ref msg)) continue;
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        int result = dlgResult;
        dlgEscResult = 2;
        dlgPrimary = 3;
        if (hwndMgr != IntPtr.Zero) EnableWindow(hwndMgr, true);
        FadeOutAndWait(me);
        DropShade(me);
        winAlpha.Remove(me);
        DestroyWindow(me);
        hDlg = IntPtr.Zero;
        if (hwndMgr != IntPtr.Zero) SetForegroundWindow(hwndMgr);
        return result;
    }

    static IntPtr DlgProc(IntPtr hwnd, uint m, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (m)
            {
                case WM_COMMAND:
                    int id = (int)(wParam.ToInt64() & 0xFFFF);
                    if (id > IDC_DLG && id <= IDC_DLG + 3) dlgResult = id - IDC_DLG;
                    else if (id == 2) dlgResult = dlgEscResult; // Esc
                    else if (id == IDC_FB_SUG || id == IDC_FB_REC)
                    {
                        fbComplaint = id == IDC_FB_REC;
                        InvalidateRect(GetDlgItem(hwnd, IDC_FB_SUG), IntPtr.Zero, false);
                        InvalidateRect(GetDlgItem(hwnd, IDC_FB_REC), IntPtr.Zero, false);
                    }
                    return IntPtr.Zero;
                case WM_CLOSE:
                    dlgResult = dlgEscResult;
                    return IntPtr.Zero;
                case 0x0133: // WM_CTLCOLOREDIT
                    SetTextColor(wParam, C_TEXT);
                    SetBkColor(wParam, C_CARD);
                    return hWhiteBrush;
                case 0x0084: // WM_NCHITTEST: arrasta pela faixa do título
                {
                    int sy = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    RECT wr;
                    GetWindowRect(hwnd, out wr);
                    return (IntPtr)(sy - wr.Top < Px(42) ? 2 : 1);
                }
                case 0x0014: // WM_ERASEBKGND
                    RECT bgrc;
                    GetClientRect(hwnd, out bgrc);
                    FillRect(wParam, ref bgrc, hWhiteBrush);
                    return (IntPtr)1;
                case WM_PAINT:
                    PAINTSTRUCT ps;
                    IntPtr hdc = BeginPaint(hwnd, out ps);
                    if (dlgFeedback) { PaintFeedback(hdc); EndPaint(hwnd, ref ps); return IntPtr.Zero; }
                    DrawTextAt(hdc, dlgTitle, hHeadFont, C_TEXT, Px(22), Px(12), Px(458), Px(42), 0x8024);
                    int tl = Px(22);
                    if (dlgWarn)
                    {
                        DrawIconEx(hdc, Px(22), Px(52), LoadIconW(IntPtr.Zero, (IntPtr)32515), Px(32), Px(32), 0, IntPtr.Zero, 3); // ícone de alerta
                        tl = Px(68);
                    }
                    DrawTextAt(hdc, dlgMessage.Replace("\n", "\r\n"), hUiFont, C_TEXT, tl, Px(50), Px(458), Px(134), 0x10); // quebra de linha
                    RoundRect(hdc, 0, 0, Px(480), Px(196), Px(WIN_R), 0, A_BTNBORDER);
                    EndPaint(hwnd, ref ps);
                    return IntPtr.Zero;
                case 0x002B: // WM_DRAWITEM
                    Buffered((DRAWITEMSTRUCT)Marshal.PtrToStructure(lParam, typeof(DRAWITEMSTRUCT)), DrawButton);
                    return (IntPtr)1;
            }
        }
        catch { }
        return DefWindowProcW(hwnd, m, wParam, lParam);
    }

    // ---- edição: só vale depois de "Salvar"
    static string EditText()
    {
        int len = GetWindowTextLengthW(hEdit);
        StringBuilder sb = new StringBuilder(len + 2);
        GetWindowTextW(hEdit, sb, len + 1);
        return sb.ToString();
    }

    static void SetDirty(bool dirty)
    {
        if (editDirty == dirty) return;
        editDirty = dirty;
        InvalidateRect(hwndMgr, IntPtr.Zero, true);
        InvalidateRect(GetDlgItem(hwndMgr, IDC_SAVE), IntPtr.Zero, true);
    }

    static int AskSave(string what)
    {
        dlgEscResult = 3;
        dlgPrimary = 1;
        bool clip = what == "clip";
        return AskDialog("Salvar " + what,
            "Gravar a alteração neste " + what + ", ou salvar como um " + what + " novo (o original fica como estava)?" +
            (clip ? "" : "\nPara salvar como novo, o atalho precisa de outro nome."),
            "Salvar alteração", clip ? "Salvar como novo" : "Salvar como novo", "Cancelar");
    }

    // "Salvar como novo": o texto editado vira um clip fixado logo abaixo do original, que não muda.
    static void SaveEditAsNew()
    {
        string text = EditText();
        if (text.Length == 0) return;
        PushClipUndo();
        int at = editClip == null ? -1 : clips.IndexOf(editClip);
        if (clips.Count >= MAX_CLIPS)
        {
            int last = clips.FindLastIndex(delegate(Clip c) { return !c.Pinned; });
            if (last < 0) { Info("A lista está cheia de clips fixados. Exclua ou solte algum."); return; }
            clips.RemoveAt(last);
            at = editClip == null ? -1 : clips.IndexOf(editClip);
        }
        Clip novo = new Clip(text, true);
        clips.Insert(at < 0 ? clips.Count : at + 1, novo);
        mgrSel = novo;
        editClip = null;
        SetDirty(false);
        SaveClips();
        RefreshList();
    }

    static void SaveEdit()
    {
        if (editClip != null && editClip.IsImage) { SetDirty(false); return; } // imagem não é editável
        PushClipUndo();
        string text = EditText();
        if (editClip == null)
        {
            if (text.Length == 0) { SetDirty(false); return; }
            editClip = new Clip(text, true); // lista vazia: o texto digitado vira um clip novo
            clips.Add(editClip);
            mgrSel = editClip;
        }
        else editClip.Text = text;
        SetDirty(false);
        SaveClips();
        RefreshList();
    }

    // Há edição não salva (clip ou atalho)? Pergunta o que fazer. Devolve false se o usuário quer continuar editando.
    static bool ResolveDirty(bool closing)
    {
        if (!editDirty && !expDirty) return true;
        bool exp = !editDirty;
        string what = exp ? "atalho" : "clip";
        int r = AskDialog("Edição não salva",
            closing ? "Deseja sair sem salvar a edição feita no " + what + "?" : "A edição feita neste " + what + " não foi salva. Deseja descartá-la?",
            "Sim", "Não", closing ? "Salvar e fechar" : "Salvar");
        if (r == 3)
        {
            if (exp) return SaveX(false);
            SaveEdit();
            return true;
        }
        if (r == 1)
        {
            if (exp)
            {
                xNew = false;
                RefreshX(true); // recarrega o texto original
            }
            else
            {
                SetDirty(false);
                editClip = null; // força recarregar o texto original
            }
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ desfazer / refazer
    class ClipSnap { public List<Clip> Refs = new List<Clip>(); public List<string> Texts = new List<string>(); public List<bool> Pins = new List<bool>(); public DateTime Time = DateTime.Now; }
    class XSnap
    {
        public List<string> Order, Loose, Est;
        public Dictionary<string, string> Map;
        public Dictionary<string, DateTime> Dates;
    }
    static List<ClipSnap> clipUndo = new List<ClipSnap>(), clipRedo = new List<ClipSnap>();
    static List<XSnap> xUndo = new List<XSnap>(), xRedo = new List<XSnap>();
    static bool xUndoing;
    const int UNDO_MAX = 30;

    static ClipSnap TakeClipSnap()
    {
        ClipSnap s = new ClipSnap();
        foreach (Clip c in clips) { s.Refs.Add(c); s.Texts.Add(c.Text); s.Pins.Add(c.Pinned); }
        return s;
    }

    static bool SameSnap(ClipSnap a, ClipSnap b)
    {
        if (a.Refs.Count != b.Refs.Count) return false;
        for (int i = 0; i < a.Refs.Count; i++)
            if (a.Refs[i] != b.Refs[i] || a.Texts[i] != b.Texts[i] || a.Pins[i] != b.Pins[i]) return false;
        return true;
    }

    static void InvalidateUndoButtons()
    {
        if (hwndMgr == IntPtr.Zero) return;
        foreach (int i in new int[] { IDC_UNDO, IDC_REDO, IDC_XUNDO, IDC_XREDO }) InvalidateRect(GetDlgItem(hwndMgr, i), IntPtr.Zero, false);
    }

    static void PushClipUndo()
    {
        ClipSnap s = TakeClipSnap();
        if (clipUndo.Count > 0 && SameSnap(clipUndo[clipUndo.Count - 1], s)) return;
        clipUndo.Add(s);
        if (clipUndo.Count > UNDO_MAX) clipUndo.RemoveAt(0);
        clipRedo.Clear();
        InvalidateUndoButtons();
    }

    static bool InUndoHistory(Clip c)
    {
        foreach (ClipSnap s in clipUndo) if (s.Refs.Contains(c)) return true;
        foreach (ClipSnap s in clipRedo) if (s.Refs.Contains(c)) return true;
        return false;
    }

    // Volta a lista ao estado guardado; o que foi copiado (Ctrl+C) depois disso continua na lista.
    static void RestoreClipSnap(ClipSnap s)
    {
        List<Clip> res = new List<Clip>();
        for (int i = 0; i < s.Refs.Count; i++)
        {
            Clip c = s.Refs[i];
            c.Text = s.Texts[i];
            c.Pinned = s.Pins[i];
            res.Add(c);
        }
        List<Clip> later = clips.FindAll(delegate(Clip c) { return c.Copied && !c.Pinned && !s.Refs.Contains(c) && c.Added >= s.Time; });
        int at = res.FindIndex(delegate(Clip c) { return !c.Pinned; });
        res.InsertRange(at < 0 ? res.Count : at, later);
        while (res.Count > MAX_CLIPS)
        {
            int last = res.FindLastIndex(delegate(Clip c) { return !c.Pinned; });
            if (last < 0) break;
            res.RemoveAt(last);
        }
        clips = res;
        if (mgrSel != null && !clips.Contains(mgrSel)) mgrSel = null;
        editClip = null;
        editDirty = false;
        SaveClips();
        RefreshList();
        RefreshPopupList();
    }

    static XSnap TakeXSnap()
    {
        XSnap s = new XSnap();
        s.Order = new List<string>(cfgAbbrOrder);
        s.Loose = new List<string>(cfgAbbrLoose);
        s.Est = new List<string>(cfgAbbrEst);
        s.Map = new Dictionary<string, string>(cfgAbbr);
        s.Dates = new Dictionary<string, DateTime>(cfgAbbrDate);
        return s;
    }

    static void PushXUndo()
    {
        xUndo.Add(TakeXSnap());
        if (xUndo.Count > UNDO_MAX) xUndo.RemoveAt(0);
        xRedo.Clear();
        InvalidateUndoButtons();
    }

    static void RestoreXSnap(XSnap s)
    {
        cfgAbbrDate = new Dictionary<string, DateTime>(s.Dates);
        cfgAbbrEst = new List<string>(s.Est);
        xUndoing = true;
        try { WriteAbbr(s.Order, s.Map, s.Loose); }
        finally { xUndoing = false; }
        xNew = false;
        expDirty = false;
        RefreshX(true);
    }

    // which: 0 = clips, 1 = atalhos por expansão
    static void DoUndo(int which, bool redo)
    {
        if (!ResolveDirty(false)) return;
        if (which == 0)
        {
            List<ClipSnap> from = redo ? clipRedo : clipUndo, to = redo ? clipUndo : clipRedo;
            ClipSnap cur = TakeClipSnap();
            while (from.Count > 0 && SameSnap(from[from.Count - 1], cur)) from.RemoveAt(from.Count - 1); // passos sem mudança
            if (from.Count == 0) { InvalidateUndoButtons(); return; }
            ClipSnap s = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(cur);
            RestoreClipSnap(s);
        }
        else
        {
            List<XSnap> from = redo ? xRedo : xUndo, to = redo ? xUndo : xRedo;
            if (from.Count == 0) return;
            XSnap s = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(TakeXSnap());
            RestoreXSnap(s);
        }
        InvalidateUndoButtons();
    }

    // ------------------------------------------------------------------ backup e restauração (listas em .txt)
    static string PickFolder(string title)
    {
        BROWSEINFO bi = new BROWSEINFO();
        bi.hwndOwner = hwndMgr;
        bi.lpszTitle = title;
        bi.ulFlags = 0x1 | 0x40; // só pastas, janela nova (com "Criar pasta")
        bi.pszDisplayName = Marshal.AllocHGlobal(1040);
        try
        {
            IntPtr pidl = SHBrowseForFolderW(ref bi);
            if (pidl == IntPtr.Zero) return null;
            StringBuilder sb = new StringBuilder(1040);
            bool ok = SHGetPathFromIDListW(pidl, sb);
            CoTaskMemFree(pidl);
            return ok && sb.Length > 0 ? sb.ToString() : null;
        }
        finally { Marshal.FreeHGlobal(bi.pszDisplayName); }
    }

    static List<string> PickBackupFiles()
    {
        List<string> res = new List<string>();
        OPENFILENAME o = new OPENFILENAME();
        o.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
        o.hwndOwner = hwndMgr;
        o.lpstrFilter = "Backups do ClipAtalho (*.txt; *.ini)\0*.txt;*.ini\0Todos os arquivos\0*.*\0\0";
        o.nFilterIndex = 1;
        o.nMaxFile = 16384;
        o.lpstrFile = Marshal.AllocHGlobal(o.nMaxFile * 2);
        o.lpstrTitle = "Restaurar backup do ClipAtalho (pode escolher os 3 arquivos juntos)";
        o.Flags = 0x1000 | 0x800 | 0x8 | 0x200 | 0x80000; // existir, sem mudar a pasta atual, vários arquivos
        try
        {
            Marshal.Copy(new byte[o.nMaxFile * 2], 0, o.lpstrFile, o.nMaxFile * 2);
            if (!GetOpenFileNameW(ref o)) return res;
            List<string> parts = new List<string>();
            IntPtr q = o.lpstrFile;
            while (true)
            {
                string part = Marshal.PtrToStringUni(q);
                if (string.IsNullOrEmpty(part)) break;
                parts.Add(part);
                q = new IntPtr(q.ToInt64() + (part.Length + 1) * 2);
            }
            if (parts.Count == 1) res.Add(parts[0]);
            else for (int i = 1; i < parts.Count; i++) res.Add(Path.Combine(parts[0], parts[i]));
        }
        finally { Marshal.FreeHGlobal(o.lpstrFile); }
        res.Sort(delegate(string x, string y) { return RestoreRank(x).CompareTo(RestoreRank(y)); }); // configurações, depois clips e expansões
        return res;
    }

    static int RestoreRank(string f) { return f.ToLowerInvariant().EndsWith(".ini") ? 0 : (f.ToLowerInvariant().Contains("clipes") ? 1 : 2); }

    static string PickTxtFile()
    {
        OPENFILENAME o = new OPENFILENAME();
        o.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
        o.hwndOwner = hwndMgr;
        o.lpstrFilter = "Listas do ClipAtalho (*.txt)\0*.txt\0Todos os arquivos\0*.*\0\0";
        o.nFilterIndex = 1;
        o.nMaxFile = 1024;
        o.lpstrFile = Marshal.AllocHGlobal(o.nMaxFile * 2);
        o.lpstrTitle = "Restaurar backup do ClipAtalho";
        o.Flags = 0x1000 | 0x800 | 0x8; // arquivo e pasta precisam existir; não muda a pasta atual
        try
        {
            Marshal.Copy(new byte[o.nMaxFile * 2], 0, o.lpstrFile, o.nMaxFile * 2);
            if (!GetOpenFileNameW(ref o)) return null;
            return Marshal.PtrToStringUni(o.lpstrFile);
        }
        finally { Marshal.FreeHGlobal(o.lpstrFile); }
    }

    static string ShortVersion() { return VERSION.Split(' ')[0]; }

    static string FreeName(string dir, string baseName, string ext)
    {
        string p = Path.Combine(dir, baseName + ext);
        for (int i = 2; File.Exists(p); i++) p = Path.Combine(dir, baseName + " (" + i + ")" + ext);
        return p;
    }

    static string ClipsBackupText(string imgDir)
    {
        StringBuilder sb = new StringBuilder();
        int n = 0;
        foreach (Clip c in clips)
        {
            string body;
            if (c.IsImage)
            {
                if (!EnsureImageFile(c)) continue;
                try
                {
                    Directory.CreateDirectory(imgDir);
                    File.Copy(Path.Combine(ImgDir, c.ImgFile), Path.Combine(imgDir, c.ImgFile), true);
                }
                catch { continue; }
                body = "[Imagem] " + c.ImgFile;
            }
            else
            {
                if (c.Text.Length == 0) continue;
                body = c.Text.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n').Replace("\n", "\r\n");
            }
            if (n > 0) sb.Append("\r\n\r\n\r\n"); // duas linhas em branco entre os itens
            n++;
            sb.Append(n).Append(c.Pinned ? " ★" : "").Append("\r\n").Append(body);
        }
        return sb.Append("\r\n").ToString();
    }

    static string XBackupText()
    {
        StringBuilder sb = new StringBuilder();
        int n = 0;
        foreach (string k in cfgAbbrOrder)
        {
            string v;
            if (!cfgAbbr.TryGetValue(k, out v)) continue;
            if (n++ > 0) sb.Append("\r\n\r\n\r\n");
            sb.Append(cfgAbbrLoose.Contains(k) ? "-" : "").Append(k).Append("\r\n")
              .Append(v.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n').Replace("\n", "\r\n"));
        }
        return sb.Append("\r\n").ToString();
    }

    // Faz o backup numa pasta escolhida. Devolve false se cancelado ou com erro.
    // Seção [geral] (tema, cor, teclas, opções) do .ini atual.
    static string ConfigBackupText()
    {
        StringBuilder sb = new StringBuilder("; ClipAtalho " + VERSION + " - backup de configurações\r\n[geral]\r\n");
        bool inGeral = false;
        foreach (string raw in (ReadIni() ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            string t = raw.Trim().TrimStart((char)0xFEFF);
            if (t.StartsWith("[") && t.EndsWith("]")) { inGeral = t.ToLowerInvariant() == "[geral]"; continue; }
            if (inGeral && t.Length > 0 && t[0] != ';' && t.IndexOf('=') > 0) sb.Append(t).Append("\r\n");
        }
        return sb.ToString();
    }

    static bool DoBackup(bool doClips, bool doX) { return DoBackup(doClips, doX, false); }

    static bool DoBackup(bool doClips, bool doX, bool doCfg)
    {
        if (!ResolveDirty(false)) return false;
        string dir = PickFolder("Escolha a pasta onde salvar o backup do ClipAtalho");
        if (dir == null) return false;
        string date = DateTime.Now.ToString("dd-MM-yyyy");
        List<string> made = new List<string>();
        try
        {
            if (doClips)
            {
                string f = FreeName(dir, "ClipAtalho " + ShortVersion() + " Backup da Lista de Clipes " + date, ".txt");
                File.WriteAllText(f, ClipsBackupText(f.Substring(0, f.Length - 4) + " - imagens"), new UTF8Encoding(true));
                made.Add(Path.GetFileName(f));
            }
            if (doX)
            {
                string f = FreeName(dir, "ClipAtalho " + ShortVersion() + " Backup da Lista de Expansões " + date, ".txt");
                File.WriteAllText(f, XBackupText(), new UTF8Encoding(true));
                made.Add(Path.GetFileName(f));
            }
            if (doCfg)
            {
                string f = FreeName(dir, "ClipAtalho " + ShortVersion() + " Backup de Configurações " + date, ".ini");
                File.WriteAllText(f, ConfigBackupText(), new UTF8Encoding(true));
                made.Add(Path.GetFileName(f));
            }
        }
        catch (Exception e)
        {
            Info("Não foi possível salvar o backup:\n" + e.Message);
            return false;
        }
        Log("Backup salvo em " + dir + ": " + string.Join(", ", made.ToArray()));
        Info("Backup salvo em:\n" + dir + "\n\n" + string.Join("\n", made.ToArray()));
        return true;
    }

    // Separa os itens: cada um começa numa linha de cabeçalho (número do clip ou nome do atalho)
    // que está no início do arquivo ou depois de duas linhas em branco.
    static List<KeyValuePair<string, string>> ParseBackup(string text, Func<string, bool> isHeader)
    {
        List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>();
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").TrimStart((char)0xFEFF).Split('\n');
        string head = null;
        List<string> body = new List<string>();
        int blanks = 2;
        foreach (string raw in lines)
        {
            string t = raw.Trim();
            if (blanks >= 2 && t.Length > 0 && isHeader(t))
            {
                if (head != null) items.Add(new KeyValuePair<string, string>(head, CloseBody(body)));
                head = t;
                body = new List<string>();
                blanks = 0;
                continue;
            }
            blanks = t.Length == 0 ? blanks + 1 : 0;
            if (head != null) body.Add(raw);
        }
        if (head != null) items.Add(new KeyValuePair<string, string>(head, CloseBody(body)));
        return items;
    }

    static string CloseBody(List<string> body)
    {
        while (body.Count > 0 && body[body.Count - 1].Trim().Length == 0) body.RemoveAt(body.Count - 1);
        return string.Join("\n", body.ToArray());
    }

    static bool IsClipHeader(string t)
    {
        string n = t.EndsWith("★") ? t.Substring(0, t.Length - 1).Trim() : t;
        int v;
        return n.Length > 0 && n.Length <= 3 && int.TryParse(n, out v) && v > 0;
    }

    static bool IsXHeader(string t) { return ValidAbbr(t.StartsWith("-") ? t.Substring(1) : t); }

    static void DoRestore()
    {
        if (!ResolveDirty(false)) return;
        foreach (string f in PickBackupFiles())
        {
            if (f.ToLowerInvariant().EndsWith(".ini")) RestoreConfig(f);
            else RestoreList(f);
        }
    }

    // Aplica a seção [geral] do backup de configurações (tema, cor, teclas e opções).
    static void RestoreConfig(string file)
    {
        string text = ReadTextFile(file);
        List<string> body = new List<string>();
        bool inGeral = false;
        if (text != null)
            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string t = raw.Trim().TrimStart((char)0xFEFF);
                if (t.StartsWith("[") && t.EndsWith("]")) { inGeral = t.ToLowerInvariant() == "[geral]"; continue; }
                if (inGeral && t.Length > 0 && t[0] != ';' && t.IndexOf('=') > 0) body.Add(t);
            }
        if (body.Count == 0) { Info("Nenhuma configuração encontrada em:\n" + Path.GetFileName(file)); return; }
        dlgEscResult = 2;
        dlgPrimary = 1;
        if (AskDialog("Restaurar configurações", "Aplicar as configurações do backup (teclas do atalho, tema, cor e opções)?\n" + Path.GetFileName(file),
            "Aplicar", "Cancelar", null) != 1) return;
        foreach (string l in body)
        {
            int eq = l.IndexOf('=');
            SetIniValue("geral", l.Substring(0, eq).Trim().ToLowerInvariant(), l.Substring(eq + 1).Trim());
        }
        LoadConfig();
        RepaintAll();
        Info("Configurações restauradas.");
    }

    static void RestoreList(string file)
    {
        string text = ReadTextFile(file);
        if (text == null) { Info("Não foi possível ler o arquivo."); return; }
        string name = Path.GetFileName(file).ToLowerInvariant();
        bool isClips;
        if (name.Contains("clipes") || name.Contains("clips")) isClips = true;
        else if (name.Contains("expans")) isClips = false;
        else
        {
            string first = "";
            foreach (string l in text.Replace("\r", "").TrimStart((char)0xFEFF).Split('\n')) if (l.Trim().Length > 0) { first = l.Trim(); break; }
            isClips = IsClipHeader(first);
        }
        List<KeyValuePair<string, string>> items = ParseBackup(text, isClips ? new Func<string, bool>(IsClipHeader) : new Func<string, bool>(IsXHeader));
        if (items.Count == 0) { Info("Nenhum item encontrado nesse arquivo.\nO backup precisa estar no formato gerado pelo ClipAtalho."); return; }
        dlgEscResult = 3;
        dlgPrimary = 2;
        int r = AskDialog("Restaurar backup",
            "Encontrados " + items.Count + (isClips ? " clip(s)." : " atalho(s) por expansão.") +
            "\nSubstituir a lista atual ou adicionar estes itens a ela? (dá para desfazer com Ctrl+Z)",
            "Substituir", "Adicionar", "Cancelar");
        if (r != 1 && r != 2) return;
        if (isClips) RestoreClips(items, file.Substring(0, file.Length - 4) + " - imagens", r == 1);
        else RestoreX(items, r == 1);
    }

    static void RestoreClips(List<KeyValuePair<string, string>> items, string imgDir, bool replace)
    {
        PushClipUndo();
        List<Clip> got = new List<Clip>();
        foreach (KeyValuePair<string, string> it in items)
        {
            bool pin = it.Key.EndsWith("★");
            string body = it.Value;
            if (body.StartsWith("[Imagem] ") && body.IndexOf('\n') < 0)
            {
                string fn = Path.GetFileName(body.Substring(9).Trim());
                try
                {
                    string src = Path.Combine(imgDir, fn);
                    if (!File.Exists(src)) continue;
                    Directory.CreateDirectory(ImgDir);
                    string dst = Path.Combine(ImgDir, fn);
                    if (!File.Exists(dst)) File.Copy(src, dst);
                    Clip ic = LoadImageClip(fn, pin);
                    if (ic != null && ic.Image != IntPtr.Zero) got.Add(ic);
                }
                catch { }
                continue;
            }
            if (body.Length > 0) got.Add(new Clip(body.Replace("\n", "\r\n"), pin));
        }
        List<Clip> res = replace ? new List<Clip>() : new List<Clip>(clips);
        int skipped = 0;
        foreach (Clip c in got)
        {
            if (res.Exists(delegate(Clip o) { return Same(o, c); })) { skipped++; continue; }
            if (res.Count >= MAX_CLIPS)
            {
                int last = res.FindLastIndex(delegate(Clip o) { return !o.Pinned && !got.Contains(o); });
                if (last < 0) { skipped++; continue; }
                res.RemoveAt(last);
            }
            res.Add(c);
        }
        clips = res;
        mgrSel = null;
        editClip = null;
        SaveClips();
        RefreshList();
        RefreshPopupList();
        Info((got.Count - skipped) + " clip(s) restaurado(s)." + (skipped > 0 ? "\n" + skipped + " não entraram (repetidos ou lista cheia: máximo " + MAX_CLIPS + ")." : ""));
    }

    static void RestoreX(List<KeyValuePair<string, string>> items, bool replace)
    {
        List<string> order = replace ? new List<string>() : new List<string>(cfgAbbrOrder);
        Dictionary<string, string> d = replace ? new Dictionary<string, string>() : new Dictionary<string, string>(cfgAbbr);
        List<string> loose = replace ? new List<string>() : new List<string>(cfgAbbrLoose);
        int added = 0, updated = 0;
        foreach (KeyValuePair<string, string> it in items)
        {
            bool isLoose = it.Key.StartsWith("-");
            string k = (isLoose ? it.Key.Substring(1) : it.Key).ToLowerInvariant();
            if (it.Value.Trim().Length == 0) continue;
            if (d.ContainsKey(k)) updated++;
            else { added++; order.Add(k); if (!cfgAbbrDate.ContainsKey(k)) cfgAbbrDate[k] = DateTime.Now; }
            d[k] = it.Value;
            loose.Remove(k);
            if (isLoose) loose.Add(k);
        }
        WriteAbbr(order, d, loose);
        xSel = null;
        xNew = false;
        RefreshX(true);
        Info(added + " atalho(s) novo(s)" + (updated > 0 ? " e " + updated + " atualizado(s)" : "") + ".");
    }

    static void AutoDelMenu()
    {
        List<MI> mi = new List<MI>();
        for (int i = 0; i < AutoDelNames.Length; i++) { MI m = Item(1 + i, AutoDelNames[i]); m.Check = i == AutoDelIndex(); mi.Add(m); }
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_AUTODEL), out r);
        int cmd = ShowMenu(mi, r.Left, r.Bottom + Px(4), hwndMgr);
        if (cmd < 1) return;
        SetIniValue("geral", "autoexclusao", AutoDelKeys[cmd - 1]);
        LoadConfig();
        autoDelDay = DateTime.Today;
        RunAutoDelete();
        RepaintAll();
    }

    // ------------------------------------------------------------------ pasta de dados
    static void ConflictMenu()
    {
        List<MI> mi = new List<MI>();
        for (int i = 0; i < ConflictNames.Length; i++) { MI m = Item(1 + i, ConflictNames[i]); m.Check = i == conflictMode; mi.Add(m); }
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_CONFLICT), out r);
        int cmd = ShowMenu(mi, r.Left, r.Bottom + Px(4), hwndMgr);
        if (cmd < 1) return;
        conflictMode = cmd - 1;
        SaveLocal();
        RepaintAll();
    }

    static void CopyImages(string fromDir, string toDir)
    {
        try
        {
            string a = Path.Combine(fromDir, "ClipAtalho imagens"), b = Path.Combine(toDir, "ClipAtalho imagens");
            if (!Directory.Exists(a)) return;
            Directory.CreateDirectory(b);
            foreach (string f in Directory.GetFiles(a, "img_*.png"))
            {
                string t = Path.Combine(b, Path.GetFileName(f));
                if (!File.Exists(t)) File.Copy(f, t);
            }
        }
        catch (Exception e) { Log("Falha ao copiar imagens: " + e.Message); }
    }

    static bool SameDir(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    // Passa a usar a pasta escolhida (copia os dados atuais para lá ou usa os que já estão lá).
    static void UseDataFolder(string dir)
    {
        if (SameDir(dir, exeDir)) { Info("Escolha uma pasta diferente da pasta do programa (por exemplo, uma pasta na rede ou no OneDrive)."); return; }
        if (dataOn && !dataMissing && SameDir(dir, dataDir)) return;
        ResolveHold(true);
        string target = Path.Combine(dir, "ClipAtalho.ini");
        string mine = ReadIni() ?? "";
        string fromDir = DataDir;
        try
        {
            if (File.Exists(target))
            {
                dlgEscResult = 3;
                dlgPrimary = 1;
                int r = AskDialog("Pasta de dados", "Já existe um ClipAtalho.ini nessa pasta.\nUsar os clips e atalhos que estão lá, ou substituí-los pelos deste computador? (o arquivo substituído fica guardado como cópia)",
                    "Usar os de lá", "Substituir", "Cancelar");
                if (r == 3) return;
                if (r == 2)
                {
                    File.Copy(target, Path.Combine(dir, "ClipAtalho (antes de substituir " + Stamp() + ").ini"), true);
                    File.WriteAllText(target, mine, new UTF8Encoding(true));
                }
            }
            else File.WriteAllText(target, mine, new UTF8Encoding(true));
            CopyImages(fromDir, dir);
        }
        catch (Exception e) { Info("Não foi possível usar essa pasta:\n" + e.Message); return; }
        dataOn = true;
        dataDir = dir;
        SaveLocal();
        ApplyDataPaths();
        ReloadAll();
        Log("Pasta de dados: " + dir);
        Info("Pronto. Os clips e atalhos agora ficam em:\n" + dir);
    }

    static void ChooseDataFolder()
    {
        if (!ResolveDirty(false)) return;
        string dir = PickFolder("Escolha a pasta de dados do ClipAtalho (rede, OneDrive, Google Drive…)");
        if (dir != null) UseDataFolder(dir);
    }

    static void ToggleDataFolder()
    {
        if (!ResolveDirty(false)) return;
        if (!dataOn) { ChooseDataFolder(); return; }
        dlgEscResult = 2;
        dlgPrimary = 1;
        if (AskDialog("Pasta de dados", "Parar de usar a pasta de dados neste computador?\nOs clips e atalhos atuais são copiados para a pasta do programa. A pasta compartilhada não é alterada.",
            "Parar de usar", "Cancelar", null) != 1) return;
        ResolveHold(true);
        DisconnectDataFolder(true);
        ReloadAll();
        RepaintAll();
    }

    static void DisconnectDataFolder(bool copyBack)
    {
        if (copyBack && !dataMissing)
        {
            try
            {
                string t = ReadIni();
                if (t != null) File.WriteAllText(Path.Combine(exeDir, "ClipAtalho.ini"), t, new UTF8Encoding(true));
                CopyImages(DataDir, exeDir);
            }
            catch (Exception e) { Log("Falha ao copiar os dados de volta: " + e.Message); }
        }
        syncHold = false;
        iniCache = null;
        dataOn = false;
        SaveLocal();
        ApplyDataPaths();
    }

    // ------------------------------------------------------------------ padrão de fábrica
    static void FactoryReset()
    {
        if (!ResolveDirty(false)) return;
        dlgWarn = true;
        dlgEscResult = 2;
        dlgPrimary = 3;
        int r = AskDialog("Restaurar padrão de fábrica",
            "Isso apaga TODOS os clips (inclusive os fixados), os atalhos por expansão e as configurações deste computador.\nSugestão: faça um backup antes.",
            "Apagar tudo", "Cancelar", "Fazer backup antes");
        dlgWarn = false;
        if (r == 3)
        {
            if (!DoBackup(true, true, true)) return;
            dlgWarn = true;
            r = AskDialog("Restaurar padrão de fábrica", "Backup feito. Apagar tudo agora e voltar ao padrão de fábrica?", "Apagar tudo", "Cancelar", null);
            dlgWarn = false;
        }
        if (r != 1) return;
        if (dataOn) DisconnectDataFolder(false); // só desliga: a pasta compartilhada não é apagada
        conflictMode = 0;
        SaveLocal();
        try
        {
            if (File.Exists(iniPath)) File.Delete(iniPath);
            if (Directory.Exists(ImgDir)) foreach (string f in Directory.GetFiles(ImgDir, "img_*.png")) File.Delete(f);
        }
        catch (Exception e) { Log("Padrão de fábrica: " + e.Message); }
        clipUndo.Clear();
        clipRedo.Clear();
        xUndo.Clear();
        xRedo.Clear();
        lastWritten = null;
        ReloadAll();
        SetStartup(true);
        startupOn = StartupEnabled();
        xSel = null;
        RepaintAll();
        Log("Padrão de fábrica restaurado");
        Info("Pronto: o ClipAtalho voltou ao padrão de fábrica.");
    }

    // ------------------------------------------------------------------ sugestões e reclamações (Formspree)
    const string FEEDBACK_URL = "https://formspree.io/f/mbgoggpz";
    static string fbPending;

    static void PaintFeedback(IntPtr hdc)
    {
        int w = Px(520);
        DrawTextAt(hdc, "Enviar sugestão / reclamação", hHeadFont, C_TEXT, Px(22), Px(12), w - Px(22), Px(42), 0x8024);
        DrawTextAt(hdc, "Seu e-mail (opcional, só se quiser resposta):", hUiFont, C_GRAY, Px(22), Px(92), w - Px(22), Px(110), 0x8024);
        RoundRect(hdc, Px(18), Px(112), Px(484), Px(30), Px(8), A_WHITE, A_BTNBORDER);
        DrawTextAt(hdc, dlgMessage.Length > 0 ? dlgMessage : "Mensagem (até 5.000 caracteres):", dlgMessage.Length > 0 ? hUiBold : hUiFont,
            dlgMessage.Length > 0 ? C_RED : C_GRAY, Px(22), Px(150), w - Px(22), Px(168), 0x8024);
        RoundRect(hdc, Px(18), Px(170), Px(484), Px(184), Px(8), A_WHITE, A_BTNBORDER);
        DrawTextAt(hdc, "Vai junto: a versão do ClipAtalho e do Windows.", hSmallFont, C_GRAY, Px(22), Px(362), Px(300), Px(400), 0x10);
        RoundRect(hdc, 0, 0, w, Px(420), Px(WIN_R), 0, A_BTNBORDER);
    }

    static void FeedbackDialog()
    {
        if (fbPending != null) { Info("Ainda estou enviando a mensagem anterior. Aguarde um instante."); return; }
        dlgLabels = new string[] { "Enviar", "Cancelar", null };
        dlgMessage = "";
        dlgTitle = "Enviar sugestão / reclamação";
        dlgResult = 0;
        dlgFeedback = true;
        dlgPrimary = 1;
        int w = Px(520), h = Px(420);
        RECT o;
        int x = (GetSystemMetrics(0) - w) / 2, y = (GetSystemMetrics(1) - h) / 2;
        if (hwndMgr != IntPtr.Zero && GetWindowRect(hwndMgr, out o)) { x = o.Left + (o.Right - o.Left - w) / 2; y = o.Top + (o.Bottom - o.Top - h) / 2; }
        hDlg = CreateWindowExW(0x10000 | 0x80000, "ClipAtalhoDialog", dlgTitle, 0x80000000 | 0x02000000, x, y, w, h, hwndMgr, IntPtr.Zero, hInst, IntPtr.Zero);
        if (hDlg == IntPtr.Zero) { dlgFeedback = false; return; }
        IntPtr me = hDlg;
        RoundWindow(me, w, h);
        AttachShade(me, NewShadeWindow(false));
        Child(me, "BUTTON", "", 0x10000 | 0xB, 22, 50, 120, 32, IDC_FB_SUG, -2);
        Child(me, "BUTTON", "", 0x10000 | 0xB, 148, 50, 120, 32, IDC_FB_REC, -2);
        hFbMail = Child(me, "EDIT", "", 0x10000 | 0x80, 28, 118, 466, 20, IDC_FB_MAIL, -2);
        SendMessageW(hFbMail, 0x00C5, (IntPtr)120, IntPtr.Zero);
        hFbMsg = Child(me, "EDIT", "", 0x00200000 | 0x10000 | 0x4 | 0x40 | 0x1000, 26, 176, 470, 172, IDC_FB_MSG, -2);
        SendMessageW(hFbMsg, 0x00C5, (IntPtr)5000, IntPtr.Zero);
        Child(me, "BUTTON", "", 0x10000 | 0xB, 296, 366, 104, 36, IDC_DLG + 1, -2);
        Child(me, "BUTTON", "", 0x10000 | 0xB, 408, 366, 94, 36, IDC_DLG + 2, -2);
        if (hwndMgr != IntPtr.Zero) EnableWindow(hwndMgr, false);
        FadeWindow(me, true);
        ShowWindow(me, 5);
        PlaceShade(me);
        SetForegroundWindow(me);
        SetFocus(hFbMsg);
        MSG msg;
        string text = "", mail = "";
        while (true)
        {
            while (dlgResult == 0)
            {
                int g = GetMessageW(out msg, IntPtr.Zero, 0, 0);
                if (g <= 0) { PostQuitMessage(0); dlgResult = 2; break; }
                if (IsDialogMessageW(me, ref msg)) continue;
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
            text = CtlText(hFbMsg).Trim();
            mail = CtlText(hFbMail).Trim();
            if (dlgResult == 1 && text.Length == 0) { dlgMessage = "Escreva a mensagem antes de enviar."; InvalidateRect(me, IntPtr.Zero, true); dlgResult = 0; SetFocus(hFbMsg); continue; }
            if (dlgResult == 1 && mail.Length > 0 && (mail.IndexOf('@') < 1 || mail.IndexOf('.') < 0 || mail.Contains(" ")))
            { dlgMessage = "Confira o e-mail (ou deixe em branco)."; InvalidateRect(me, IntPtr.Zero, true); dlgResult = 0; SetFocus(hFbMail); continue; }
            break;
        }
        int result = dlgResult;
        dlgFeedback = false;
        dlgPrimary = 3;
        if (hwndMgr != IntPtr.Zero) EnableWindow(hwndMgr, true);
        FadeOutAndWait(me);
        DropShade(me);
        winAlpha.Remove(me);
        DestroyWindow(me);
        hDlg = IntPtr.Zero;
        if (hwndMgr != IntPtr.Zero) SetForegroundWindow(hwndMgr);
        if (result == 1) SendFeedback(fbComplaint ? "Reclamação" : "Sugestão", text, mail);
    }

    static string Enc(string s) { return Uri.EscapeDataString(s ?? ""); }

    static void SendFeedback(string tipo, string text, string mail)
    {
        fbPending = tipo + "\r\n\r\n" + text;
        string body = "tipo=" + Enc(tipo) + "&mensagem=" + Enc(text) + "&versao=" + Enc(VERSION) +
            "&windows=" + Enc(Environment.OSVersion.VersionString + (Environment.Is64BitOperatingSystem ? " 64 bits" : " 32 bits")) +
            "&_subject=" + Enc("ClipAtalho " + ShortVersion() + " - " + tipo);
        if (mail.Length > 0) body += "&email=" + Enc(mail);
        ThreadPool.QueueUserWorkItem(delegate(object st)
        {
            bool ok = false;
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(FEEDBACK_URL);
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
                req.Accept = "application/json";
                req.Timeout = 20000;
                req.UserAgent = "ClipAtalho/" + ShortVersion();
                if (req.Proxy != null) req.Proxy.Credentials = CredentialCache.DefaultCredentials; // proxy da empresa
                byte[] data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse()) ok = (int)resp.StatusCode < 300;
            }
            catch (Exception e) { Log("Falha ao enviar sugestão: " + e.Message); }
            PostMessageW(hwndMain, WM_FEEDBACKDONE, (IntPtr)(ok ? 1 : 0), IntPtr.Zero);
        });
    }

    static void FeedbackDone(bool ok)
    {
        string text = fbPending;
        fbPending = null;
        if (ok) { Info("Mensagem enviada. Obrigado!"); return; }
        bool copied = text != null && SetClipboardText(text);
        Info("Não foi possível enviar agora (sem internet ou bloqueado pela rede)." +
            (copied ? "\nO texto foi copiado para a área de transferência, para você não perder o que escreveu." : ""));
    }

    // ------------------------------------------------------------------ página "Atalhos por Expansão"
    static IntPtr hXList, hXAbbr, hXText, oldXListProc;
    static WndProcDelegate xListProcRef;
    static List<string> xKeys = new List<string>();
    static string xSel;
    static bool xNew, expDirty, xDragging;
    static int xDragFrom = -1, xDragTarget = -1, xDragStartY;

    static string CtlText(IntPtr h)
    {
        int len = GetWindowTextLengthW(h);
        StringBuilder sb = new StringBuilder(len + 2);
        GetWindowTextW(h, sb, len + 1);
        return sb.ToString();
    }

    static void SetXDirty(bool dirty)
    {
        if (expDirty == dirty) return;
        expDirty = dirty;
        InvalidateRect(hwndMgr, IntPtr.Zero, true);
        InvalidateRect(GetDlgItem(hwndMgr, IDC_XSAVE), IntPtr.Zero, true);
    }

    static void LoadX()
    {
        string v;
        if (xSel == null || !cfgAbbr.TryGetValue(xSel, out v)) v = "";
        mgrLoading = true;
        SetWindowTextW(hXAbbr, xSel ?? "");
        SetWindowTextW(hXText, v.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        mgrLoading = false;
        SetXDirty(false);
        InvalidateRect(GetDlgItem(hwndMgr, IDC_XPIN), IntPtr.Zero, true);
    }

    // reloadFields=false preserva o que está sendo digitado (usado ao reordenar/fixar).
    static void RefreshX(bool reloadFields)
    {
        if (hwndMgr == IntPtr.Zero || !IsWindowVisible(hwndMgr)) return;
        xKeys = new List<string>(cfgAbbrOrder);
        if (xSel != null && !cfgAbbr.ContainsKey(xSel)) xSel = null;
        if (xSel == null && !xNew && xKeys.Count > 0) xSel = xKeys[0];
        mgrLoading = true;
        SendMessageW(hXList, 0x000B, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW off
        int top = SendMessageW(hXList, 0x018E, IntPtr.Zero, IntPtr.Zero).ToInt32(); // LB_GETTOPINDEX
        SendMessageW(hXList, 0x0184, IntPtr.Zero, IntPtr.Zero); // LB_RESETCONTENT
        for (int i = 0; i < xKeys.Count; i++) SendMessageStr(hXList, 0x0180, IntPtr.Zero, "");
        if (top > 0 && top < xKeys.Count) SendMessageW(hXList, 0x0197, (IntPtr)top, IntPtr.Zero); // LB_SETTOPINDEX
        SendMessageW(hXList, 0x0186, (IntPtr)(xSel == null ? -1 : xKeys.IndexOf(xSel)), IntPtr.Zero); // LB_SETCURSEL
        SendMessageW(hXList, 0x000B, (IntPtr)1, IntPtr.Zero);
        mgrLoading = false;
        InvalidateRect(hXList, IntPtr.Zero, true);
        InvalidateRect(GetDlgItem(hwndMgr, IDC_XPIN), IntPtr.Zero, true);
        if (reloadFields || !expDirty) LoadX();
    }

    static string EscAbbr(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    // Regrava a seção [atalhos] do .ini (na ordem dada) e recarrega.
    static void WriteAbbr(List<string> order, Dictionary<string, string> d, List<string> loose)
    {
        if (!xUndoing) PushXUndo();
        List<string> body = new List<string>();
        body.Add("; atalho=texto   (cadastre e edite pela tela \"Atalhos por Expansão\")");
        body.Add("; No texto, \\n representa quebra de linha. Linha iniciada por - é um atalho não fixado.");
        foreach (string k in order)
        {
            if (!d.ContainsKey(k)) continue;
            body.Add((loose.Contains(k) ? "-" : "") + k + "=" + (d[k].StartsWith("@") ? d[k] : EscAbbr(d[k])));
        }
        ReplaceIniSection("atalhos", body);
        List<string> dates = new List<string>();
        dates.Add("; data de criação de cada atalho (usada em \"Reorganizar lista\")");
        foreach (string k in order)
            if (d.ContainsKey(k) && cfgAbbrDate.ContainsKey(k)) dates.Add(k + "=" + (cfgAbbrEst.Contains(k) ? "~" : "") + cfgAbbrDate[k].ToString("yyyyMMddHHmmss"));
        ReplaceIniSection("atalhos_criacao", dates);
        LoadConfig();
    }

    static void ReplaceIniSection(string section, List<string> body)
    {
        try
        {
            string text = ReadIni() ?? DefaultIni;
            List<string> lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            if (lines.Count > 0) lines[0] = lines[0].TrimStart((char)0xFEFF);
            int start = -1, end = -1, clipsAt = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (!(t.StartsWith("[") && t.EndsWith("]"))) continue;
                string name = t.Substring(1, t.Length - 2).Trim().ToLowerInvariant();
                if (start >= 0 && end < 0) end = i;
                if (name == section && start < 0) start = i;
                if (name == "clips" && clipsAt < 0) clipsAt = i;
            }
            List<string> block = new List<string>();
            block.Add("[" + section + "]");
            block.AddRange(body);
            block.Add("");
            if (start >= 0)
            {
                if (end < 0) end = lines.Count;
                lines.RemoveRange(start, end - start);
                lines.InsertRange(start, block);
            }
            else lines.InsertRange(clipsAt >= 0 ? clipsAt : lines.Count, block);
            WriteIni(string.Join("\r\n", lines.ToArray()));
        }
        catch (Exception e) { Log("FALHA ao gravar a seção [" + section + "] no .ini: " + e.Message); }
    }

    // asNew = "Salvar Novo": grava os campos como um atalho novo e deixa o atalho original como estava.
    static bool SaveX(bool asNew)
    {
        string abbr = CtlText(hXAbbr).Trim().ToLowerInvariant();
        string text = CtlText(hXText);
        if (!ValidAbbr(abbr))
        {
            Info("O atalho deve ter só letras sem acento e números (até 32), sem espaços.\nExemplos: bn1, prst, obrigado");
            SetFocus(hXAbbr);
            return false;
        }
        if (text.Trim().Length == 0)
        {
            Info("Digite o texto que o atalho deve inserir.");
            SetFocus(hXText);
            return false;
        }
        string orig = asNew ? null : xSel;
        if (abbr != orig && cfgAbbr.ContainsKey(abbr))
        {
            Info((asNew && abbr == xSel)
                ? "Para salvar como novo, troque o nome do atalho: \"" + abbr + "\" já é o nome deste atalho."
                : "Atalho já em uso: \"" + abbr + "\". Escolha outro nome.");
            SetFocus(hXAbbr);
            return false;
        }
        List<string> order = new List<string>(cfgAbbrOrder);
        Dictionary<string, string> d = new Dictionary<string, string>(cfgAbbr);
        List<string> loose = new List<string>(cfgAbbrLoose);
        int at = orig == null ? -1 : order.IndexOf(orig);
        if (at >= 0)
        {
            order[at] = abbr; // mantém a posição, o estado fixado e a data ao renomear
            d.Remove(orig);
            if (loose.Remove(orig)) loose.Add(abbr);
            DateTime dt;
            if (abbr != orig && cfgAbbrDate.TryGetValue(orig, out dt)) { cfgAbbrDate.Remove(orig); cfgAbbrDate[abbr] = dt; }
            if (abbr != orig && cfgAbbrEst.Remove(orig)) cfgAbbrEst.Add(abbr);
        }
        else
        {
            int after = (asNew && xSel != null) ? order.IndexOf(xSel) : -1;
            if (after >= 0) order.Insert(after + 1, abbr); // "Salvar Novo": entra logo abaixo do original
            else order.Add(abbr);
            cfgAbbrDate[abbr] = DateTime.Now;
        }
        d[abbr] = text;
        WriteAbbr(order, d, loose);
        xSel = abbr;
        xNew = false;
        RefreshX(true);
        return true;
    }

    // Aviso em vermelho ao lado do campo do atalho.
    static string XAbbrWarning()
    {
        if (hXAbbr == IntPtr.Zero) return null;
        string a = CtlText(hXAbbr).Trim().ToLowerInvariant();
        if (a.Length == 0) return null;
        if (!ValidAbbr(a)) return "Só letras e números";
        if (a != xSel && cfgAbbr.ContainsKey(a)) return "Atalho já em uso";
        return null;
    }

    static void InvalidateXWarning()
    {
        RECT r = new RECT();
        r.Left = Px(586);
        r.Top = Px(104);
        r.Right = Px(800);
        r.Bottom = Px(144);
        InvalidateRectArea(hwndMgr, ref r, true);
    }

    // ---- reorganizar a lista de atalhos
    const int SORT_OLD = 170, SORT_NEW = 171, SORT_AZ = 172, SORT_ZA = 173, SORT_FAMILY = 174, SORT_UNDO = 175;
    static List<string> xUndoOrder;

    // Ordem "natural": end2 vem antes de end10.
    static int NaturalCompare(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                string na = a.Substring(si, i - si).TrimStart('0'), nb = b.Substring(sj, j - sj).TrimStart('0');
                if (na.Length != nb.Length) return na.Length - nb.Length;
                int c = string.CompareOrdinal(na, nb);
                if (c != 0) return c;
            }
            else
            {
                if (a[i] != b[j]) return a[i] - b[j];
                i++;
                j++;
            }
        }
        return (a.Length - i) - (b.Length - j);
    }

    // Família = mesmo prefixo de 2 caracteres (end1…end7; prst, prdbz1, pr3d).
    static string FamilyKey(string k) { return k.Length >= 2 ? k.Substring(0, 2) : k; }

    static DateTime XDate(string k)
    {
        DateTime dt;
        return cfgAbbrDate.TryGetValue(k, out dt) ? dt : DateTime.MinValue; // sem data = criado antes da V14
    }

    static void StableSort(List<string> list, Comparison<string> cmp)
    {
        Dictionary<string, int> pos = new Dictionary<string, int>();
        for (int i = 0; i < list.Count; i++) pos[list[i]] = i;
        list.Sort(delegate(string a, string b) { int r = cmp(a, b); return r != 0 ? r : pos[a] - pos[b]; });
    }

    static void XReorganize(int mode)
    {
        if (!ResolveDirty(false)) return;
        List<string> cur = new List<string>(cfgAbbrOrder);
        List<string> res;
        if (mode == SORT_UNDO)
        {
            if (xUndoOrder == null) return;
            res = new List<string>();
            foreach (string k in xUndoOrder) if (cfgAbbr.ContainsKey(k)) res.Add(k);
            foreach (string k in cur) if (!res.Contains(k)) res.Add(k); // atalhos criados depois
            xUndoOrder = null;
        }
        else if (mode == SORT_FAMILY)
        {
            // junta os membros de cada família, mantendo as famílias na ordem em que aparecem
            List<string> fams = new List<string>();
            Dictionary<string, List<string>> g = new Dictionary<string, List<string>>();
            foreach (string k in cur)
            {
                string f = FamilyKey(k);
                if (!g.ContainsKey(f)) { g[f] = new List<string>(); fams.Add(f); }
                g[f].Add(k);
            }
            res = new List<string>();
            foreach (string f in fams)
            {
                StableSort(g[f], NaturalCompare);
                res.AddRange(g[f]);
            }
            xUndoOrder = cur;
        }
        else
        {
            res = new List<string>(cur);
            if (mode == SORT_OLD) StableSort(res, delegate(string a, string b) { return XDate(a).CompareTo(XDate(b)); });
            else if (mode == SORT_NEW) StableSort(res, delegate(string a, string b) { return XDate(b).CompareTo(XDate(a)); });
            else if (mode == SORT_AZ) StableSort(res, NaturalCompare);
            else StableSort(res, delegate(string a, string b) { return NaturalCompare(b, a); });
            xUndoOrder = cur;
        }
        WriteAbbr(res, cfgAbbr, cfgAbbrLoose);
        RefreshX(false);
    }

    // Atalhos sem data (criados antes da V14) recebem uma data estimada, na ordem atual da lista:
    // ficam mais antigos que qualquer atalho com data real, e o primeiro da lista conta como o mais antigo.
    static void EnsureAbbrDates()
    {
        List<string> missing = new List<string>();
        foreach (string k in cfgAbbrOrder) if (!cfgAbbrDate.ContainsKey(k)) missing.Add(k);
        if (missing.Count == 0) return;
        DateTime oldest = DateTime.Now;
        foreach (DateTime d in cfgAbbrDate.Values) if (d < oldest) oldest = d;
        DateTime b = oldest.AddSeconds(-missing.Count - 1);
        for (int i = 0; i < missing.Count; i++)
        {
            cfgAbbrDate[missing[i]] = b.AddSeconds(i);
            if (!cfgAbbrEst.Contains(missing[i])) cfgAbbrEst.Add(missing[i]);
        }
        WriteAbbr(new List<string>(cfgAbbrOrder), new Dictionary<string, string>(cfgAbbr), new List<string>(cfgAbbrLoose));
        Log("Datas estimadas para " + missing.Count + " atalho(s) sem data de criação");
    }

    static void XSortMenu()
    {
        List<MI> mi = new List<MI>();
        mi.Add(Item(SORT_OLD, "Por data de criação: mais antigos primeiro"));
        mi.Add(Item(SORT_NEW, "Por data de criação: mais recentes primeiro"));
        mi.Add(Sep());
        mi.Add(Item(SORT_AZ, "Ordem alfabética: A → Z"));
        mi.Add(Item(SORT_ZA, "Ordem alfabética: Z → A"));
        mi.Add(Sep());
        mi.Add(Item(SORT_FAMILY, "Agrupar famílias (prefixos)"));
        mi.Add(Sep());
        MI undo = Item(SORT_UNDO, "Desfazer a última reorganização");
        undo.Off = xUndoOrder == null;
        mi.Add(undo);
        RECT rc;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_XSORT), out rc);
        int cmd = ShowMenu(mi, rc.Left, rc.Bottom + Px(4), hwndMgr);
        if (cmd >= SORT_OLD && cmd <= SORT_UNDO) XReorganize(cmd);
    }

    static void XMove(int from, int to)
    {
        List<string> order = new List<string>(cfgAbbrOrder);
        if (from < 0 || from >= order.Count) { RefreshX(false); return; }
        int dest = to > from ? to - 1 : to;
        dest = Math.Max(0, Math.Min(order.Count - 1, dest));
        if (dest != from)
        {
            string k = order[from];
            order.RemoveAt(from);
            order.Insert(dest, k);
            WriteAbbr(order, cfgAbbr, cfgAbbrLoose);
        }
        RefreshX(false);
    }

    static void DrawXItem(DRAWITEMSTRUCT d)
    {
        FillRect(d.hDC, ref d.rcItem, hBgBrush);
        int i = (int)d.itemID;
        if (i < 0 || i >= xKeys.Count) return;
        string v;
        if (!cfgAbbr.TryGetValue(xKeys[i], out v)) v = "";
        bool sel = (d.itemState & 0x1) != 0, loose = cfgAbbrLoose.Contains(xKeys[i]);
        int x = d.rcItem.Left + Px(2), y = d.rcItem.Top + Px(3);
        int w = d.rcItem.Right - d.rcItem.Left - Px(8), h = d.rcItem.Bottom - d.rcItem.Top - Px(6);
        RoundRect(d.hDC, x, y, w, h, Px(9), sel ? A_SEL : A_WHITE, sel ? A_ACCENT : A_BORDER);
        int bw = Px(84), bh = Px(26), bx = x + Px(10), by = y + (h - bh) / 2;
        RoundRect(d.hDC, bx, by, bw, bh, Px(8), A_ACCENT, 0);
        DrawTextAt(d.hDC, xKeys[i], hUiBold, C_WHITE, bx + Px(6), by, bx + bw - Px(6), by + bh, 0x8025);
        int tx = bx + bw + Px(10), tr = x + w - Px(10);
        DrawTextAt(d.hDC, Preview(v, 90), hUiFont, C_TEXT, tx, y + Px(7), tr, y + Px(28), 0x8024);
        DrawTextAt(d.hDC, (loose ? "Não fixado" : "★ Fixado") + "  ·  " +
            (v.StartsWith("@") ? "texto no arquivo " + v.Substring(1) : v.Length + " caracteres") +
            (cfgAbbrDate.ContainsKey(xKeys[i]) ? "  ·  " + (cfgAbbrEst.Contains(xKeys[i]) ? "antes de " : "") + cfgAbbrDate[xKeys[i]].ToString("dd/MM/yy") : ""),
            hSmallFont, loose ? C_GRAY : C_ACCENT, tx, y + Px(28), tr, y + h - Px(6), 0x8024);

        if (xDragging) // marca onde o atalho arrastado vai entrar
        {
            RECT line = d.rcItem;
            if (i == xDragTarget) { line.Bottom = line.Top + Px(3); FillRect(d.hDC, ref line, hAccentBrush); }
            else if (xDragTarget >= xKeys.Count && i == xKeys.Count - 1) { line.Top = line.Bottom - Px(3); FillRect(d.hDC, ref line, hAccentBrush); }
        }
    }

    static int XItemAt(IntPtr lParam)
    {
        long r = CallWindowProcW(oldXListProc, hXList, 0x01A9, IntPtr.Zero, lParam).ToInt64(); // LB_ITEMFROMPOINT
        if (((r >> 16) & 0xFFFF) != 0) return -1;
        int idx = (int)(r & 0xFFFF);
        return idx < xKeys.Count ? idx : -1;
    }

    static void XSelectKey(string key)
    {
        if (!ResolveDirty(false) || !cfgAbbr.ContainsKey(key)) return;
        xSel = key;
        xNew = false;
        RefreshX(true);
    }

    // Mouse da lista de atalhos: arrastar para reordenar e botão direito para o menu (igual à lista de clips).
    static IntPtr XListProc(IntPtr hwnd, uint m, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
            switch (m)
            {
                case 0x0201: // WM_LBUTTONDOWN
                {
                    int idx = XItemAt(lParam);
                    if (expDirty && idx >= 0 && xKeys[idx] != xSel)
                    {
                        XSelectKey(xKeys[idx]); // pergunta antes de trocar; o clique original é descartado
                        return IntPtr.Zero;
                    }
                    xDragFrom = idx;
                    xDragStartY = y;
                    xDragTarget = -1;
                    xDragging = false;
                    break;
                }
                case 0x0200: // WM_MOUSEMOVE
                    if ((wParam.ToInt64() & 1) != 0 && xDragFrom >= 0)
                    {
                        if (!xDragging && Math.Abs(y - xDragStartY) > Px(6)) xDragging = true;
                        if (xDragging)
                        {
                            RECT rc;
                            GetClientRect(hwnd, out rc);
                            int top = CallWindowProcW(oldXListProc, hwnd, 0x018E, IntPtr.Zero, IntPtr.Zero).ToInt32();
                            if (y < 0 && top > 0) CallWindowProcW(oldXListProc, hwnd, 0x0197, (IntPtr)(--top), IntPtr.Zero);
                            else if (y > rc.Bottom)
                            {
                                CallWindowProcW(oldXListProc, hwnd, 0x0197, (IntPtr)(top + 1), IntPtr.Zero);
                                top = CallWindowProcW(oldXListProc, hwnd, 0x018E, IntPtr.Zero, IntPtr.Zero).ToInt32();
                            }
                            int ih = Px(58);
                            int yy = Math.Max(0, Math.Min(rc.Bottom, y));
                            int target = Math.Max(0, Math.Min(xKeys.Count, top + (yy + ih / 2) / ih));
                            if (target != xDragTarget) // só redesenha quando o destino muda
                            {
                                xDragTarget = target;
                                InvalidateRect(hwnd, IntPtr.Zero, false);
                            }
                            return IntPtr.Zero;
                        }
                    }
                    break;
                case 0x0202: // WM_LBUTTONUP
                {
                    bool was = xDragging;
                    int from = xDragFrom, to = xDragTarget;
                    xDragging = false;
                    xDragFrom = -1;
                    IntPtr res = CallWindowProcW(oldXListProc, hwnd, m, wParam, lParam);
                    if (was) XMove(from, to);
                    return res;
                }
                case 0x0215: // WM_CAPTURECHANGED
                    if (xDragging) { xDragging = false; xDragFrom = -1; InvalidateRect(hwnd, IntPtr.Zero, false); }
                    break;
                case 0x0204: // WM_RBUTTONDOWN: seleciona o atalho sob o ponteiro
                {
                    int idx = XItemAt(lParam);
                    if (idx >= 0 && xKeys[idx] != xSel) XSelectKey(xKeys[idx]);
                    return IntPtr.Zero;
                }
                case 0x0205: // WM_RBUTTONUP: menu do atalho
                {
                    int idx = XItemAt(lParam);
                    if (idx < 0 || xKeys[idx] != xSel) return IntPtr.Zero;
                    List<MI> mi = new List<MI>();
                    mi.Add(Item(IDC_XPIN, cfgAbbrLoose.Contains(xSel) ? "★  Fixar" : "☆  Soltar (deixar de fixar)"));
                    mi.Add(Item(IDC_XUP, "↑  Subir"));
                    mi.Add(Item(IDC_XDOWN, "↓  Descer"));
                    mi.Add(Sep());
                    mi.Add(Item(IDC_XDEL, "Excluir"));
                    POINT pt;
                    GetCursorPos(out pt);
                    int cmd = ShowMenu(mi, pt.X, pt.Y, hwndMgr);
                    if (cmd != 0) ManagerCommand(cmd, 0);
                    return IntPtr.Zero;
                }
            }
        }
        catch { }
        return CallWindowProcW(oldXListProc, hwnd, m, wParam, lParam);
    }

    static int AutoDelIndex() { int i = Array.IndexOf(AutoDelMinutes, cfgAutoDel); return i < 0 ? 0 : i; }

    // Apaga sozinho os clips não fixados, conforme o tempo escolhido em Configurações.
    static void RunAutoDelete()
    {
        if (cfgAutoDel == 0) return;
        DateTime now = DateTime.Now;
        int removed;
        if (cfgAutoDel < 0)
        {
            if (now.Date == autoDelDay) return;
            autoDelDay = now.Date;
            PushClipUndo();
            removed = clips.RemoveAll(delegate(Clip c) { return !c.Pinned; });
        }
        else
        {
            if (!clips.Exists(delegate(Clip c) { return !c.Pinned && (now - c.Added).TotalMinutes >= cfgAutoDel; })) return;
            PushClipUndo();
            removed = clips.RemoveAll(delegate(Clip c) { return !c.Pinned && (now - c.Added).TotalMinutes >= cfgAutoDel; });
        }
        if (removed == 0) return;
        Log("Autoexclusão: " + removed + " clip(s) não fixado(s) apagado(s)");
        SaveClips();
        RefreshList();
        RefreshPopupList();
    }

    static void ClearUnpinned()
    {
        if (hwndMgr != IntPtr.Zero && IsWindowVisible(hwndMgr))
        {
            if (!ResolveDirty(false)) return;
            if (AskDialog("Limpar lista", "Apagar todos os clips não fixados? Os fixados (★) são mantidos.", "Apagar", "Cancelar", null) != 1) return;
        }
        PushClipUndo();
        clips.RemoveAll(delegate(Clip c) { return !c.Pinned; });
        SaveClips();
        RefreshList();
        RefreshPopupList();
    }

    static string ButtonLabel(int id)
    {
        switch (id)
        {
            case IDC_UP: return "↑";
            case IDC_DOWN: return "↓";
            case IDC_CLEAR: return "Limpar não fixados";
            case IDC_DELPIN: return "Apagar Fixados";
            case IDC_SAVE: return "Salvar";
            case IDC_NEW: return "+  Novo";
            case IDC_PIN: return (mgrSel != null && mgrSel.Pinned) ? "☆  Soltar" : "★  Fixar";
            case IDC_DEL: return "Excluir";
            case IDC_NAV: return "Clips";
            case IDC_NAV + 1: return "Atalhos por Expansão";
            case IDC_NAV + 2: return "Configurações";
            case IDC_NAV + 3: return "Ajuda";
            case IDC_XSAVE: return "Salvar";
            case IDC_XSAVENEW: return "Salvar Novo";
            case IDC_XSORT: return "Reorganizar lista  ▼";
            case IDC_XNEW: return "+  Novo";
            case IDC_XUP: return "↑";
            case IDC_XDOWN: return "↓";
            case IDC_XCLEAR: return "Limpar não fixados";
            case IDC_XPIN: return (xSel != null && cfgAbbrLoose.Contains(xSel)) ? "★  Fixar" : "☆  Soltar";
            case IDC_THEME: return "Claro";
            case IDC_THEME + 1: return "Escuro";
            case IDC_XDEL: return "Excluir";
            case IDC_T_STARTUP: return packaged ? "Iniciar junto com o Windows (no app da loja: ajuste em Configurações do Windows › Aplicativos › Inicialização)" : "Iniciar junto com o Windows";
            case IDC_T_SAVEALL: return "Guardar também os clips não fixados (se desligado, só os fixados ficam salvos ao fechar)";
            case IDC_T_IMAGES: return "Clips de imagem: guardar também imagens copiadas (prints, figuras), como no Win+V";
            case IDC_T_EXPAND: return "Expansão de texto: digitar um atalho e apertar Tab (cadastre na tela Atalhos por Expansão)";
            case IDC_SEG: return "Geral";
            case IDC_SEG + 1: return "Dados e backup";
            case IDC_BK_CLIPS: return "Backup dos clips";
            case IDC_BK_X: return "Backup das expansões";
            case IDC_BK_ALL: return "Backup completo";
            case IDC_RESTOREBK: return "Restaurar backup…";
            case IDC_AUTODEL: return AutoDelNames[AutoDelIndex()] + "   ▼";
            case IDC_T_DATADIR: return "Usar uma pasta de dados (rede, OneDrive, Google Drive…) — os mesmos clips e atalhos em vários computadores";
            case IDC_DATADIR: return "Escolher pasta…";
            case IDC_CONFLICT: return ConflictNames[conflictMode] + "   ▼";
            case IDC_FACTORY: return "Restaurar padrão de fábrica";
            case IDC_FEEDBACK: return "Enviar sugestão / reclamação";
            case IDC_FB_SUG: return "Sugestão";
            case IDC_FB_REC: return "Reclamação";
            case IDC_OPENINI: return "Abrir o arquivo .ini";
            case IDC_OPENDIR: return "Abrir a pasta do programa";
            case IDC_INSTA: return "@";
        }
        if (id >= IDC_MOD && id < IDC_MOD + 4) return ModNames[id - IDC_MOD];
        if (id > IDC_DLG && id <= IDC_DLG + 3) return dlgLabels[id - IDC_DLG - 1] ?? "";
        return "";
    }

    static void DrawButton(DRAWITEMSTRUCT d)
    {
        int id = (int)d.CtlID;
        bool down = (d.itemState & 0x1) != 0, focus = (d.itemState & 0x10) != 0;
        bool onCard = id >= IDC_MOD && id != IDC_DELPIN && id != IDC_XSAVENEW && id != IDC_XSORT && id != IDC_CLOSE && id != IDC_SEG && id != IDC_SEG + 1 && !(id >= IDC_XSAVE && id <= IDC_XCLEAR); // dentro de cartão ou sobre o fundo
        FillRect(d.hDC, ref d.rcItem, onCard ? hWhiteBrush : hBgBrush);
        int l = d.rcItem.Left, t = d.rcItem.Top, w = d.rcItem.Right - d.rcItem.Left, h = d.rcItem.Bottom - d.rcItem.Top;

        if (id == IDC_UNDO || id == IDC_REDO || id == IDC_XUNDO || id == IDC_XREDO) // desfazer / refazer
        {
            bool can = id == IDC_UNDO ? clipUndo.Count > 0 : id == IDC_REDO ? clipRedo.Count > 0 : id == IDC_XUNDO ? xUndo.Count > 0 : xRedo.Count > 0;
            if (can) RoundRect(d.hDC, l, t, w, h, Px(7), down ? A_DOWN : A_WHITE, A_BTNBORDER);
            DrawTextAt(d.hDC, (id == IDC_UNDO || id == IDC_XUNDO) ? "\uE7A7" : "\uE7A6", hIconFont, can ? C_ACCENT : C_LINE, l, t, l + w, t + h, 0x25);
            return;
        }
        if ((id >= IDC_T_STARTUP && id <= IDC_T_EXPAND) || id == IDC_T_IMAGES || id == IDC_T_DATADIR) // chave liga/desliga
        {
            bool on = id == IDC_T_DATADIR ? dataOn : id == IDC_T_IMAGES ? cfgImages : (id == IDC_T_STARTUP ? startupOn : (id == IDC_T_SAVEALL ? cfgSaveAll : cfgExpand));
            int tw = Px(40), th = Px(22), ty = t + (h - th) / 2, knob = Px(16);
            RoundRect(d.hDC, l, ty, tw, th, th / 2, on ? A_ACCENT : A_BTNBORDER, 0);
            RoundRect(d.hDC, on ? l + tw - knob - Px(3) : l + Px(3), ty + (th - knob) / 2, knob, knob, knob / 2, 0xFFFFFFFF, 0);
            DrawTextAt(d.hDC, ButtonLabel(id), hUiFont, C_TEXT, l + tw + Px(12), t, l + w, t + h, 0x8024);
            return;
        }

        if (id >= IDC_COLOR && id < IDC_COLOR + ColorRgb.Length) // amostra de cor (a escolhida ganha um anel)
        {
            int ci = id - IDC_COLOR;
            uint c = 0xFF000000 | ColorRgb[ci];
            if (ci == cfgColor)
            {
                RoundRect(d.hDC, l, t, w, h, w / 2, A_WHITE, A_TEXT);
                RoundRect(d.hDC, l + Px(5), t + Px(5), w - Px(10), h - Px(10), (w - Px(10)) / 2, c, 0);
            }
            else RoundRect(d.hDC, l + Px(3), t + Px(3), w - Px(6), h - Px(6), (w - Px(6)) / 2, c, 0);
            return;
        }
        if (id == IDC_CLOSE) // botão X arredondado
        {
            RoundRect(d.hDC, l, t, w, h, Px(9), down ? A_DOWN : A_WHITE, focus ? A_ACCENT : A_BORDER);
            DrawTextAt(d.hDC, "✕", hUiFont, down ? C_RED : C_GRAY, l, t, l + w, t + h, 0x25);
            return;
        }
        if (id == IDC_INSTA) // ícone que abre o perfil
        {
            RoundRect(d.hDC, l + Px(2), t + Px(2), w - Px(4), h - Px(4), Px(11), down ? A_ACCENT_DOWN : A_ACCENT, 0);
            DrawTextAt(d.hDC, "@", hHeadFont, C_WHITE, l, t - Px(2), l + w, t + h - Px(2), 0x25);
            return;
        }

        uint fill = down ? A_DOWN : A_WHITE, border = focus ? A_ACCENT : A_BTNBORDER, color = C_TEXT;
        bool primary = false;
        if (id == IDC_SAVE || id == IDC_XSAVE)
        {
            if (id == IDC_SAVE ? editDirty : expDirty) primary = true;
            else { fill = A_DOWN; border = 0; color = C_GRAY; }
        }
        else if (id == IDC_DLG + dlgPrimary) primary = true;
        else if (id >= IDC_NAV && id < IDC_NAV + 4)
        {
            bool active = page == id - IDC_NAV || (page == 4 && id == IDC_NAV + 2);
            fill = active ? A_SEL : (down ? A_DOWN : A_BG);
            border = active ? A_ACCENT : 0;
            color = active ? C_ACCENT : C_GRAY;
        }
        else if (id == IDC_SEG || id == IDC_SEG + 1)
        {
            bool active = page == (id == IDC_SEG ? 2 : 4);
            fill = active ? A_SEL : (down ? A_DOWN : A_BG);
            border = active ? A_ACCENT : A_BTNBORDER;
            color = active ? C_ACCENT : C_GRAY;
        }
        else if (id == IDC_FB_SUG || id == IDC_FB_REC) primary = (id == IDC_FB_REC) == fbComplaint;
        else if (id == IDC_FEEDBACK) { color = C_ACCENT; border = A_ACCENT; }
        else if (id >= IDC_MOD && id < IDC_MOD + 4) primary = (cfgMods & ModBits[id - IDC_MOD]) != 0;
        else if (id == IDC_THEME || id == IDC_THEME + 1) primary = (id == IDC_THEME + 1) == cfgDark;
        else if (id == IDC_DEL || id == IDC_XDEL || (id == IDC_DLG + 1 && dlgWarn)) color = C_RED;
        if (id == IDC_DELPIN || id == IDC_FACTORY) // botão vermelho
        {
            fill = down ? 0xFFB91C1C : 0xFFDC2626;
            border = 0;
            color = C_WHITE;
        }
        else if (primary)
        {
            fill = down ? A_ACCENT_DOWN : A_ACCENT;
            border = 0;
            color = C_WHITE;
        }
        RoundRect(d.hDC, l, t, w, h, Px(9), fill, border);
        DrawTextAt(d.hDC, ButtonLabel(id), hUiBold, color, l, t, l + w, t + h, 0x25);
    }

    // Desenha o item em memória e copia de uma vez para a tela (evita piscar ao arrastar/atualizar).
    static void Buffered(DRAWITEMSTRUCT d, Action<DRAWITEMSTRUCT> draw)
    {
        int w = d.rcItem.Right - d.rcItem.Left, h = d.rcItem.Bottom - d.rcItem.Top;
        IntPtr mem = (w > 0 && h > 0) ? CreateCompatibleDC(d.hDC) : IntPtr.Zero;
        IntPtr bmp = mem != IntPtr.Zero ? CreateCompatibleBitmap(d.hDC, w, h) : IntPtr.Zero;
        if (bmp == IntPtr.Zero)
        {
            if (mem != IntPtr.Zero) DeleteDC(mem);
            draw(d);
            return;
        }
        IntPtr old = SelectObject(mem, bmp);
        DRAWITEMSTRUCT b = d;
        b.hDC = mem;
        b.rcItem.Left = 0;
        b.rcItem.Top = 0;
        b.rcItem.Right = w;
        b.rcItem.Bottom = h;
        draw(b);
        BitBlt(d.hDC, d.rcItem.Left, d.rcItem.Top, w, h, mem, 0, 0, 0x00CC0020); // SRCCOPY
        SelectObject(mem, old);
        DeleteObject(bmp);
        DeleteDC(mem);
    }

    static void DrawListItem(DRAWITEMSTRUCT d)
    {
        FillRect(d.hDC, ref d.rcItem, hBgBrush);
        int i = (int)d.itemID;
        if (i < 0 || i >= clips.Count) return;
        Clip c = clips[i];
        bool sel = (d.itemState & 0x1) != 0;
        int x = d.rcItem.Left + Px(2), y = d.rcItem.Top + Px(3);
        int w = d.rcItem.Right - d.rcItem.Left - Px(8), h = d.rcItem.Bottom - d.rcItem.Top - Px(6);
        RoundRect(d.hDC, x, y, w, h, Px(9), sel ? A_SEL : A_WHITE, sel ? A_ACCENT : A_BORDER);
        int bs = Px(28), bx = x + Px(11), by = y + (h - bs) / 2;
        RoundRect(d.hDC, bx, by, bs, bs, Px(8), i < 9 ? A_ACCENT : A_BORDER, 0);
        DrawTextAt(d.hDC, i < 9 ? (i + 1).ToString() : "–", hUiBold, i < 9 ? C_WHITE : C_GRAY, bx, by, bx + bs, by + bs, 0x25);
        int tx = bx + bs + Px(11), tr = x + w - Px(10);
        if (c.IsImage) // miniatura + dimensões
        {
            int tw = Px(72), th = h - Px(12);
            RoundRect(d.hDC, tx, y + Px(6), tw, th, Px(6), A_BG, A_BORDER);
            DrawImageFit(d.hDC, ThumbOf(c), tx + Px(3), y + Px(9), tw - Px(6), th - Px(6));
            int lx = tx + tw + Px(10);
            DrawTextAt(d.hDC, c.Image == IntPtr.Zero ? "Imagem (não foi possível abrir)" : "Imagem", hUiFont, C_TEXT, lx, y + Px(7), tr, y + Px(28), 0x8024);
            DrawTextAt(d.hDC, (c.Pinned ? "★ Fixado" : "Recente") + "  ·  " + c.W + " × " + c.H + " px",
                hSmallFont, c.Pinned ? C_ACCENT : C_GRAY, lx, y + Px(28), tr, y + h - Px(6), 0x8024);
        }
        else
        {
            DrawTextAt(d.hDC, c.Text.Length == 0 ? "(vazio — digite o texto e clique em Salvar)" : Preview(c.Text, 90),
                hUiFont, c.Text.Length == 0 ? C_GRAY : C_TEXT, tx, y + Px(7), tr, y + Px(28), 0x8024);
            DrawTextAt(d.hDC, (c.Pinned ? "★ Fixado" : "Recente") + "  ·  " + c.Text.Length + " caracteres",
                hSmallFont, c.Pinned ? C_ACCENT : C_GRAY, tx, y + Px(28), tr, y + h - Px(6), 0x8024);
        }

        if (dragging) // marca onde o clip arrastado vai entrar
        {
            RECT line = d.rcItem;
            if (i == dragTarget) { line.Bottom = line.Top + Px(3); FillRect(d.hDC, ref line, hAccentBrush); }
            else if (dragTarget >= clips.Count && i == clips.Count - 1) { line.Top = line.Bottom - Px(3); FillRect(d.hDC, ref line, hAccentBrush); }
        }
    }

    static void PaintManager(IntPtr hwnd)
    {
        PAINTSTRUCT ps;
        IntPtr hdc = BeginPaint(hwnd, out ps);
        DrawAppIcon(hdc, Px(18), Px(8), Px(16)); // nome do programa sempre visível
        DrawTextAt(hdc, "ClipAtalho", hUiBold, C_ACCENT, Px(40), Px(4), Px(336), Px(28), 0x24);
        DrawTextAt(hdc, PageTitles[page], hTitleFont, C_TEXT, Px(18), Px(22), Px(336), Px(52), 0x24);
        if (page == 0)
        {
            DrawTextAt(hdc, cfgModsLabel + "+1…9 cola o clip da posição  ·  arraste para reordenar  ·  botão direito para mais opções" +
                (paused ? "  ·  PAUSADO" : ""), hUiFont, paused ? C_RED : C_GRAY, Px(18), Px(50), Px(804), Px(72), 0x8024);
            RoundRect(hdc, Px(362), Px(78), Px(442), Px(400), Px(11), A_WHITE, A_BORDER);
            if (mgrSel != null && mgrSel.IsImage) // prévia da imagem no lugar do editor
            {
                DrawTextAt(hdc, "Clip de imagem  ·  não editável  ·  cole com o atalho ou pela lista rápida",
                    hSmallFont, C_GRAY, Px(376), Px(86), Px(726), Px(108), 0x8024);
                DrawImageFit(hdc, mgrSel.Image, Px(376), Px(114), Px(414), Px(322));
                DrawTextAt(hdc, mgrSel.W + " × " + mgrSel.H + " px", hSmallFont, C_GRAY, Px(376), Px(446), Px(790), Px(470), 0x25);
            }
            else
                DrawTextAt(hdc, editDirty ? "Edição não salva — clique em Salvar para valer" : "Texto do clip selecionado  ·  edite e clique em Salvar",
                    hSmallFont, editDirty ? C_RED : C_GRAY, Px(376), Px(86), Px(726), Px(108), 0x8024);
        }
        else if (page == 1)
        {
            DrawTextAt(hdc, "Digite o atalho em qualquer programa e aperte Tab  ·  arraste para reordenar  ·  botão direito para mais opções" +
                (cfgExpand ? "" : "  ·  DESLIGADA (ligue em Configurações)"), hUiFont, cfgExpand ? C_GRAY : C_RED, Px(18), Px(50), Px(804), Px(72), 0x8024);
            RoundRect(hdc, Px(362), Px(78), Px(442), Px(400), Px(11), A_WHITE, A_BORDER);
            DrawTextAt(hdc, "Atalho  ·  o que você digita (só letras sem acento e números; ex.: bn1)", hSmallFont, C_GRAY, Px(376), Px(86), Px(726), Px(106), 0x8024);
            RoundRect(hdc, Px(370), Px(108), Px(212), Px(32), Px(8), A_WHITE, A_BTNBORDER);
            string warn = XAbbrWarning();
            if (warn != null) DrawTextAt(hdc, warn, hUiBold, C_RED, Px(592), Px(108), Px(796), Px(140), 0x8024);
            DrawTextAt(hdc, expDirty ? "Edição não salva — clique em Salvar para valer" : "Texto que será inserido no lugar do atalho",
                hSmallFont, expDirty ? C_RED : C_GRAY, Px(376), Px(148), Px(792), Px(170), 0x8024);
            RoundRect(hdc, Px(370), Px(172), Px(426), Px(298), Px(8), A_WHITE, A_BTNBORDER);
        }
        else if (page == 2)
        {
            DrawTextAt(hdc, "Preferências do programa. As alterações valem na hora.", hUiFont, C_GRAY, Px(18), Px(50), Px(380), Px(72), 0x8024);
            RoundRect(hdc, Px(16), Px(78), Px(788), Px(444), Px(11), A_WHITE, A_BORDER);
            DrawTextAt(hdc, "Atalhos de colagem", hUiBold, C_TEXT, Px(36), Px(88), Px(780), Px(108), 0x24);
            DrawTextAt(hdc, "Teclas que, junto com os números 1 a 9, colam o clip da posição (e, com o 0, abrem a lista rápida):",
                hUiFont, C_GRAY, Px(36), Px(108), Px(780), Px(128), 0x8024);
            DrawTextAt(hdc, "Atalho atual:  " + cfgModsLabel + "+1…9", hUiBold, C_ACCENT, Px(384), Px(134), Px(780), Px(164), 0x24);
            DrawTextAt(hdc, "Aparência", hUiBold, C_TEXT, Px(36), Px(174), Px(780), Px(194), 0x24);
            DrawTextAt(hdc, "Cor da janela:", hUiFont, C_GRAY, Px(220), Px(198), Px(318), Px(228), 0x24);
            DrawTextAt(hdc, "Geral", hUiBold, C_TEXT, Px(36), Px(234), Px(780), Px(254), 0x24);
            DrawTextAt(hdc, "Arquivo", hUiBold, C_TEXT, Px(36), Px(392), Px(780), Px(412), 0x24);
            DrawTextAt(hdc, "Tudo fica no ClipAtalho.ini (e as imagens na pasta \"ClipAtalho imagens\"). Backup, pasta compartilhada e padrão de fábrica: \"Dados e backup\".",
                hSmallFont, C_GRAY, Px(36), Px(458), Px(780), Px(480), 0x8024);
        }
        else if (page == 4)
        {
            DrawTextAt(hdc, "Backup, restauração e onde os dados ficam.", hUiFont, C_GRAY, Px(18), Px(50), Px(540), Px(72), 0x8024);
            RoundRect(hdc, Px(16), Px(78), Px(788), Px(444), Px(11), A_WHITE, A_BORDER);
            DrawTextAt(hdc, "Backup", hUiBold, C_TEXT, Px(36), Px(88), Px(780), Px(108), 0x24);
            DrawTextAt(hdc, "Listas em texto (.txt) que dá para abrir, editar e restaurar. O backup completo leva também as configurações (.ini).",
                hUiFont, C_GRAY, Px(36), Px(108), Px(780), Px(128), 0x8024);
            DrawTextAt(hdc, "Apagar sozinho os clips não fixados", hUiBold, C_TEXT, Px(36), Px(178), Px(530), Px(198), 0x24);
            DrawTextAt(hdc, "Os fixados (★) nunca são apagados.", hUiFont, C_GRAY, Px(36), Px(198), Px(530), Px(218), 0x8024);
            DrawTextAt(hdc, "Pasta de dados", hUiBold, C_TEXT, Px(36), Px(230), Px(780), Px(250), 0x24);
            string where = !dataOn ? "Desligada: os dados ficam na pasta do programa (" + exeDir + ")"
                : (dataMissing ? "INDISPONÍVEL agora: " + dataDir + "  (usando a pasta do programa)" : "Pasta atual: " + dataDir);
            DrawTextAt(hdc, where, hSmallFont, dataMissing ? C_RED : C_GRAY, Px(36), Px(284), Px(780), Px(304), 0x8024);
            DrawTextAt(hdc, "Se mudar em outro computador:", hUiFont, C_GRAY, Px(196), Px(308), Px(410), Px(340), 0x8024);
            DrawTextAt(hdc, "Padrão de fábrica", hUiBold, C_TEXT, Px(36), Px(354), Px(780), Px(374), 0x24);
            DrawTextAt(hdc, "Apaga clips, atalhos e configurações deste computador e volta tudo como na primeira instalação.",
                hUiFont, C_GRAY, Px(36), Px(374), Px(780), Px(394), 0x8024);
            DrawTextAt(hdc, "Na pasta de dados, os computadores que apontarem para ela usam os mesmos clips e atalhos. A escolha da pasta vale só para este computador.",
                hSmallFont, C_GRAY, Px(36), Px(446), Px(780), Px(500), 0x10);
        }
        else
        {
            // cartão de apresentação: ícone, nome, versão e autor
            RoundRect(hdc, Px(16), Px(58), Px(788), Px(104), Px(14), A_WHITE, A_BORDER);
            DrawAppIcon(hdc, Px(38), Px(76), Px(68));
            DrawTextAt(hdc, "ClipAtalho", hBigFont, C_TEXT, Px(126), Px(68), Px(720), Px(102), 0x24);
            string[] vp = VERSION.Split(' ');
            DrawTextAt(hdc, "Versão " + vp[0] + (vp.Length > 1 ? "  ·  " + vp[1].Replace('-', '/') : ""), hUiFont, C_GRAY,
                Px(126), Px(102), Px(720), Px(122), 0x24);
            DrawTextAt(hdc, "Desenvolvido por TalixXGamer", hUiBold, C_TEXT, Px(126), Px(124), Px(720), Px(146), 0x24);
            // cartão com o texto de ajuda
            RoundRect(hdc, Px(16), Px(172), Px(788), Px(350), Px(14), A_WHITE, A_BORDER);
        }
        RoundRect(hdc, 0, 0, Px(820), Px(540), Px(WIN_R), 0, A_BTNBORDER); // contorno suave da janela
        EndPaint(hwnd, ref ps);
    }

    static void OpenManager(int p)
    {
        bool wasOpen = hwndMgr != IntPtr.Zero && IsWindowVisible(hwndMgr);
        if (hwndMgr == IntPtr.Zero) CreateManager();
        if (hwndMgr == IntPtr.Zero) return;
        HidePopup();
        ShowWindow(hwndMgr, 9); // restaura/mostra
        SetForegroundWindow(hwndMgr);
        if (!wasOpen)
        {
            editClip = null;
            editDirty = false;
            ShowPage(p);
            if (clips.Count == 0) SetWindowTextW(hEdit, "");
        }
        else NavTo(p);
    }

    static void CloseManager()
    {
        if (!ResolveDirty(true)) return;
        clips.RemoveAll(delegate(Clip c) { return !c.IsImage && c.Text.Length == 0; });
        ShowWindow(hwndMgr, 0);
        mgrSel = null;
        editClip = null;
        editDirty = false;
        expDirty = false;
        xNew = false;
        SaveClips();
    }

    static void LoadEdit()
    {
        mgrLoading = true;
        editClip = mgrSel;
        SetWindowTextW(hEdit, (mgrSel == null || mgrSel.IsImage) ? "" : mgrSel.Text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        mgrLoading = false;
        SetDirty(false);
        UpdateEditVisibility();
        InvalidateRect(GetDlgItem(hwndMgr, IDC_PIN), IntPtr.Zero, true); // rótulo Fixar/Soltar
    }

    // Clip de imagem selecionado: esconde o editor de texto e mostra a prévia.
    static void UpdateEditVisibility()
    {
        if (hwndMgr == IntPtr.Zero) return;
        bool img = mgrSel != null && mgrSel.IsImage;
        ShowWindow(hEdit, (page == 0 && !img) ? 5 : 0);
        InvalidateRect(hwndMgr, IntPtr.Zero, true);
    }

    static void RefreshList()
    {
        if (hwndMgr == IntPtr.Zero || !IsWindowVisible(hwndMgr)) return;
        mgrLoading = true;
        SendMessageW(hList, 0x000B, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW off (sem piscar)
        int top = SendMessageW(hList, 0x018E, IntPtr.Zero, IntPtr.Zero).ToInt32(); // LB_GETTOPINDEX
        SendMessageW(hList, 0x0184, IntPtr.Zero, IntPtr.Zero); // LB_RESETCONTENT
        for (int i = 0; i < clips.Count; i++)
            SendMessageStr(hList, 0x0180, IntPtr.Zero, ""); // LB_ADDSTRING (o desenho usa a lista de clips)
        if (mgrSel == null || !clips.Contains(mgrSel)) mgrSel = clips.Count > 0 ? clips[0] : null;
        if (top > 0 && top < clips.Count) SendMessageW(hList, 0x0197, (IntPtr)top, IntPtr.Zero); // LB_SETTOPINDEX
        if (mgrSel != null) SendMessageW(hList, 0x0186, (IntPtr)clips.IndexOf(mgrSel), IntPtr.Zero); // LB_SETCURSEL
        SendMessageW(hList, 0x000B, (IntPtr)1, IntPtr.Zero);
        InvalidateRect(hList, IntPtr.Zero, true);
        mgrLoading = false;
        if (editClip != mgrSel) LoadEdit();
        else InvalidateRect(GetDlgItem(hwndMgr, IDC_PIN), IntPtr.Zero, true);
    }

    static void SelectIndex(int idx)
    {
        if (idx < 0 || idx >= clips.Count) return;
        mgrSel = clips[idx];
        SendMessageW(hList, 0x0186, (IntPtr)idx, IntPtr.Zero); // LB_SETCURSEL
        if (editClip != mgrSel) LoadEdit();
    }

    static void MoveClip(int from, int to)
    {
        if (from < 0 || from >= clips.Count) return;
        int dest = to > from ? to - 1 : to;
        dest = Math.Max(0, Math.Min(clips.Count - 1, dest));
        if (dest != from)
        {
            PushClipUndo();
            Clip c = clips[from];
            clips.RemoveAt(from);
            clips.Insert(dest, c);
            mgrSel = c;
            SaveClips();
        }
        RefreshList();
    }

    // Índice do clip sob o ponteiro (lParam de mensagem de mouse) ou -1.
    static int ItemAt(IntPtr lParam)
    {
        long r = CallWindowProcW(oldListProc, hList, 0x01A9, IntPtr.Zero, lParam).ToInt64(); // LB_ITEMFROMPOINT
        if (((r >> 16) & 0xFFFF) != 0) return -1;
        int idx = (int)(r & 0xFFFF);
        return idx < clips.Count ? idx : -1;
    }

    // Mouse da lista: arrastar para reordenar e botão direito para o menu.
    static IntPtr ListProc(IntPtr hwnd, uint m, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
            switch (m)
            {
                case 0x0201: // WM_LBUTTONDOWN
                {
                    int idx = ItemAt(lParam);
                    if (editDirty && idx >= 0 && clips[idx] != editClip)
                    {
                        // pergunta antes de trocar de clip; o clique original é descartado
                        if (ResolveDirty(false)) SelectIndex(idx);
                        return IntPtr.Zero;
                    }
                    dragFrom = idx;
                    dragStartY = y;
                    dragTarget = -1;
                    dragging = false;
                    break;
                }
                case 0x0200: // WM_MOUSEMOVE
                    if ((wParam.ToInt64() & 1) != 0 && dragFrom >= 0)
                    {
                        if (!dragging && Math.Abs(y - dragStartY) > Px(6)) dragging = true;
                        if (dragging)
                        {
                            RECT rc;
                            GetClientRect(hwnd, out rc);
                            int top = CallWindowProcW(oldListProc, hwnd, 0x018E, IntPtr.Zero, IntPtr.Zero).ToInt32();
                            if (y < 0 && top > 0) CallWindowProcW(oldListProc, hwnd, 0x0197, (IntPtr)(--top), IntPtr.Zero);
                            else if (y > rc.Bottom) { CallWindowProcW(oldListProc, hwnd, 0x0197, (IntPtr)(top + 1), IntPtr.Zero);
                                top = CallWindowProcW(oldListProc, hwnd, 0x018E, IntPtr.Zero, IntPtr.Zero).ToInt32(); }
                            int ih = Px(58);
                            int yy = Math.Max(0, Math.Min(rc.Bottom, y));
                            int target = Math.Max(0, Math.Min(clips.Count, top + (yy + ih / 2) / ih));
                            if (target != dragTarget) // só redesenha quando o destino muda
                            {
                                dragTarget = target;
                                InvalidateRect(hwnd, IntPtr.Zero, false);
                            }
                            return IntPtr.Zero; // não deixa a lista trocar a seleção durante o arraste
                        }
                    }
                    break;
                case 0x0202: // WM_LBUTTONUP
                {
                    bool was = dragging;
                    int from = dragFrom, to = dragTarget;
                    dragging = false;
                    dragFrom = -1;
                    IntPtr res = CallWindowProcW(oldListProc, hwnd, m, wParam, lParam);
                    if (was) MoveClip(from, to);
                    return res;
                }
                case 0x0215: // WM_CAPTURECHANGED
                    if (dragging) { dragging = false; dragFrom = -1; InvalidateRect(hwnd, IntPtr.Zero, false); }
                    break;
                case 0x0204: // WM_RBUTTONDOWN: seleciona o clip sob o ponteiro
                {
                    int idx = ItemAt(lParam);
                    if (idx >= 0 && clips[idx] != mgrSel && ResolveDirty(false)) SelectIndex(idx);
                    return IntPtr.Zero;
                }
                case 0x0205: // WM_RBUTTONUP: menu do clip
                {
                    int idx = ItemAt(lParam);
                    if (idx < 0 || clips[idx] != mgrSel) return IntPtr.Zero;
                    List<MI> mi = new List<MI>();
                    mi.Add(Item(IDC_PIN, mgrSel.Pinned ? "☆  Soltar (deixar de fixar)" : "★  Fixar nesta posição"));
                    mi.Add(Item(IDC_UP, "↑  Subir"));
                    mi.Add(Item(IDC_DOWN, "↓  Descer"));
                    mi.Add(Sep());
                    mi.Add(Item(IDC_DEL, "Excluir"));
                    POINT pt;
                    GetCursorPos(out pt);
                    int cmd = ShowMenu(mi, pt.X, pt.Y, hwndMgr);
                    if (cmd != 0) ManagerCommand(cmd, 0);
                    return IntPtr.Zero;
                }
            }
        }
        catch { }
        return CallWindowProcW(oldListProc, hwnd, m, wParam, lParam);
    }

    static void ManagerCommand(int id, int code)
    {
        if (id == IDC_LIST)
        {
            if (code != 1 || mgrLoading) return; // LBN_SELCHANGE
            int cur = SendMessageW(hList, 0x0188, IntPtr.Zero, IntPtr.Zero).ToInt32(); // LB_GETCURSEL
            Clip target = (cur >= 0 && cur < clips.Count) ? clips[cur] : null;
            if (target != editClip && !ResolveDirty(false))
            {
                // continua editando: volta a seleção para o clip em edição
                SendMessageW(hList, 0x0186, (IntPtr)clips.IndexOf(mgrSel), IntPtr.Zero);
                return;
            }
            mgrSel = target;
            if (editClip != mgrSel) LoadEdit();
            return;
        }
        if (id == IDC_EDIT)
        {
            if (code == 0x0300 && !mgrLoading) SetDirty(true); // EN_CHANGE
            return;
        }
        if (id == IDC_XLIST)
        {
            if (code != 1 || mgrLoading) return; // LBN_SELCHANGE
            int cur = SendMessageW(hXList, 0x0188, IntPtr.Zero, IntPtr.Zero).ToInt32();
            string key = (cur >= 0 && cur < xKeys.Count) ? xKeys[cur] : null;
            if (key == xSel) return;
            if (!ResolveDirty(false))
            {
                SendMessageW(hXList, 0x0186, (IntPtr)(xSel == null ? -1 : xKeys.IndexOf(xSel)), IntPtr.Zero);
                return;
            }
            if (key != null && cfgAbbr.ContainsKey(key)) xSel = key;
            xNew = false;
            LoadX(); // sem recriar a lista no meio do clique
            return;
        }
        if (id == IDC_XABBR || id == IDC_XTEXT)
        {
            if (code == 0x0300 && !mgrLoading) SetXDirty(true); // EN_CHANGE
            if (code == 0x0300 && id == IDC_XABBR) InvalidateXWarning(); // atualiza o aviso "Atalho já em uso"
            return;
        }
        if (id == 2) { CloseManager(); return; } // Esc
        if (code != 0) return;

        if (id >= IDC_NAV && id < IDC_NAV + 4) { NavTo(id - IDC_NAV); return; }
        if (id == IDC_CLOSE) { CloseManager(); return; }

        int idx = mgrSel == null ? -1 : clips.IndexOf(mgrSel);
        if (id == IDC_UP || id == IDC_DOWN || id == IDC_PIN || id == IDC_DEL || id == IDC_NEW) PushClipUndo(); // repetidos sem mudança são ignorados ao desfazer
        switch (id)
        {
            // ---------------- página Clips
            case IDC_SAVE:
                if (!editDirty) return;
                if (editClip != null && !editClip.IsImage && editClip.Text.Length > 0)
                {
                    int r = AskSave("clip");
                    if (r == 1) SaveEdit();
                    else if (r == 2) SaveEditAsNew();
                }
                else SaveEdit();
                return;
            case IDC_CLEAR:
                ClearUnpinned();
                return;
            case IDC_DELPIN:
            {
                if (!clips.Exists(delegate(Clip c) { return c.Pinned; }))
                {
                    Info("Não há clips fixados para apagar.");
                    return;
                }
                if (!ResolveDirty(false)) return;
                dlgWarn = true;
                int ans = AskDialog("Apagar Fixados",
                    "Isso apagará TODOS os seus clips fixados. Você tem certeza disso?\n(essa opção foi adicionada apenas para apressados e/ou preguiçosos)",
                    "Sim, apaga TUDO!", "Eita, melhor não.", null);
                dlgWarn = false;
                if (ans != 1) return;
                PushClipUndo();
                clips.RemoveAll(delegate(Clip c) { return c.Pinned; });
                mgrSel = null;
                editClip = null;
                SaveClips();
                RefreshList();
                Log("Apagar Fixados: todos os clips fixados foram apagados");
                return;
            }
            case IDC_UP:
                if (idx > 0) { clips.RemoveAt(idx); clips.Insert(idx - 1, mgrSel); }
                break;
            case IDC_DOWN:
                if (idx >= 0 && idx < clips.Count - 1) { clips.RemoveAt(idx); clips.Insert(idx + 1, mgrSel); }
                break;
            case IDC_PIN:
                if (idx >= 0) mgrSel.Pinned = !mgrSel.Pinned;
                break;
            case IDC_DEL:
                if (idx < 0) return;
                if (mgrSel.Pinned && (mgrSel.Text.Length > 0 || mgrSel.IsImage) &&
                    AskDialog("Excluir clip", "Excluir este clip fixado? Não é possível desfazer.", "Excluir", "Cancelar", null) != 1) return;
                clips.RemoveAt(idx);
                mgrSel = clips.Count == 0 ? null : clips[Math.Min(idx, clips.Count - 1)];
                editClip = null;
                if (clips.Count == 0) LoadEdit();
                break;
            case IDC_NEW:
                if (!ResolveDirty(false)) return;
                idx = mgrSel == null ? -1 : clips.IndexOf(mgrSel);
                if (clips.Count >= MAX_CLIPS)
                {
                    int last = clips.FindLastIndex(delegate(Clip c) { return !c.Pinned; });
                    if (last < 0)
                    {
                        Info("A lista está cheia de clips fixados. Exclua ou solte algum.");
                        return;
                    }
                    clips.RemoveAt(last);
                    idx = mgrSel == null ? -1 : clips.IndexOf(mgrSel);
                }
                Clip novo = new Clip("", true);
                clips.Insert(idx < 0 ? clips.Count : idx, novo);
                mgrSel = novo;
                break;

            // ---------------- página Atalhos por Expansão
            case IDC_XSAVE:
                if (!expDirty) return;
                if (xSel != null)
                {
                    int r = AskSave("atalho");
                    if (r == 1) SaveX(false);
                    else if (r == 2) SaveX(true);
                }
                else SaveX(false);
                return;
            case IDC_XSAVENEW:
                SaveX(true);
                return;
            case IDC_XSORT:
                XSortMenu();
                return;
            case IDC_XNEW:
                if (!ResolveDirty(false)) return;
                xSel = null;
                xNew = true;
                RefreshX(true);
                SetFocus(hXAbbr);
                return;
            case IDC_XUP:
            case IDC_XDOWN:
            {
                int xi = xSel == null ? -1 : cfgAbbrOrder.IndexOf(xSel);
                if (xi < 0) return;
                XMove(xi, id == IDC_XUP ? xi - 1 : xi + 2);
                return;
            }
            case IDC_XPIN:
            {
                if (xSel == null || !cfgAbbr.ContainsKey(xSel)) return;
                List<string> loose = new List<string>(cfgAbbrLoose);
                if (!loose.Remove(xSel)) loose.Add(xSel);
                WriteAbbr(cfgAbbrOrder, cfgAbbr, loose);
                RefreshX(false);
                return;
            }
            case IDC_XCLEAR:
            {
                if (cfgAbbrLoose.Count == 0)
                {
                    Info("Não há atalhos não fixados para apagar.");
                    return;
                }
                if (!ResolveDirty(false)) return;
                if (AskDialog("Limpar lista", "Apagar os " + cfgAbbrLoose.Count + " atalho(s) não fixado(s)? Os fixados (★) são mantidos.",
                    "Apagar", "Cancelar", null) != 1) return;
                Dictionary<string, string> kept = new Dictionary<string, string>(cfgAbbr);
                foreach (string k in cfgAbbrLoose) kept.Remove(k);
                WriteAbbr(cfgAbbrOrder, kept, new List<string>());
                xNew = false;
                RefreshX(true);
                return;
            }
            case IDC_XDEL:
            {
                if (xSel == null)
                {
                    xNew = false;
                    RefreshX(true);
                    return;
                }
                if (AskDialog("Excluir atalho", "Excluir o atalho \"" + xSel + "\"? Não é possível desfazer.", "Excluir", "Cancelar", null) != 1) return;
                Dictionary<string, string> rest = new Dictionary<string, string>(cfgAbbr);
                rest.Remove(xSel);
                WriteAbbr(cfgAbbrOrder, rest, cfgAbbrLoose);
                xSel = null;
                xNew = false;
                RefreshX(true);
                return;
            }

            // ---------------- página Configurações
            case IDC_T_STARTUP:
                if (packaged)
                {
                    Info("No ClipAtalho instalado pela Microsoft Store, o início junto com o Windows é ligado ou desligado em:\nConfigurações do Windows › Aplicativos › Inicialização.");
                    try { Process.Start("ms-settings:startupapps"); } catch { }
                    return;
                }
                SetStartup(!startupOn);
                startupOn = StartupEnabled();
                RepaintAll();
                return;
            case IDC_T_SAVEALL:
                SetIniValue("geral", "salvar_historico", cfgSaveAll ? "nao" : "sim");
                LoadConfig();
                SaveClips();
                RepaintAll();
                return;
            case IDC_T_IMAGES:
                SetIniValue("geral", "imagens", cfgImages ? "nao" : "sim");
                LoadConfig();
                RepaintAll();
                return;
            case IDC_T_EXPAND:
                SetIniValue("geral", "expansao", cfgExpand ? "nao" : "sim");
                LoadConfig();
                RepaintAll();
                return;
            case IDC_OPENINI:
                try { Process.Start("notepad.exe", "\"" + iniPath + "\""); } catch { }
                return;
            case IDC_OPENDIR:
                try { Process.Start("explorer.exe", "\"" + DataDir + "\""); } catch { }
                return;

            // ---------------- Dados e backup
            case IDC_SEG: ShowPage(2); return;
            case IDC_SEG + 1: ShowPage(4); return;
            case IDC_BK_CLIPS: DoBackup(true, false); return;
            case IDC_BK_X: DoBackup(false, true); return;
            case IDC_BK_ALL: DoBackup(true, true, true); return;
            case IDC_RESTOREBK: DoRestore(); return;
            case IDC_AUTODEL: AutoDelMenu(); return;
            case IDC_T_DATADIR: ToggleDataFolder(); return;
            case IDC_DATADIR: ChooseDataFolder(); return;
            case IDC_CONFLICT: ConflictMenu(); return;
            case IDC_FACTORY: FactoryReset(); return;
            case IDC_UNDO: case IDC_REDO: DoUndo(0, id == IDC_REDO); return;
            case IDC_XUNDO: case IDC_XREDO: DoUndo(1, id == IDC_XREDO); return;
            case IDC_FEEDBACK: FeedbackDialog(); return;

            // ---------------- página Ajuda
            case IDC_INSTA:
                try { Process.Start(INSTAGRAM); } catch { }
                return;

            default:
                if (id == IDC_THEME || id == IDC_THEME + 1)
                {
                    SetIniValue("geral", "tema", id == IDC_THEME ? "claro" : "escuro");
                    LoadConfig();
                    RepaintAll();
                }
                else if (id >= IDC_COLOR && id < IDC_COLOR + ColorRgb.Length)
                {
                    SetIniValue("geral", "cor", ColorNames[id - IDC_COLOR]);
                    LoadConfig();
                    RepaintAll();
                }
                else if (id >= IDC_MOD && id < IDC_MOD + 4)
                {
                    uint nm = cfgMods ^ ModBits[id - IDC_MOD];
                    if (nm == 0) { Info("Escolha pelo menos uma tecla para o atalho."); return; }
                    if (nm == 4) { Info("Shift sozinho impediria digitar ! @ # $ %. Combine com Ctrl ou Alt."); return; }
                    SetIniValue("geral", "modificadores", ModsLabel(nm));
                    LoadConfig();
                    RepaintAll();
                }
                return;
        }
        RefreshList();
        SaveClips();
        if (id == IDC_NEW) SetFocus(hEdit);
    }

    static IntPtr MgrProc(IntPtr hwnd, uint m, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (m)
            {
                case WM_COMMAND:
                    long wp = wParam.ToInt64();
                    ManagerCommand((int)(wp & 0xFFFF), (int)((wp >> 16) & 0xFFFF));
                    return IntPtr.Zero;
                case WM_CLOSE:
                    CloseManager();
                    return IntPtr.Zero;
                case 0x0047: // WM_WINDOWPOSCHANGED: a sombra acompanha a janela
                    if (IsWindowVisible(hwnd) && !IsIconic(hwnd)) PlaceShade(hwnd);
                    else HideShade(hwnd);
                    break;
                case 0x0083: // WM_NCCALCSIZE: sem barra de título do Windows (o programa desenha o topo)
                    if (wParam != IntPtr.Zero) return IntPtr.Zero;
                    break;
                case 0x0086: // WM_NCACTIVATE: não redesenha a moldura padrão ao trocar de janela
                    return DefWindowProcW(hwnd, m, wParam, (IntPtr)(-1));
                case 0x0084: // WM_NCHITTEST: a faixa do título serve para arrastar a janela
                {
                    int sy = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    RECT wr;
                    GetWindowRect(hwnd, out wr);
                    return (IntPtr)(sy - wr.Top < Px(58) ? 2 : 1); // HTCAPTION : HTCLIENT
                }
                case WM_PAINT:
                    PaintManager(hwnd);
                    return IntPtr.Zero;
                case 0x002B: // WM_DRAWITEM
                    DRAWITEMSTRUCT d = (DRAWITEMSTRUCT)Marshal.PtrToStructure(lParam, typeof(DRAWITEMSTRUCT));
                    if (d.CtlType == 2 && d.CtlID == IDC_XLIST) Buffered(d, DrawXItem);
                    else if (d.CtlType == 2) Buffered(d, DrawListItem);
                    else if (d.CtlType == 4) Buffered(d, DrawButton);
                    return (IntPtr)1;
                case 0x0134: // WM_CTLCOLORLISTBOX: fundo da lista igual ao da janela
                    return hBgBrush;
                case 0x0138: // WM_CTLCOLORSTATIC: texto da Ajuda (somente leitura) com fundo branco
                    SetTextColor(wParam, C_TEXT);
                    SetBkColor(wParam, C_CARD);
                    return hWhiteBrush;
                case 0x0133: // WM_CTLCOLOREDIT: campos de texto na cor do cartão
                    SetTextColor(wParam, C_TEXT);
                    SetBkColor(wParam, C_CARD);
                    return hWhiteBrush;
                case 0x0014: // WM_ERASEBKGND: fundo da janela na cor do tema
                    RECT bgrc;
                    GetClientRect(hwnd, out bgrc);
                    FillRect(wParam, ref bgrc, hBgBrush);
                    return (IntPtr)1;
            }
        }
        catch { }
        return DefWindowProcW(hwnd, m, wParam, lParam);
    }

    static void ShowSuggest(string abbr, string text)
    {
        popAbbr = abbr;
        popClips = new List<Clip>();
        popNumbered = false;
        popLines = new List<string>();
        popLines.Add("Tab = inserir  ·  Esc = ignorar   [" + abbr + "]");
        popLines.Add(Preview(text, 160));
        popMode = MODE_SUGGEST;
        ShowPopup(6000);
    }

    static void ShowPopup(uint ms)
    {
        popHover = -1;
        popHoverTrash = false;
        popClearArmed = false;
        popTrashArmed = null;
        popDrag = false;
        if (popScale.Length != popLines.Count) popScale = new float[popLines.Count];
        if (hMouseHook == IntPtr.Zero) // o mouse só é observado enquanto a lista está aberta
            hMouseHook = SetWindowsHookExW(14, Marshal.GetFunctionPointerForDelegate(mouseProcRef), hInst, 0);
        int w = Px(540), h = PopupHeight();
        int x = 0, y = 0;
        bool placed = false;
        if (popMode == MODE_HISTORY && cfgListPos) // abre onde o usuário deixou da última vez
        {
            x = cfgListX;
            y = cfgListY;
            placed = true;
        }

        GUITHREADINFO gti = new GUITHREADINFO();
        gti.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
        if (!placed && GetGUIThreadInfo(0, ref gti) && gti.hwndCaret != IntPtr.Zero && gti.rcCaret.Bottom - gti.rcCaret.Top > 0)
        {
            POINT pt = new POINT();
            pt.X = gti.rcCaret.Left;
            pt.Y = gti.rcCaret.Bottom;
            ClientToScreen(gti.hwndCaret, ref pt);
            x = pt.X;
            y = pt.Y + Px(6);
            placed = true;
        }
        if (!placed)
        {
            IntPtr fg = GetForegroundWindow();
            RECT rc;
            if (fg != IntPtr.Zero && GetWindowRect(fg, out rc))
            {
                x = rc.Left + (rc.Right - rc.Left - w) / 2;
                y = rc.Bottom - h - Px(70);
            }
            else
            {
                POINT pt;
                GetCursorPos(out pt);
                x = pt.X;
                y = pt.Y + Px(20);
            }
        }
        // mantém dentro da área útil do monitor
        POINT at = new POINT();
        at.X = x;
        at.Y = y;
        MONITORINFO mi = new MONITORINFO();
        mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        if (GetMonitorInfoW(MonitorFromPoint(at, 2), ref mi))
        {
            if (x + w > mi.rcWork.Right) x = mi.rcWork.Right - w;
            if (x < mi.rcWork.Left) x = mi.rcWork.Left;
            if (y + h > mi.rcWork.Bottom) y = mi.rcWork.Bottom - h;
            if (y < mi.rcWork.Top) y = mi.rcWork.Top;
        }
        FadeWindow(hwndPopup, true);
        SetWindowPos(hwndPopup, (IntPtr)(-1), x, y, w, h, 0x10 | 0x40); // topo, sem ativar
        RoundWindow(hwndPopup, w, h);
        PlaceShade(hwndPopup);
        InvalidateRect(hwndPopup, IntPtr.Zero, true);
        SetTimer(hwndMain, (IntPtr)TIMER_POPUP, ms, IntPtr.Zero);
    }

    static void HidePopup()
    {
        if (popMode == MODE_NONE) return;
        popMode = MODE_NONE;
        if (popDrag) { popDrag = false; ReleaseCapture(); }
        FadeWindow(hwndPopup, false);
        KillTimer(hwndMain, (IntPtr)TIMER_POPUP);
        KillTimer(hwndMain, (IntPtr)TIMER_WAVE);
        if (hMouseHook != IntPtr.Zero) { UnhookWindowsHookEx(hMouseHook); hMouseHook = IntPtr.Zero; }
    }

    static void PaintPopup(IntPtr hwnd)
    {
        PAINTSTRUCT ps;
        IntPtr hdc = BeginPaint(hwnd, out ps);
        int w = Px(540), h = PopupHeight();
        if (popMem == IntPtr.Zero || popMemW != w || popMemH != h) // bitmap de trabalho reaproveitado entre pinturas
        {
            if (popMem != IntPtr.Zero) { SelectObject(popMem, popMemOld); DeleteObject(popMemBmp); DeleteDC(popMem); }
            popMem = CreateCompatibleDC(hdc);
            popMemBmp = CreateCompatibleBitmap(hdc, w, h);
            popMemOld = SelectObject(popMem, popMemBmp);
            popMemW = w;
            popMemH = h;
        }
        IntPtr mem = popMem;
        RECT pr = ps.rcPaint;
        FillRect(mem, ref pr, hWhiteBrush);
        for (int i = 0; i < popLines.Count; i++)
        {
            int top = Px(8) + i * Px(POP_ROW), bottom = top + Px(POP_ROW);
            if (bottom + Px(2) < pr.Top || top - Px(2) > pr.Bottom) continue; // só as linhas que mudaram
            if (i == 0)
            {
                DrawTextAt(mem, popClearArmed ? "Clique em “Limpar” de novo para apagar os não fixados" : popLines[0],
                    popClearArmed ? hFontBold : hFont, popClearArmed ? C_RED : C_GRAY, Px(16), top, w - Px(130), bottom, 0x8024);
                continue;
            }
            float sc = i < popScale.Length ? popScale[i] : 0f;
            bool hover = i == popHover;
            if (hover || sc > 0.01f) // destaque do item sob o cursor, crescendo junto
            {
                int grow = (int)(Px(3) * sc);
                RoundRect(mem, Px(8) - grow, top - grow / 2, w - Px(16) + grow * 2, Px(POP_ROW) + grow, Px(8), A_SEL, 0);
            }
            Clip c = (popNumbered && i - 1 < popClips.Count) ? popClips[i - 1] : null;
            if (c == null)
            {
                DrawTextAt(mem, popLines[i], hFontBold, C_TEXT, Px(16), top, w - Px(16), bottom, 0x8024);
                continue;
            }
            IntPtr f = popFonts[Math.Max(0, Math.Min(3, (int)Math.Round(sc * 3)))];
            if (f == IntPtr.Zero) f = hFontBold;
            int shift = (int)(Px(6) * sc);
            DrawTextAt(mem, i.ToString(), f, i <= 9 ? C_ACCENT : C_GRAY, Px(16) + shift / 2, top, Px(46) + shift, bottom, 0x24);
            int tx = Px(46) + shift;
            if (c.IsImage) // miniatura leve, ampliando com o efeito
            {
                int bw = Px(48) + (int)(Px(16) * sc), bh = Px(POP_ROW) - Px(6) + (int)(Px(4) * sc);
                int by = top + (Px(POP_ROW) - bh) / 2;
                RoundRect(mem, tx, by, bw, bh, Px(5), A_BG, A_BORDER);
                DrawImageFit(mem, ThumbOf(c), tx + Px(2), by + Px(2), bw - Px(4), bh - Px(4));
                tx += bw + Px(8);
            }
            int right = hover ? w - Px(50) : w - Px(16);
            DrawTextAt(mem, popLines[i], f, C_TEXT, tx, top, right, bottom, 0x8024);
            if (hover) // lixeira para excluir o item
            {
                bool armed = popTrashArmed == c;
                if (popHoverTrash || armed) RoundRect(mem, w - Px(46), top + Px(3), Px(30), Px(POP_ROW) - Px(6), Px(6), A_DOWN, 0);
                DrawTextAt(mem, armed ? "?" : "", armed ? hFontBold : hIconFont, (popHoverTrash || armed) ? C_RED : C_GRAY,
                    w - Px(46), top, w - Px(16), bottom, 0x25);
            }
        }
        // cabeçalho: "Limpar" e X
        int hy = Px(10), hs = Px(26);
        if (popNumbered && pr.Top <= Px(8 + POP_ROW))
        {
            int lx = w - Px(118), lw = Px(70);
            if (popHover == -3 || popClearArmed) RoundRect(mem, lx, hy, lw, hs, Px(7), A_DOWN, 0);
            DrawTextAt(mem, "Limpar", hFontBold, (popHover == -3 || popClearArmed) ? C_RED : C_GRAY, lx, hy, lx + lw, hy + hs, 0x25);
        }
        int xx = w - Px(38);
        if (popHover == -2) RoundRect(mem, xx, hy, hs, hs, Px(7), A_DOWN, 0);
        DrawTextAt(mem, "✕", hFontBold, popHover == -2 ? C_RED : C_GRAY, xx, hy, xx + hs, hy + hs, 0x25);
        RoundRect(mem, 0, 0, w, h, Px(WIN_R), 0, A_BTNBORDER);
        BitBlt(hdc, pr.Left, pr.Top, pr.Right - pr.Left, pr.Bottom - pr.Top, mem, pr.Left, pr.Top, 0x00CC0020);
        EndPaint(hwnd, ref ps);
    }

    // ------------------------------------------------------------------ gancho de teclado
    // Devolve true quando a tecla deve ser "engolida".
    // Teclas de captura de tela: Print Screen (sozinha ou com Alt/Ctrl/Shift) e combinações com Win (ex.: Win+Shift+S).
    static bool IsCaptureKey(int vk)
    {
        return vk == 0x2C || KeyDown(VK_LWIN) || KeyDown(VK_RWIN); // 0x2C = VK_SNAPSHOT
    }

    static bool HandleKey(int vk)
    {
        bool ctrlAltWin = KeyDown(VK_CONTROL) || KeyDown(VK_MENU) || KeyDown(VK_LWIN) || KeyDown(VK_RWIN);
        bool shift = KeyDown(VK_SHIFT);

        if (popMode == MODE_HISTORY)
        {
            if (IsModifierVk(vk)) return false;
            if (IsCaptureKey(vk)) return false; // não fecha: deixa printar a lista
            if (vk == VK_ESCAPE)
            {
                PostMessageW(hwndMain, WM_HIDEPOPUP, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            int n = -1;
            if (vk >= '1' && vk <= '9' && !shift) n = vk - '1';
            else if (vk >= 0x61 && vk <= 0x69) n = vk - 0x61;
            PostMessageW(hwndMain, WM_HIDEPOPUP, IntPtr.Zero, IntPtr.Zero);
            if (n >= 0 && !ctrlAltWin)
            {
                if (n < clips.Count) PasteClip(clips[n]);
                return true;
            }
            return false;
        }

        if (popMode == MODE_SUGGEST)
        {
            if (IsModifierVk(vk)) return false;
            if (IsCaptureKey(vk)) return false; // não fecha: deixa printar a sugestão
            if (vk == VK_TAB && !ctrlAltWin && !shift)
            {
                string abbr = popAbbr;
                PostMessageW(hwndMain, WM_HIDEPOPUP, IntPtr.Zero, IntPtr.Zero);
                typed.Length = 0;
                string text = AbbrText(abbr);
                if (text != null)
                {
                    StartPaste(text, abbr.Length);
                    return true;
                }
                return false;
            }
            PostMessageW(hwndMain, WM_HIDEPOPUP, IntPtr.Zero, IntPtr.Zero);
            if (vk == VK_ESCAPE)
            {
                typed.Length = 0;
                return true;
            }
        }

        if (IsModifierVk(vk)) return false;

        // Modo alternativo: se o Windows recusou o registro de um atalho, ele é capturado aqui.
        int digitKey = -1, slot = 0;
        if (vk >= '0' && vk <= '9') { digitKey = vk - '0'; slot = digitKey == 0 ? 10 : digitKey; }
        else if (vk >= 0x60 && vk <= 0x69) { digitKey = vk - 0x60; slot = (digitKey == 0 ? 10 : digitKey) + 10; }
        if (digitKey >= 0 && !registeredOK[slot])
        {
            uint mask = (KeyDown(VK_MENU) ? 1u : 0u) | (KeyDown(VK_CONTROL) ? 2u : 0u) | (shift ? 4u : 0u) |
                ((KeyDown(VK_LWIN) || KeyDown(VK_RWIN)) ? 8u : 0u);
            if (mask == cfgMods)
            {
                PostMessageW(hwndMain, WM_HOTKEY, (IntPtr)slot, IntPtr.Zero);
                return true;
            }
        }

        if (!cfgExpand || cfgAbbr.Count == 0) return false;
        DateTime now = DateTime.UtcNow;
        IntPtr fg = GetForegroundWindow();
        if (fg != lastFg || (now - lastKey).TotalSeconds > 5) typed.Length = 0;
        lastFg = fg;
        lastKey = now;

        if (ctrlAltWin) { typed.Length = 0; return false; }
        if (vk >= 'A' && vk <= 'Z') typed.Append((char)(vk + 32));
        else if (vk >= '0' && vk <= '9' && !shift) typed.Append((char)vk);
        else if (vk >= 0x60 && vk <= 0x69) typed.Append((char)(vk - 0x60 + '0'));
        else if (vk == VK_BACK) { if (typed.Length > 0) typed.Length--; }
        else { typed.Length = 0; return false; }

        if (typed.Length > 32) typed.Remove(0, typed.Length - 32);
        if (cfgAbbr.ContainsKey(typed.ToString()))
            PostMessageW(hwndMain, WM_SHOWPOPUP, IntPtr.Zero, IntPtr.Zero);
        return false;
    }

    static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            uint msg = (uint)wParam.ToInt64();
            if (nCode == 0 && !paused && (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN))
            {
                int vk = Marshal.ReadInt32(lParam);
                int flags = Marshal.ReadInt32(lParam, 8);
                if ((flags & 0x10) == 0 && HandleKey(vk)) // ignora teclas injetadas (inclusive as nossas)
                    return (IntPtr)1;
            }
        }
        catch { }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    // ------------------------------------------------------------------ janelas
    static IntPtr MainProc(IntPtr hwnd, uint m, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (m)
            {
                case WM_CLIPBOARDUPDATE:
                    OnClipboardUpdate();
                    return IntPtr.Zero;
                case WM_HOTKEY:
                    if (paused) return IntPtr.Zero;
                    int id = wParam.ToInt32();
                    if (id > 10) id -= 10; // teclado numérico
                    Log("Atalho recebido: " + cfgModsLabel + "+" + (id == ID_LIST ? 0 : id) + " (clips na lista: " + clips.Count + ")");
                    if (id == ID_LIST)
                    {
                        if (popMode == MODE_HISTORY) HidePopup(); else ShowHistory();
                    }
                    else if (id >= 1 && id <= 9)
                    {
                        HidePopup();
                        if (id - 1 < clips.Count) PasteClip(clips[id - 1]);
                    }
                    return IntPtr.Zero;
                case WM_TIMER:
                    switch (wParam.ToInt32())
                    {
                        case TIMER_WAITMODS: OnWaitMods(); break;
                        case TIMER_RESTORE: OnRestore(); break;
                        case TIMER_POPUP: HidePopup(); break;
                        case TIMER_FADE: OnFadeTimer(); break;
                        case TIMER_CLIPREAD: ProcessClipboard(); break;
                        case TIMER_WAVE: OnWaveTimer(); break;
                        case TIMER_ARM:
                            KillTimer(hwndMain, (IntPtr)TIMER_ARM);
                            popClearArmed = false;
                            popTrashArmed = null;
                            if (popMode == MODE_HISTORY) InvalidateRect(hwndPopup, IntPtr.Zero, false);
                            break;
                        case TIMER_CONFIG:
                            if (File.Exists(iniPath) && File.GetLastWriteTimeUtc(iniPath) != iniMod) OnIniChangedOutside();
                            RunAutoDelete();
                            break;
                    }
                    return IntPtr.Zero;
                case WM_SHOWPOPUP:
                    string abbr = typed.ToString();
                    string text = AbbrText(abbr);
                    if (text != null) ShowSuggest(abbr, text);
                    return IntPtr.Zero;
                case WM_HIDEPOPUP:
                    HidePopup();
                    return IntPtr.Zero;
                case WM_TRAY:
                    uint ev = (uint)(lParam.ToInt64() & 0xFFFF);
                    if (ev == WM_LBUTTONUP) OpenManager(0);
                    else if (ev == WM_RBUTTONUP) TrayMenu();
                    return IntPtr.Zero;
                case WM_DESTROY:
                    PostQuitMessage(0);
                    return IntPtr.Zero;
                case 0x0016: // WM_ENDSESSION: Windows desligando com conflito pendente
                    if (wParam != IntPtr.Zero) ResolveHold(false);
                    return IntPtr.Zero;
                case WM_FEEDBACKDONE:
                    FeedbackDone(wParam != IntPtr.Zero);
                    return IntPtr.Zero;
            }
            if (msgTaskbar != 0 && m == msgTaskbar) // Explorer reiniciou: recria o ícone
            {
                AddTray();
                SetTip();
                return IntPtr.Zero;
            }
        }
        catch { }
        return DefWindowProcW(hwnd, m, wParam, lParam);
    }

    static IntPtr PopupProc(IntPtr hwnd, uint m, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (m)
            {
                case 0x0014: // WM_ERASEBKGND: pintado em WM_PAINT (sem piscar)
                    return (IntPtr)1;
                case WM_PAINT:
                    PaintPopup(hwnd);
                    return IntPtr.Zero;
                case WM_MOUSEACTIVATE:
                    return (IntPtr)3; // MA_NOACTIVATE
                case 0x0201: // WM_LBUTTONDOWN na faixa de cima: começa a arrastar a lista
                {
                    if (OnPopupClose(lParam)) return IntPtr.Zero;
                    int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    if (popMode == MODE_HISTORY && y < Px(8 + POP_ROW))
                    {
                        popDrag = true;
                        GetCursorPos(out popDragStart);
                        GetWindowRect(hwnd, out popDragRect);
                        SetCapture(hwnd);
                    }
                    return IntPtr.Zero;
                }
                case 0x0200: // WM_MOUSEMOVE
                {
                    if (popDrag)
                    {
                        POINT pt;
                        GetCursorPos(out pt);
                        SetWindowPos(hwnd, IntPtr.Zero, popDragRect.Left + pt.X - popDragStart.X, popDragRect.Top + pt.Y - popDragStart.Y,
                            0, 0, 0x1 | 0x4 | 0x10);
                        PlaceShade(hwnd);
                        return IntPtr.Zero;
                    }
                    int line = OnPopupClose(lParam) ? -2 : (OnPopupClear(lParam) ? -3 : PopupLineAt(lParam));
                    if (line == 0) line = -1;
                    bool trash = line >= 1 && popNumbered && OnPopupTrash(lParam);
                    if (line != popHover || trash != popHoverTrash)
                    {
                        int old = popHover;
                        popHover = line;
                        popHoverTrash = trash;
                        for (int k = 1; k < popScale.Length; k++) // só um item ampliado por vez
                            if (k != line && popScale[k] > 0f) { popScale[k] = 0f; InvalidatePopRow(k); }
                        if (old < 1 || line < 1) InvalidatePopRow(0); // cabeçalho (X / Limpar)
                        InvalidatePopRow(old);
                        InvalidatePopRow(line);
                        if (popNumbered) SetTimer(hwndMain, (IntPtr)TIMER_WAVE, 16, IntPtr.Zero);
                    }
                    return IntPtr.Zero;
                }
                case 0x0215: // WM_CAPTURECHANGED
                    popDrag = false;
                    break;
                case WM_LBUTTONUP: // clique: cola o item (a janela não tira o foco do programa em uso)
                {
                    if (popDrag) // fim do arraste: guarda a posição
                    {
                        popDrag = false;
                        ReleaseCapture();
                        RECT r;
                        if (GetWindowRect(hwnd, out r))
                        {
                            cfgListPos = true;
                            cfgListX = r.Left;
                            cfgListY = r.Top;
                            SetIniValue("geral", "lista_pos", r.Left + "," + r.Top);
                        }
                        SetTimer(hwndMain, (IntPtr)TIMER_POPUP, 20000, IntPtr.Zero);
                        return IntPtr.Zero;
                    }
                    if (OnPopupClose(lParam)) { HidePopup(); return IntPtr.Zero; } // botão X
                    if (popNumbered && OnPopupClear(lParam)) { PopupClear(); return IntPtr.Zero; } // "Limpar"
                    int line = PopupLineAt(lParam);
                    if (line >= 1 && popNumbered && OnPopupTrash(lParam) && line - 1 < popClips.Count) { PopupTrash(popClips[line - 1]); return IntPtr.Zero; }
                    int mode = popMode;
                    string abbr = popAbbr;
                    if (line < 1) return IntPtr.Zero;
                    HidePopup();
                    if (paused) return IntPtr.Zero;
                    if (mode == MODE_HISTORY)
                    {
                        if (line - 1 < popClips.Count)
                        {
                            Log("Lista rápida: clique no item " + line);
                            PasteClip(popClips[line - 1]);
                        }
                    }
                    else if (mode == MODE_SUGGEST)
                    {
                        string text = AbbrText(abbr);
                        typed.Length = 0;
                        if (text != null) StartPaste(text, abbr.Length);
                    }
                    return IntPtr.Zero;
                }
            }
        }
        catch { }
        return DefWindowProcW(hwnd, m, wParam, lParam);
    }

    // O ponteiro está sobre o botão X da lista rápida?
    static bool OnPopupClose(IntPtr lParam)
    {
        int x = (short)(lParam.ToInt64() & 0xFFFF), y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        int w = Px(540);
        return x >= w - Px(42) && x < w - Px(8) && y >= Px(6) && y < Px(40);
    }

    static bool OnPopupClear(IntPtr lParam)
    {
        int x = (short)(lParam.ToInt64() & 0xFFFF), y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        int w = Px(540);
        return x >= w - Px(120) && x < w - Px(46) && y >= Px(6) && y < Px(40);
    }

    static bool OnPopupTrash(IntPtr lParam)
    {
        int x = (short)(lParam.ToInt64() & 0xFFFF);
        int w = Px(540);
        return x >= w - Px(48) && x < w - Px(12);
    }

    // "Limpar": o primeiro clique pede confirmação na própria lista; o segundo apaga os não fixados.
    static void PopupClear()
    {
        if (!popClearArmed)
        {
            popClearArmed = true;
            InvalidatePopRow(0);
            SetTimer(hwndMain, (IntPtr)TIMER_ARM, 4000, IntPtr.Zero);
            return;
        }
        popClearArmed = false;
        KillTimer(hwndMain, (IntPtr)TIMER_ARM);
        PushClipUndo();
        int n = clips.RemoveAll(delegate(Clip c) { return !c.Pinned; });
        Log("Lista rápida: " + n + " clip(s) não fixado(s) apagado(s)");
        SaveClips();
        RefreshList();
        RefreshPopupList();
    }

    // Lixeira: apaga na hora; num clip fixado, pede um segundo clique para confirmar.
    static void PopupTrash(Clip c)
    {
        if (c.Pinned && popTrashArmed != c)
        {
            popTrashArmed = c;
            InvalidatePopRow(popHover);
            SetTimer(hwndMain, (IntPtr)TIMER_ARM, 4000, IntPtr.Zero);
            return;
        }
        popTrashArmed = null;
        PushClipUndo();
        clips.Remove(c);
        SaveClips();
        RefreshList();
        RefreshPopupList();
    }

    // Linha da lista rápida sob o ponteiro (0 = cabeçalho) ou -1.
    static int PopupLineAt(IntPtr lParam)
    {
        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        if (y < Px(8)) return -1;
        int line = (y - Px(8)) / Px(POP_ROW);
        return line < popLines.Count ? line : -1;
    }

    static void RegisterClass(string name, WndProcDelegate proc, IntPtr background)
    {
        WNDCLASSEX wc = new WNDCLASSEX();
        wc.cbSize = Marshal.SizeOf(typeof(WNDCLASSEX));
        wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(proc);
        wc.hInstance = hInst;
        wc.hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)32512);
        wc.hbrBackground = background;
        wc.lpszClassName = name;
        RegisterClassExW(ref wc);
    }

    static IntPtr MakeFont(int weight)
    {
        return CreateFontW(-Px(15), 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
    }

    [STAThread]
    static void Main()
    {
        bool created;
        Mutex mutex = new Mutex(true, "ClipAtalho_InstanciaUnica", out created);
        if (!created)
        {
            MessageBoxW(IntPtr.Zero, "O ClipAtalho já está em execução (veja o ícone perto do relógio).", "ClipAtalho", 0x40);
            return;
        }

        exeDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        packaged = IsPackaged();
        if (packaged) // a pasta de instalação da loja é só leitura: os dados vão para a pasta do usuário
        {
            exeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipAtalho");
            try { Directory.CreateDirectory(exeDir); } catch { }
        }
        OleInitialize(IntPtr.Zero); // janelas de escolher pasta/arquivo
        LoadLocal();
        ApplyDataPaths();
        clipsPath = Path.Combine(exeDir, "ClipAtalho.clips");
        logPath = Path.Combine(exeDir, "ClipAtalho.log");
        try { if (File.Exists(logPath) && new FileInfo(logPath).Length > 200000) File.Delete(logPath); } catch { }
        Log("---- ClipAtalho iniciado (" + VERSION + ", " + (IntPtr.Size * 8) + " bits) ----");

        SetProcessDPIAware();
        hInst = GetModuleHandleW(null);
        IntPtr dc = GetDC(IntPtr.Zero);
        if (dc != IntPtr.Zero)
        {
            int dpi = GetDeviceCaps(dc, 90);
            if (dpi >= 96) scale = dpi / 96.0;
            ReleaseDC(IntPtr.Zero, dc);
        }
        hFont = MakeFont(400);
        hFontBold = MakeFont(600);
        for (int k = 0; k < 4; k++) popFonts[k] = CreateFontW(-Px(15 + k), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        hIconFont = CreateFontW(-Px(14), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe MDL2 Assets");
        hUiFont = CreateFontW(-Px(13), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        hUiBold = CreateFontW(-Px(13), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        hSmallFont = CreateFontW(-Px(11), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        hTitleFont = CreateFontW(-Px(21), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        hBigFont = CreateFontW(-Px(25), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        hHeadFont = CreateFontW(-Px(17), 0, 0, 0, 600, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        try
        {
            GdiplusStartupInput gsi = new GdiplusStartupInput();
            gsi.GdiplusVersion = 1;
            IntPtr token;
            gdipOK = GdiplusStartup(out token, ref gsi, IntPtr.Zero) == 0;
        }
        catch { gdipOK = false; }
        ApplyTheme(); // cores, pincéis e ícone (refeito depois de ler o .ini)

        mainProcRef = MainProc;
        popupProcRef = PopupProc;
        hookProcRef = HookProc;
        mgrProcRef = MgrProc;
        RegisterClass("ClipAtalhoMain", mainProcRef, IntPtr.Zero);
        RegisterClass("ClipAtalhoPopup", popupProcRef, IntPtr.Zero);
        RegisterClass("ClipAtalhoManager", mgrProcRef, IntPtr.Zero);
        dlgProcRef = DlgProc;
        RegisterClass("ClipAtalhoDialog", dlgProcRef, IntPtr.Zero);
        shadeProcRef = ShadeProc;
        RegisterClass("ClipAtalhoShadow", shadeProcRef, IntPtr.Zero);
        menuProcRef = MenuProc;
        RegisterClass("ClipAtalhoMenu", menuProcRef, IntPtr.Zero);
        hwndMain = CreateWindowExW(0, "ClipAtalhoMain", "ClipAtalho", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
        hwndPopup = CreateWindowExW(0x8 | 0x80 | 0x08000000 | 0x80000, "ClipAtalhoPopup", "", 0x80000000, 0, 0, 10, 10, IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
        if (hwndMain == IntPtr.Zero || hwndPopup == IntPtr.Zero)
        {
            MessageBoxW(IntPtr.Zero, "Falha ao criar as janelas do programa.", "ClipAtalho", 0x10);
            return;
        }

        AttachShade(hwndPopup, NewShadeWindow(true));

        fmtExclude = RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing");
        fmtPng = RegisterClipboardFormatW("PNG");
        msgTaskbar = RegisterWindowMessageW("TaskbarCreated");

        AddTray();
        LoadConfig();
        LoadClips();
        EnsureAbbrDates();
        if (dataMissing) Balloon("ClipAtalho", "A pasta de dados não está acessível agora. Usando os dados da pasta do programa.");
        if (iniCreated || StartupEnabled()) SetStartup(true); // padrão na 1ª execução; mantém o caminho certo se a pasta mudou
        string first = GetClipboardText();
        if (first != null && first.Trim().Length > 0) AddCopied(first);
        AddClipboardFormatListener(hwndMain);
        SetTimer(hwndMain, (IntPtr)TIMER_CONFIG, 2000, IntPtr.Zero);

        hHook = SetWindowsHookExW(13, Marshal.GetFunctionPointerForDelegate(hookProcRef), hInst, 0);
        mouseProcRef = MouseProc; // o gancho do mouse só é ligado enquanto a lista rápida está aberta
        Log("Gancho de teclado: " + (hHook == IntPtr.Zero ? "FALHOU (erro " + Marshal.GetLastWin32Error() + ")" : "ativo"));
        if (hHook == IntPtr.Zero)
            Balloon("ClipAtalho", "Não foi possível ativar a expansão de texto; os atalhos de colagem continuam funcionando.");
        else
            Balloon("ClipAtalho ativo", cfgModsLabel + "+1..9 cola o clip da posição \u00B7 " + cfgModsLabel +
                "+0 mostra a lista \u00B7 clique no ícone para abrir.");

        MSG msg;
        while (GetMessageW(out msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == 0x0100 && hwndMgr != IntPtr.Zero && hDlg == IntPtr.Zero && (page == 0 || page == 1) && KeyDown(0x11) &&
                (msg.wParam.ToInt32() == 'Z' || msg.wParam.ToInt32() == 'Y') && (msg.hwnd == hwndMgr || GetParent(msg.hwnd) == hwndMgr))
            {
                IntPtr f = GetFocus(); // dentro de um texto em edição, o Ctrl+Z é do próprio campo
                bool typing = (f == hEdit && editDirty) || ((f == hXText || f == hXAbbr) && expDirty);
                if (!typing) { DoUndo(page, msg.wParam.ToInt32() == 'Y'); continue; }
            }
            if (hwndMgr != IntPtr.Zero && IsDialogMessageW(hwndMgr, ref msg)) continue;
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        if (hHook != IntPtr.Zero) UnhookWindowsHookEx(hHook);
        if (hMouseHook != IntPtr.Zero) UnhookWindowsHookEx(hMouseHook);
        GC.KeepAlive(mutex);
    }
}
