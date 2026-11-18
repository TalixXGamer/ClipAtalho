// ClipAtalho - gerenciador de área de transferência portátil + expansão de texto.
// Versão 1.4.0 17-11-2026. Compilar: dê dois cliques no "Compilar ClipAtalho.bat" (usa o csc.exe que já vem no Windows).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
        WM_TRAY = 0x8001, WM_SHOWPOPUP = 0x8002, WM_HIDEPOPUP = 0x8003, WM_FEEDBACKDONE = 0x8010, WM_POPSCROLL = 0x8011, WM_CLOUDDONE = 0x8012, WM_CLOUDCHECK = 0x8013, WM_CLOUDAUTH = 0x8014, WM_POPFILTER = 0x8015, WM_CLOUDLIVE = 0x8016;
    const int TIMER_WAITMODS = 1, TIMER_RESTORE = 2, TIMER_POPUP = 3, TIMER_CONFIG = 4, TIMER_FADE = 5,
        TIMER_WAVE = 6, TIMER_CLIPREAD = 7, TIMER_ARM = 8, TIMER_CLOUDASK = 9, TIMER_LIVE = 10;
    const uint CF_UNICODETEXT = 13;
    const int VK_BACK = 0x08, VK_TAB = 0x09, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12,
        VK_ESCAPE = 0x1B, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    const int MODE_NONE = 0, MODE_SUGGEST = 1, MODE_HISTORY = 2, MODE_NOTICE = 3;
    const int ID_LIST = 10, MAX_IMAGES = 100;
    static int cfgLimit = 100; // 0 = sem limite
    static int MaxClips { get { return cfgLimit <= 0 ? int.MaxValue : cfgLimit; } }
    static readonly int[] LimitValues = { 20, 50, 100, 200, 0 };
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
        public bool Empty;                    // vaga livre: posição fixada guardada, sem conteúdo
        public byte[] Html, Rtf;              // formatação original (negrito, cores, links), quando houver
        public bool HasFormat { get { return Html != null || Rtf != null; } }
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
    static int cfgDateFmt; // 0 = 16/11/2026, 1 = 16-11-2026, 2 = 16112026
    static bool cfgYear2, cfgAmPm, cfgDateKey = true, cfgDateAbbrDone;
    static readonly string[] DateFmtKeys = { "barra", "hifen", "junto" };
    static readonly string[] MonthNames = { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };

    static string DateStr(DateTime d)
    {
        string sep = cfgDateFmt == 1 ? "-" : cfgDateFmt == 2 ? "" : "/";
        return d.ToString("dd") + sep + d.ToString("MM") + sep + (cfgYear2 ? d.ToString("yy") : d.ToString("yyyy"));
    }

    static string HourStr(DateTime d)
    {
        return cfgAmPm ? d.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture) : d.ToString("HH:mm") + "h";
    }

    // {data} {hora} {datahora} {extenso}: trocados pela data e hora do momento em que se cola.
    static string ExpandTokens(string v)
    {
        if (v.IndexOf('{') < 0) return v;
        DateTime n = DateTime.Now;
        return v.Replace("{datahora}", DateStr(n) + " " + HourStr(n)).Replace("{data}", DateStr(n)).Replace("{hora}", HourStr(n))
            .Replace("{extenso}", n.Day + " de " + MonthNames[n.Month - 1] + " de " + n.Year);
    }
    // Limpeza automática: 0 nunca | 1 itens mais antigos que cfgAutoDel minutos | 2 todo dia | 3 toda semana | 4 a cada N horas
    static int autoMode, cfgAutoDel, autoHour, autoMinute, autoWeekDay, autoEvery;
    static DateTime lastClean = DateTime.MinValue;
    static readonly string[] AutoDelKeys = { "30min", "1h", "2h", "24h" };
    static readonly string[] AutoDelNames = { "Itens com mais de 30 minutos", "Itens com mais de 1 hora", "Itens com mais de 2 horas", "Itens com mais de 24 horas" };
    static readonly int[] AutoDelMinutes = { 30, 60, 120, 1440 };
    static readonly string[] WeekNames = { "domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado" };

    static bool ParseHM(string t, out int h, out int m)
    {
        h = m = 0;
        string[] p = t.Trim().Replace('h', ':').Split(':');
        if (p.Length < 1 || !int.TryParse(p[0], out h)) return false;
        if (p.Length > 1 && p[1].Length > 0 && !int.TryParse(p[1], out m)) return false;
        return h >= 0 && h <= 23 && m >= 0 && m <= 59;
    }

    static void ParseAutoDel(string val)
    {
        string[] p = val.Trim().ToLowerInvariant().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        autoMode = 0;
        if (p.Length == 0) return;
        int ai = Array.IndexOf(AutoDelKeys, p[0]);
        if (ai >= 0) { autoMode = 1; cfgAutoDel = AutoDelMinutes[ai]; return; }
        if (p[0] == "meianoite") { autoMode = 2; autoHour = 0; autoMinute = 0; return; } // versões antigas
        if (p[0] == "diario" && p.Length > 1 && ParseHM(p[1], out autoHour, out autoMinute)) { autoMode = 2; return; }
        if (p[0] == "semanal" && p.Length > 2 && int.TryParse(p[1], out autoWeekDay) && autoWeekDay >= 0 && autoWeekDay <= 6
            && ParseHM(p[2], out autoHour, out autoMinute)) { autoMode = 3; return; }
        if (p[0] == "cada" && p.Length > 1 && int.TryParse(p[1], out autoEvery) && autoEvery >= 1 && autoEvery <= 720) { autoMode = 4; return; }
    }

    static string AutoDelLabel()
    {
        switch (autoMode)
        {
            case 1: return AutoDelNames[Math.Max(0, Array.IndexOf(AutoDelMinutes, cfgAutoDel))];
            case 2: return "Zerar todo dia às " + autoHour.ToString("00") + ":" + autoMinute.ToString("00");
            case 3: return "Zerar toda " + WeekNames[autoWeekDay] + " às " + autoHour.ToString("00") + ":" + autoMinute.ToString("00");
            case 4: return "Zerar a cada " + autoEvery + (autoEvery == 1 ? " hora" : " horas");
        }
        return "Nunca";
    }

    // Último horário agendado que já passou (para saber se a limpeza está pendente).
    static DateTime LastScheduled(DateTime now)
    {
        DateTime t = now.Date.AddHours(autoHour).AddMinutes(autoMinute);
        if (autoMode == 2) return t <= now ? t : t.AddDays(-1);
        int back = ((int)now.DayOfWeek - autoWeekDay + 7) % 7;
        t = t.AddDays(-back);
        return t <= now ? t : t.AddDays(-7);
    }
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
            if (nCode == 0 && popMode != MODE_NONE && hMenuWnd == IntPtr.Zero)
            {
                uint m = (uint)wParam.ToInt64();
                if (m == 0x020A && popMode == MODE_HISTORY) // roda do mouse sobre a lista: rola
                {
                    int x0 = Marshal.ReadInt32(lParam), y0 = Marshal.ReadInt32(lParam, 4);
                    RECT pr;
                    if (GetWindowRect(hwndPopup, out pr) && x0 >= pr.Left && x0 < pr.Right && y0 >= pr.Top && y0 < pr.Bottom)
                    {
                        int delta = (short)((Marshal.ReadInt32(lParam, 8) >> 16) & 0xFFFF);
                        PostMessageW(hwndMain, WM_POPSCROLL, (IntPtr)(delta > 0 ? -3 : 3), IntPtr.Zero);
                        return (IntPtr)1;
                    }
                }
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
    static bool[] registeredOK = new bool[31]; // 1..10 = teclado principal, 11..20 = teclado numérico, 21..29 = +Shift (sem formatação)

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
; Limpeza automática dos clips NÃO fixados (os fixados nunca são apagados):
;   nunca | 30min | 1h | 2h | 24h (itens mais antigos que isso) | diario 18:00 | semanal 1 08:00 (0 = domingo) | cada 6 (horas)
autoexclusao=nunca
; Tamanho máximo da lista: 20, 50, 100, 200 ou 0 (sem limite). Imagens: no máximo 100.
limite=100
; Data dos atalhos {data} e da tecla Ctrl+Shift+D: barra (16/11/2026), hifen (16-11-2026) ou junto (16112026); ano com 4 ou 2 dígitos
data_formato=barra
ano=4
; Hora do atalho {hora}: 24h (23:06h) ou ampm (11:06 PM)
hora_formato=24h
; Ctrl+Shift+D cola a data de hoje: sim ou nao
tecla_data=sim
atalhos_data=sim

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
        "• Ctrl+0: abre a lista com todos os textos, perto de onde você está escrevendo. Clique para colar ou aperte 1 a 9. Botão direito: colar sem formatação, fixar, mudar de posição ou excluir. Arraste um item para mudar a posição. Fecha com Esc ou com um clique fora.\n\n" +
        "COMO OS TEXTOS ENTRAM NA LISTA\n" +
        "• Tudo que você copia (Ctrl+C) entra sozinho na lista, inclusive imagens e prints.\n" +
        "• Para criar à mão: clique no ícone do ClipAtalho perto do relógio › “+ Novo” › digite › “Salvar”.\n\n" +
        "FIXAR (★)\n" +
        "• Um clip fixado não sai do lugar e fica sempre guardado. Os não fixados vão descendo a cada nova cópia.\n\n" +
        "ATALHOS POR EXPANSÃO\n" +
        "• Cadastre um atalho curto e o texto dele (ex.: bd1 = Bom dia! Tudo bem?).\n" +
        "• Em qualquer programa, digite o atalho logo depois de um espaço: aparece a sugestão. Aperte Tab e o texto entra no lugar. Esc ignora.\n" +
        "• Já vem com exemplos: hj1 (data de hoje), hr1 (hora), dh1, ext1, b1, bd1, bt1, bn1, obg, agd, att, end1, ia3d e cla.\n\n" +
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
        new string[] { "cla", "ClipAtalho: escreva menos e faça mais ⚡" },
        new string[] { "hj1", "{data}" },
        new string[] { "hr1", "{hora}" },
        new string[] { "dh1", "{datahora}" },
        new string[] { "ext1", "{extenso}" }
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
        if (section == "geral" && !cfgUndoing && key != "lista_pos" && key != "ultima_limpeza" && key != "atalhos_data") PushCfgUndo();
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

    // Atalho: até 32 caracteres; aceita letras (com acento), números, símbolos e espaço no meio.
    // Não pode: "=" (separa atalho e texto no .ini), quebra de linha, começar com "-", ";" ou "[", nem espaço nas pontas.
    static bool ValidAbbr(string s)
    {
        if (s.Length == 0 || s.Length > 32 || s.Trim() != s) return false;
        if (s[0] == '-' || s[0] == ';' || s[0] == '[') return false;
        foreach (char c in s) if (c == '=' || char.IsControl(c)) return false;
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
        autoMode = 0;
        cfgLimit = 100;
        cfgDateFmt = 0;
        cfgYear2 = false;
        cfgAmPm = false;
        cfgDateKey = true;
        cfgDateAbbrDone = false;
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
                else if (key == "autoexclusao") ParseAutoDel(val);
                else if (key == "data_formato") cfgDateFmt = Math.Max(0, Array.IndexOf(DateFmtKeys, val.ToLowerInvariant()));
                else if (key == "ano") cfgYear2 = val.Trim() == "2";
                else if (key == "hora_formato") cfgAmPm = val.Trim().ToLowerInvariant() == "ampm";
                else if (key == "tecla_data") cfgDateKey = val.Trim().ToLowerInvariant() != "nao";
                else if (key == "atalhos_data") cfgDateAbbrDone = val.Trim().ToLowerInvariant() == "sim";
                else if (key == "ultima_limpeza")
                {
                    DateTime lt;
                    if (DateTime.TryParseExact(val, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out lt)) lastClean = lt;
                }
                else if (key == "limite")
                {
                    string lv = val.ToLowerInvariant();
                    int li;
                    if (lv == "sem" || lv == "semlimite" || lv == "0") cfgLimit = 0;
                    else if (int.TryParse(lv, out li) && li >= 5) cfgLimit = li;
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
        cloudProv = 0;
        cloudDir = "";
        cloudRefresh = null;
        cloudMode = 0;
        cloudCheck = true;
        cloudLastSync = "";
        backupDir = "";
        cloudLive = false;
        cloudLiveImg = false;
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
            else if (k == "nuvem") cloudProv = v == "onedrive" ? 1 : v == "pasta" ? 2 : 0;
            else if (k == "nuvem_pasta") cloudDir = v;
            else if (k == "nuvem_token") cloudRefresh = Unprotect(v);
            else if (k == "nuvem_modo") cloudMode = Math.Max(0, Array.IndexOf(CloudModeKeys, v.ToLowerInvariant()));
            else if (k == "nuvem_aviso") cloudCheck = v.ToLowerInvariant() != "nao";
            else if (k == "nuvem_sinc") cloudLastSync = v;
            else if (k == "onedrive_client_id") cloudClientOverride = v;
            else if (k == "pasta_backups") backupDir = v;
            else if (k == "nuvem_tempo_real") cloudLive = v.ToLowerInvariant() == "sim";
            else if (k == "nuvem_tempo_real_imagens") cloudLiveImg = v.ToLowerInvariant() == "sim";
        }
        if (dataDir.Length == 0) dataOn = false;
    }

    static readonly object localLock = new object();

    static void SaveLocal()
    {
        lock (localLock)
        {
            try
            {
                string tok = cloudRefresh != null ? Protect(cloudRefresh) : "";
                File.WriteAllText(LocalIniPath, "; Configuração só deste computador (não vai para a pasta de dados nem para a nuvem)\r\n[local]\r\nusar_pasta=" +
                    (dataOn ? "sim" : "nao") + "\r\npasta=" + dataDir + "\r\nconflito=" + ConflictKeys[conflictMode] +
                    "\r\nnuvem=" + (cloudProv == 1 ? "onedrive" : cloudProv == 2 ? "pasta" : "") + "\r\nnuvem_pasta=" + cloudDir +
                    "\r\n; acesso ao OneDrive deste computador (criptografado pelo Windows; só funciona neste usuário)\r\nnuvem_token=" + tok +
                    "\r\nnuvem_modo=" + CloudModeKeys[cloudMode] + "\r\nnuvem_aviso=" + (cloudCheck ? "sim" : "nao") + "\r\nnuvem_sinc=" + cloudLastSync +
                    "\r\nnuvem_tempo_real=" + (cloudLive ? "sim" : "nao") + "\r\nnuvem_tempo_real_imagens=" + (cloudLiveImg ? "sim" : "nao") +
                    "\r\npasta_backups=" + backupDir +
                    (cloudClientOverride.Length > 0 ? "\r\nonedrive_client_id=" + cloudClientOverride : "") + "\r\n", new UTF8Encoding(true));
            }
            catch (Exception e) { Log("FALHA ao gravar ClipAtalho.local.ini: " + e.Message); }
        }
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
        EnsureDateAbbr();
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
        v = ExpandTokens(v);
        return v.Length > 0 ? v : null;
    }

    static void RegisterHotkeys()
    {
        for (int i = 1; i <= 30; i++)
            if (registeredOK[i]) { UnregisterHotKey(hwndMain, i); registeredOK[i] = false; }
        if (cfgDateKey) // Ctrl+Shift+D: cola a data de hoje
        {
            registeredOK[30] = RegisterHotKey(hwndMain, 30, 2 | 4 | 0x4000, (uint)'D');
            if (!registeredOK[30]) Log("Ctrl+Shift+D não registrado (usado por outro programa)");
        }
        if ((cfgMods & 4) == 0) // mesma combinação + Shift: cola sem formatação
            for (int d = 1; d <= 9; d++) registeredOK[20 + d] = RegisterHotKey(hwndMain, 20 + d, cfgMods | 4 | 0x4000, (uint)('0' + d));
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
        foreach (Clip c in clips) if (c.Pinned && Same(c, cand))
        {
            if (!c.HasFormat && cand.HasFormat) { c.Html = cand.Html; c.Rtf = cand.Rtf; SaveClips(); }
            DropImage(cand);
            LiveSend(c);
            return;
        }
        Clip front = null;
        foreach (Clip c in clips) if (!c.Pinned && Same(c, cand)) { front = c; break; }
        if (front == null) front = cand;
        else
        {
            if (cand.HasFormat) { front.Html = cand.Html; front.Rtf = cand.Rtf; } // fica com a formatação mais recente
            DropImage(cand);
        }
        List<Clip> un = new List<Clip>();
        un.Add(front);
        int pinned = 0;
        foreach (Clip c in clips)
        {
            if (c.Pinned) pinned++;
            else if (c != front && !Same(c, cand)) un.Add(c);
        }
        int maxUn = Math.Max(0, MaxClips - pinned);
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
        int imgs = 0;
        for (int i = 0; i < res.Count; i++) if (res[i].IsImage) imgs++;
        for (int i = res.Count - 1; i >= 0 && imgs > MAX_IMAGES; i--)
            if (res[i].IsImage && !res[i].Pinned) { res.RemoveAt(i); imgs--; } // imagens pesam: no máximo 100
        clips = res;
        ReleaseUnusedImages();
        if (cfgSaveAll) SaveClips();
        RefreshList();
        LiveSend(front); // tempo real: manda para o outro computador
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
                if (c.Empty) { sb.Append("vaga=\r\n"); continue; } // posição livre guardada
                if (c.Text.Length == 0) continue;
                sb.Append(c.Pinned ? "fixo=" : "normal=").Append(EscClip(c.Text)).Append("\r\n");
                if (c.Html != null) sb.Append("html=").Append(Convert.ToBase64String(c.Html)).Append("\r\n"); // formatação do clip acima
                if (c.Rtf != null) sb.Append("rtf=").Append(Convert.ToBase64String(c.Rtf)).Append("\r\n");
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
                if (eq < 1 || clips.Count >= MaxClips) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                if (key == "fixo_img" || key == "normal_img")
                {
                    Clip ic = LoadImageClip(line.Substring(eq + 1), key == "fixo_img");
                    if (ic != null) clips.Add(ic);
                    continue;
                }
                if ((key == "html" || key == "rtf") && clips.Count > 0 && !clips[clips.Count - 1].IsImage) // formatação do clip anterior
                {
                    try
                    {
                        byte[] b = Convert.FromBase64String(line.Substring(eq + 1).Trim());
                        if (key == "html") clips[clips.Count - 1].Html = b; else clips[clips.Count - 1].Rtf = b;
                    }
                    catch { }
                    continue;
                }
                if (key == "vaga") { Clip vg = new Clip("", true); vg.Empty = true; clips.Add(vg); continue; }
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
            if (line.Length < 3 || line[1] != '\t' || clips.Count >= MaxClips) continue;
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
    static uint fmtHtml, fmtRtf;
    const int MAX_FORMAT_BYTES = 1024 * 1024; // formatação maior que 1 MB não é guardada (só o texto)

    // Copia os bytes de um formato da área de transferência (abre e fecha rapidinho).
    static byte[] GetClipboardBytes(uint fmt)
    {
        if (fmt == 0 || !IsClipboardFormatAvailable(fmt) || !OpenClip()) return null;
        try
        {
            IntPtr h = GetClipboardData(fmt);
            if (h == IntPtr.Zero) return null;
            ulong size = GlobalSize(h).ToUInt64();
            if (size == 0 || size > (ulong)MAX_FORMAT_BYTES) return null;
            IntPtr p = GlobalLock(h);
            if (p == IntPtr.Zero) return null;
            try
            {
                byte[] b = new byte[(int)size];
                Marshal.Copy(p, b, 0, b.Length);
                return b;
            }
            finally { GlobalUnlock(h); }
        }
        catch { return null; }
        finally { CloseClipboard(); }
    }

    static IntPtr BytesToGlobal(byte[] b)
    {
        IntPtr h = GlobalAlloc(2, new UIntPtr((ulong)b.Length + 1));
        if (h == IntPtr.Zero) return IntPtr.Zero;
        IntPtr p = GlobalLock(h);
        if (p == IntPtr.Zero) { GlobalFree(h); return IntPtr.Zero; }
        Marshal.Copy(b, 0, p, b.Length);
        Marshal.WriteByte(p, b.Length, 0);
        GlobalUnlock(h);
        return h;
    }

    // Texto + formatação original: cada programa usa o formato mais rico que entende.
    static bool SetClipboardRich(Clip c)
    {
        char[] chars = (c.Text.Replace("\0", "") + "\0").ToCharArray();
        IntPtr h = GlobalAlloc(2, new UIntPtr((uint)(chars.Length * 2)));
        if (h == IntPtr.Zero) return false;
        IntPtr p = GlobalLock(h);
        if (p == IntPtr.Zero) { GlobalFree(h); return false; }
        Marshal.Copy(chars, 0, p, chars.Length);
        GlobalUnlock(h);
        IntPtr hh = c.Html != null && fmtHtml != 0 ? BytesToGlobal(c.Html) : IntPtr.Zero;
        IntPtr hr = c.Rtf != null && fmtRtf != 0 ? BytesToGlobal(c.Rtf) : IntPtr.Zero;
        if (!OpenClip())
        {
            GlobalFree(h);
            if (hh != IntPtr.Zero) GlobalFree(hh);
            if (hr != IntPtr.Zero) GlobalFree(hr);
            return false;
        }
        try
        {
            EmptyClipboard();
            if (SetClipboardData(CF_UNICODETEXT, h) == IntPtr.Zero) GlobalFree(h);
            if (hh != IntPtr.Zero && SetClipboardData(fmtHtml, hh) == IntPtr.Zero) GlobalFree(hh);
            if (hr != IntPtr.Zero && SetClipboardData(fmtRtf, hr) == IntPtr.Zero) GlobalFree(hr);
            if (fmtExclude != 0)
            {
                IntPtr h2 = GlobalAlloc(2, new UIntPtr(4));
                if (h2 != IntPtr.Zero && SetClipboardData(fmtExclude, h2) == IntPtr.Zero) GlobalFree(h2);
            }
            return true;
        }
        finally { CloseClipboard(); }
    }

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
        if (t != null && t.Trim().Length > 0 && !(hasImg && onlyLink)) // havendo texto, ele tem preferência (ex.: células do Excel)
        {
            Clip tc = new Clip(t, false);
            tc.Html = GetClipboardBytes(fmtHtml); // formatação original (Word, navegadores, e-mails...)
            tc.Rtf = GetClipboardBytes(fmtRtf);
            AddCopiedClip(tc);
        }
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
    static Clip pendImg, pendRich;

    static void PasteClip(Clip c) { PasteClip(c, false); }

    // plain = "Colar sem formatação": só o texto, sem negrito, cores ou links.
    static void PasteClip(Clip c, bool plain)
    {
        if (c == null || c.Empty || (!c.IsImage && c.Text.Length == 0)) { ShowNotice("Você ainda não salvou nada nesta posição."); return; }
        if (c.IsImage)
        {
            if (c.Image == IntPtr.Zero) return;
            StartPaste("", 0);
            pendImg = c;
        }
        else if (c.Text.Length > 0)
        {
            StartPaste(c.Text, 0);
            if (!plain && c.HasFormat) pendRich = c;
        }
    }

    static void StartPaste(string text, int backspaces)
    {
        pendImg = null;
        pendRich = null;
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
        if (!(pendImg != null ? SetClipboardImage(pendImg) : pendRich != null ? SetClipboardRich(pendRich) : SetClipboardText(pendText)))
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
    static bool popNumbered, popDrag, popHoverTrash, popClearArmed, popHoverStar, popItemDrag;
    static int popPress = -1, popPressY, popDropLine = -1;
    static Clip popTrashArmed;
    static POINT popDragStart;
    static RECT popDragRect;
    static float[] popScale = new float[0]; // 0 = tamanho normal, 1 = ampliado (efeito jukebox)
    static IntPtr[] popFonts = new IntPtr[4];
    static IntPtr popMem, popMemBmp, popMemOld, hIconFont, hIconBig, hSearch, hXSearch;
    static int popMemW, popMemH;
    const int POP_ROW = 30;

    const int POP_VISIBLE = 12; // itens visíveis de uma vez; o resto rola com a roda do mouse
    static int popScroll;
    static int PopVisible() { return Math.Min(Math.Max(0, popLines.Count - 1), POP_VISIBLE); }
    static int PopupHeight() { return Px(16) + (1 + PopVisible()) * Px(POP_ROW); }
    static int PopRowTop(int i) { return Px(8) + (i == 0 ? 0 : i - popScroll) * Px(POP_ROW); }
    static bool PopRowVisible(int i) { return i == 0 || (i > popScroll && i <= popScroll + PopVisible()); }

    static void PopupScroll(int rows)
    {
        int max = Math.Max(0, popLines.Count - 1 - PopVisible());
        int ns = Math.Max(0, Math.Min(max, popScroll + rows));
        if (ns == popScroll) return;
        popScroll = ns;
        popHover = -1;
        popHoverTrash = popHoverStar = false;
        InvalidateRect(hwndPopup, IntPtr.Zero, false);
        SetTimer(hwndMain, (IntPtr)TIMER_POPUP, 20000, IntPtr.Zero);
    }

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
            string pf = Fold(popFilter);
            popLines.Add(pf.Length > 0 ? "🔍 " + popFilter + "   ·   Esc limpa a busca" : "Clique cola  ·  digite para pesquisar  ·  botão direito: opções");
            for (int i = 0; i < clips.Count; i++) // todos os clips, fixados ou não (ou só os que batem com a busca)
            {
                Clip c = clips[i];
                if (!ClipMatches(c, pf)) continue;
                popClips.Add(c);
                popLines.Add(c.Empty ? "(posição livre)" : (c.Pinned ? "★ " : "") + (c.IsImage ? "Imagem  ·  " + c.W + " × " + c.H + " px" : Preview(c.Text, 140)));
            }
            if (popClips.Count == 0) popLines.Add("Nada encontrado para \"" + popFilter + "\"");
            popNumbered = true;
        }
        popScale = new float[popLines.Count];
        popScroll = Math.Max(0, Math.Min(popScroll, popLines.Count - 1 - PopVisible()));
    }

    static void ShowHistory()
    {
        popScroll = 0;
        popFilter = "";
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
        if (!PopRowVisible(i)) return;
        r.Top = PopRowTop(i) - Px(2);
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
        IDC_UNDO = 192, IDC_REDO = 193, IDC_XUNDO = 194, IDC_XREDO = 195, IDC_CUNDO = 196, IDC_CREDO = 197, IDC_LIMIT = 198, IDC_IN_EDIT = 214,
        IDC_SEG3 = 220, IDC_CLOUDPROV = 221, IDC_CLOUDCONN = 222, IDC_CLOUDUP = 223, IDC_CLOUDDOWN = 224, IDC_CLOUDMODE = 225,
        IDC_T_CLOUDCHECK = 226, IDC_DATEFMT = 227, IDC_YEARFMT = 228, IDC_T_DATEKEY = 229, IDC_HOURFMT = 230,
        IDC_SEARCH = 231, IDC_XSEARCH = 232, IDC_TOEXP = 233, IDC_GOCLOUD = 234, IDC_T_CLOUDLIVE = 235, IDC_T_CLOUDLIVEIMG = 236,
        IDC_FB_SUG = 210, IDC_FB_REC = 211, IDC_FB_MAIL = 212, IDC_FB_MSG = 213;
    const string RUNKEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string INSTAGRAM = "https://www.instagram.com/talespsg";
    const string VERSION = "1.4.0 17-11-2026";
    static readonly string[] PageTitles = { "Seus clips", "Atalhos por Expansão", "Configurações", "Ajuda", "Configurações", "Configurações" };
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
    static bool dlgFeedback, fbComplaint, dlgInput;
    static int dlgExtra;
    static string dlgRedNote = ""; // aviso em vermelho abaixo da mensagem
    static string dlgPrompt = "";
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
        hSearch = Child(hwndMgr, "EDIT", "", 0x10000 | 0x80, 46, 84, 300, 20, IDC_SEARCH, 0);
        hList = Child(hwndMgr, "LISTBOX", "", 0x00200000 | 0x10000 | 0x1 | 0x10 | 0x40 | 0x100, 16, 114, 336, 364, IDC_LIST, 0);
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
        hXSearch = Child(hwndMgr, "EDIT", "", 0x10000 | 0x80, 46, 120, 300, 20, IDC_XSEARCH, 1);
        hXList = Child(hwndMgr, "LISTBOX", "", 0x00200000 | 0x10000 | 0x1 | 0x10 | 0x40 | 0x100, 16, 150, 336, 328, IDC_XLIST, 1);
        Btn(16, 78, 226, 30, IDC_XSORT, 1);
        Btn(248, 78, 104, 30, IDC_HOURFMT, 1);
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
        Btn(36, 384, 740, 30, IDC_T_DATEKEY, 2);
        Btn(96, 422, 196, 30, IDC_DATEFMT, 2);
        Btn(300, 422, 230, 30, IDC_YEARFMT, 2);
        Btn(36, 462, 170, 30, IDC_OPENINI, 2);
        Btn(214, 462, 210, 30, IDC_OPENDIR, 2);
        Btn(452, 46, 92, 28, IDC_SEG, 24);       // Configurações: "Geral" | "Dados e backup" | "Nuvem"
        Btn(550, 46, 140, 28, IDC_SEG + 1, 24);
        Btn(696, 46, 108, 28, IDC_SEG3, 24);
        Btn(384, 46, 28, 28, IDC_CUNDO, 24);     // desfazer / refazer das configurações
        Btn(416, 46, 28, 28, IDC_CREDO, 24);

        // página 5: nuvem (sub-página de Configurações)
        Btn(36, 134, 380, 32, IDC_CLOUDPROV, 5);
        Btn(424, 134, 220, 32, IDC_CLOUDCONN, 5);
        Btn(36, 204, 362, 52, IDC_CLOUDUP, 5);
        Btn(412, 204, 364, 52, IDC_CLOUDDOWN, 5);
        Btn(36, 326, 480, 32, IDC_CLOUDMODE, 5);
        Btn(36, 362, 740, 30, IDC_T_CLOUDCHECK, 5);
        Btn(36, 396, 740, 30, IDC_T_CLOUDLIVE, 5);
        Btn(76, 428, 700, 30, IDC_T_CLOUDLIVEIMG, 5);

        // página 4: dados e backup (sub-página de Configurações)
        Btn(36, 132, 176, 32, IDC_BK_CLIPS, 4);
        Btn(218, 132, 196, 32, IDC_BK_X, 4);
        Btn(420, 132, 160, 32, IDC_BK_ALL, 4);
        Btn(586, 132, 190, 32, IDC_RESTOREBK, 4);
        Btn(540, 178, 236, 30, IDC_LIMIT, 4);
        Btn(540, 214, 236, 30, IDC_AUTODEL, 4);
        Btn(200, 254, 576, 22, IDC_GOCLOUD, 4);
        Btn(36, 278, 740, 30, IDC_T_DATADIR, 4);
        Btn(36, 334, 150, 32, IDC_DATADIR, 4);
        Btn(414, 334, 362, 32, IDC_CONFLICT, 4);
        Btn(36, 422, 260, 34, IDC_FACTORY, 4);

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
            ShowWindow(ctrlHandles[i], (ctrlPages[i] == p || ctrlPages[i] == -1 || (ctrlPages[i] == 24 && (p == 2 || p == 4 || p == 5))) ? 5 : 0);
        if (p == 0) RefreshList();
        if (p == 1) RefreshX(true);
        if (p == 2 || p == 4) startupOn = StartupEnabled();
        if (p == 3) SetWindowTextW(hHelp, HelpText());
        UpdateEditVisibility();
        UpdateSearchBoxes();
        RepaintAll();
    }

    // O campo de busca aparece ao clicar na caixa "Pesquisar…" (ou Ctrl+F); vazio e sem foco, some e mostra a dica.
    static void UpdateSearchBoxes()
    {
        if (hwndMgr == IntPtr.Zero) return;
        IntPtr f = GetFocus();
        ShowWindow(hSearch, page == 0 && (clipFilter.Length > 0 || f == hSearch) ? 5 : 0);
        ShowWindow(hXSearch, page == 1 && (xFilter.Length > 0 || f == hXSearch) ? 5 : 0);
    }

    static void OpenSearch()
    {
        IntPtr h = page == 0 ? hSearch : page == 1 ? hXSearch : IntPtr.Zero;
        if (h == IntPtr.Zero) return;
        ShowWindow(h, 5);
        SetFocus(h);
        SendMessageW(h, 0x00B1, IntPtr.Zero, (IntPtr)(-1));
    }

    static void ClearSearch()
    {
        IntPtr h = GetFocus();
        if (h != hSearch && h != hXSearch) return;
        SetWindowTextW(h, "");
        SetFocus(h == hSearch ? hList : hXList);
        UpdateSearchBoxes();
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
            "•  " + m + "+0: mostra a lista rápida com todos os clips (fixados e recentes). Clique em um item para colar, ou aperte 1 a 9. A lixeira que aparece no item sob o mouse o apaga (num clip fixado, clique duas vezes para confirmar), a estrela fixa ou solta, arrastar um item muda a posição dele e o botão direito traz Colar sem formatação, Subir, Descer, Mover para o topo/fim e Excluir. \"Limpar\" (clicado duas vezes) apaga todos os não fixados. A lista abre logo abaixo (ou acima) da linha onde você está escrevendo, sem cobri-la. Fecha com Esc, com o X ou com um clique fora dela. A tecla Print Screen (e o Win+Shift+S) não a fecha, para você poder capturá-la. Para mudar a lista de lugar, arraste a faixa de cima: ela volta a abrir onde você deixou.\n\n" +
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
            "•  Desfazer / refazer: botões ↶ ↷ no canto do cartão, ou Ctrl+Z / Ctrl+Y. Vale para editar, mover, fixar, excluir, limpar, restaurar e também para as ações feitas na lista rápida (Ctrl+0). Dentro do texto em edição, o Ctrl+Z desfaz a digitação palavra por palavra, quantas vezes quiser.\n" +
            "•  Formatação: ao copiar um texto com negrito, cores ou links (Word, navegador, e-mail), o clip guarda a formatação e cola formatado. Para colar só o texto: Ctrl+Shift+1 a 9, Shift+clique (ou Shift+número) na lista rápida, ou botão direito › \"Colar sem formatação\". Editar um clip formatado o transforma em texto sem formatação.\n\n" +
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
            "•  Aparência: tema claro ou escuro e a cor da janela.\n" +
            "•  Errou num ajuste? Ctrl+Z (ou os botões ↶ ↷ no alto) desfaz também as mudanças nas configurações.\n\n" +
            "DADOS E BACKUP (Configurações › Dados e backup)\n" +
            "•  Backup: gera listas em texto (.txt), uma de clips e outra de expansões. O \"Backup completo\" gera as duas e também um arquivo de configurações (.ini: teclas, tema, cor e opções). Entre um item e outro ficam duas linhas em branco. Nos clips, a 1ª linha é o número (★ = fixado); nas expansões, a 1ª linha é o atalho (com - na frente = não fixado) e depois vem o texto. Imagens vão numa pasta \"... - imagens\" ao lado.\n" +
            "•  Os backups vão para a pasta \"Backups do ClipAtalho\" (com o ícone do programa), criada na primeira vez no lugar que você escolher; dá para escolher outra quando quiser.\n" +
            "•  Backup automático: todo dia o programa guarda uma cópia na pasta \"Backups automáticos\", ao lado dele, mantendo os últimos 7 dias. Se apagar algo sem querer hoje, a cópia de ontem ainda tem.\n" +
            "•  Restaurar backup: aponte o arquivo ou deixe o ClipAtalho procurar numa pasta ou disco. Escolha um ou mais arquivos (pode marcar os 3 do backup completo juntos). Nas listas, dá para substituir a atual ou adicionar os itens a ela; as configurações são aplicadas depois de confirmar.\n" +
            "•  Tamanho máximo da lista: 20, 50, 100 (padrão), 200 ou sem limite. Imagens: no máximo 100. Na lista rápida, o que não couber rola com a roda do mouse.\n" +
            "•  Limpeza automática dos não fixados: itens com mais de 30 min / 1 h / 2 h / 24 h, ou zerar a lista todo dia num horário, toda semana (dia e horário) ou a cada X horas. Se o computador estiver desligado no horário, a limpeza acontece quando o programa abrir. Os fixados nunca são apagados.\n" +
            "•  Pasta de dados: guarda clips e atalhos numa pasta da rede, OneDrive ou Google Drive, para usar os mesmos em vários computadores. Se o arquivo mudar em outro computador, o ClipAtalho pode carregar a versão nova sozinho, perguntar na hora ou perguntar ao fechar; a versão que não ficar é guardada como cópia na pasta.\n" +
            "•  Restaurar padrão de fábrica: apaga tudo deste computador (com sugestão de backup antes). Se houver pasta de dados, ela só é desligada, sem ser apagada.\n\n" +
            "NUVEM (Configurações › Nuvem)\n" +
            "•  Leve clips, imagens, atalhos e configurações para outro computador: \"Enviar para a nuvem\" num computador e \"Baixar da nuvem\" no outro. Antes de substituir, o programa mostra a data, a hora e o computador do backup que já está lá.\n" +
            "•  Onde guardar: OneDrive, direto pela internet, sem instalar nada (o login é feito uma vez, na página da Microsoft, e o ClipAtalho só enxerga a própria pasta Aplicativos › ClipAtalho), ou uma pasta do Google Drive, Dropbox, rede ou pendrive.\n" +
            "•  Depois de alterar algo, o programa pergunta se deseja enviar (ou envia sozinho, se preferir). Ao abrir, avisa se outro computador enviou uma versão mais nova.\n\n" +
            "•  Tempo real (opcional): o que você copiar num computador aparece no outro em poucos segundos, pronto para colar. Imagens só se você ligar essa opção.\n\n" +
            "PESQUISAR\n" +
            "•  Nas telas Clips e Atalhos por Expansão: clique em \"Pesquisar…\" (ou Ctrl+F) e digite; a lista vai afunilando. Vale o texto e, nas expansões, também o atalho. Esc limpa.\n" +
            "•  No Ctrl+0: é só começar a digitar com a lista aberta. Backspace apaga, Esc limpa a busca (e, de novo, fecha).\n\n" +
            "POSIÇÕES E FIXADOS\n" +
            "•  Ao fixar um clip, ele sobe e fica logo abaixo do último fixado.\n" +
            "•  Ao excluir um fixado que tem itens abaixo, você escolhe: \"Puxar para cima\" (os de baixo mudam de número) ou \"Manter posições\" (a posição fica como vaga livre, para você não precisar decorar tudo de novo). Para preencher a vaga, selecione-a e digite o texto.\n" +
            "•  Converter em expansão: botão direito num clip (na lista ou no Ctrl+0) › \"Converter em expansão\". Digite o atalho e salve; depois você escolhe se o clip continua na lista.\n\n" +
            "DATA E HORA\n" +
            "•  Atalhos prontos: hj1 (data), hr1 (hora), dh1 (data e hora) e ext1 (data por extenso). Em qualquer atalho seu dá para usar {data}, {hora}, {datahora} e {extenso}: na hora de colar, viram a data e a hora do momento.\n" +
            "•  Ctrl+Shift+D cola a data de hoje. Formato da data e do ano: Configurações › Geral. Hora em 24h ou AM/PM: botão ao lado de \"Reorganizar\" na tela Atalhos por Expansão.\n\n" +
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
        int estLines = 0;
        foreach (string ml in message.Split('\n')) estLines += Math.Max(1, (ml.Length + 61) / 62);
        if (dlgRedNote.Length > 0) estLines += 1 + (dlgRedNote.Length + 61) / 62;
        dlgExtra = Math.Max(0, estLines - 4) * 20;
        const uint style = 0x80000000 | 0x02000000; // sem barra de título: o programa desenha tudo
        int w = Px(480), h = Px(196 + dlgExtra);
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
            Child(me, "BUTTON", "", 0x10000 | 0xB, bx, 142 + dlgExtra, widths[i], 36, IDC_DLG + 1 + i, -2);
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
                    if (dlgInput) { PaintInput(hdc); EndPaint(hwnd, ref ps); return IntPtr.Zero; }
                    DrawTextAt(hdc, dlgTitle, hHeadFont, C_TEXT, Px(22), Px(12), Px(458), Px(42), 0x8024);
                    int tl = Px(22);
                    if (dlgWarn)
                    {
                        DrawIconEx(hdc, Px(22), Px(52), LoadIconW(IntPtr.Zero, (IntPtr)32515), Px(32), Px(32), 0, IntPtr.Zero, 3); // ícone de alerta
                        tl = Px(68);
                    }
                    DrawTextAt(hdc, dlgMessage.Replace("\n", "\r\n"), hUiFont, C_TEXT, tl, Px(50), Px(458), Px(134 + dlgExtra), 0x10); // quebra de linha
                    if (dlgRedNote.Length > 0) // aviso em vermelho, logo abaixo do texto
                    {
                        RECT mr = new RECT();
                        mr.Left = tl; mr.Top = Px(50); mr.Right = Px(458); mr.Bottom = Px(134 + dlgExtra);
                        SelectObject(hdc, hUiFont);
                        DrawTextW(hdc, dlgMessage.Replace("\n", "\r\n"), -1, ref mr, 0x10 | 0x400 | 0x800); // DT_CALCRECT
                        DrawTextAt(hdc, dlgRedNote, hUiBold, C_RED, tl, mr.Bottom + Px(6), Px(458), Px(136 + dlgExtra), 0x10);
                    }
                    RoundRect(hdc, 0, 0, Px(480), Px(196 + dlgExtra), Px(WIN_R), 0, A_BTNBORDER);
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
        if (clips.Count >= MaxClips)
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
        if (editClip != null && editClip.HasFormat && EditText() != editClip.Text)
        {
            dlgEscResult = 2;
            dlgPrimary = 1;
            if (AskDialog("Clip com formatação", "Este clip tem formatação (negrito, cores, links...).\nAo salvar a edição, ele passa a ser colado como texto sem formatação.",
                "Salvar sem formatação", "Cancelar", null) != 1) return;
        }
        PushClipUndo();
        if (editClip != null && EditText() != editClip.Text) { editClip.Html = null; editClip.Rtf = null; }
        string text = EditText();
        if (editClip == null)
        {
            if (text.Length == 0) { SetDirty(false); return; }
            editClip = new Clip(text, true); // lista vazia: o texto digitado vira um clip novo
            clips.Add(editClip);
            mgrSel = editClip;
        }
        else { editClip.Text = text; editClip.Empty = false; } // preencher uma vaga livre
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
        dlgEscResult = 2;
        dlgPrimary = 3;
        int r = AskDialog("Edição não salva", "A edição feita neste " + what + " não foi salva. Deseja descartá-la?",
            "Descartar", "Voltar ao texto", "Salvar");
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
                convertFrom = null;
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
    class ClipSnap
    {
        public List<Clip> Refs = new List<Clip>();
        public List<string> Texts = new List<string>();
        public List<bool> Pins = new List<bool>();
        public List<byte[]> Htmls = new List<byte[]>(), Rtfs = new List<byte[]>();
        public List<bool> Empties = new List<bool>();
        public DateTime Time = DateTime.Now;
    }
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
        foreach (Clip c in clips) { s.Refs.Add(c); s.Texts.Add(c.Text); s.Pins.Add(c.Pinned); s.Htmls.Add(c.Html); s.Rtfs.Add(c.Rtf); s.Empties.Add(c.Empty); }
        return s;
    }

    static bool SameSnap(ClipSnap a, ClipSnap b)
    {
        if (a.Refs.Count != b.Refs.Count) return false;
        for (int i = 0; i < a.Refs.Count; i++)
            if (a.Refs[i] != b.Refs[i] || a.Texts[i] != b.Texts[i] || a.Pins[i] != b.Pins[i] || a.Htmls[i] != b.Htmls[i] || a.Rtfs[i] != b.Rtfs[i]) return false;
        return true;
    }

    static void InvalidateUndoButtons()
    {
        if (hwndMgr == IntPtr.Zero) return;
        foreach (int i in new int[] { IDC_UNDO, IDC_REDO, IDC_XUNDO, IDC_XREDO, IDC_CUNDO, IDC_CREDO }) InvalidateRect(GetDlgItem(hwndMgr, i), IntPtr.Zero, false);
    }

    static void PushClipUndo()
    {
        MarkCloudDirty();
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
            c.Html = s.Htmls[i];
            c.Rtf = s.Rtfs[i];
            c.Empty = s.Empties[i];
            res.Add(c);
        }
        List<Clip> later = clips.FindAll(delegate(Clip c) { return c.Copied && !c.Pinned && !s.Refs.Contains(c) && c.Added >= s.Time; });
        int at = res.FindIndex(delegate(Clip c) { return !c.Pinned; });
        res.InsertRange(at < 0 ? res.Count : at, later);
        while (res.Count > MaxClips)
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
        MarkCloudDirty();
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

    // ---- configurações (tema, cor, teclas, opções): guarda a seção [geral] antes de cada mudança
    static List<List<string>> cfgUndo = new List<List<string>>(), cfgRedo = new List<List<string>>();
    static bool cfgUndoing;
    static DateTime cfgLastPush;

    static List<string> TakeCfgSnap()
    {
        List<string> body = new List<string>();
        bool inGeral = false;
        foreach (string raw in (ReadIni() ?? DefaultIni).Replace("\r\n", "\n").Split('\n'))
        {
            string t = raw.Trim().TrimStart((char)0xFEFF);
            if (t.StartsWith("[") && t.EndsWith("]")) { inGeral = t.ToLowerInvariant() == "[geral]"; continue; }
            if (inGeral) body.Add(raw.TrimEnd('\r'));
        }
        while (body.Count > 0 && body[body.Count - 1].Trim().Length == 0) body.RemoveAt(body.Count - 1);
        return body;
    }

    static void PushCfgUndo()
    {
        MarkCloudDirty();
        if ((DateTime.Now - cfgLastPush).TotalMilliseconds < 400 && cfgUndo.Count > 0) return; // vários ajustes de uma ação só = um passo
        cfgLastPush = DateTime.Now;
        cfgUndo.Add(TakeCfgSnap());
        if (cfgUndo.Count > UNDO_MAX) cfgUndo.RemoveAt(0);
        cfgRedo.Clear();
        InvalidateUndoButtons();
    }

    static void RestoreCfgSnap(List<string> body)
    {
        cfgUndoing = true;
        try { ReplaceIniSection("geral", body); }
        finally { cfgUndoing = false; }
        LoadConfig();
        SetTip();
        RepaintAll();
    }

    // ---- campos de texto (clip e expansão): desfazer e refazer várias vezes, palavra por palavra
    class EditHist { public List<string> Texts = new List<string>(); public List<int> Sels = new List<int>(); public int Pos = -1; public DateTime Last; }
    static Dictionary<IntPtr, EditHist> editHist = new Dictionary<IntPtr, EditHist>();
    static bool editApplying;

    static void ResetEditHist(IntPtr h)
    {
        EditHist e = new EditHist();
        e.Texts.Add(CtlText(h));
        e.Sels.Add(0);
        e.Pos = 0;
        editHist[h] = e;
    }

    static void RecordEdit(IntPtr h)
    {
        if (editApplying) return;
        EditHist e;
        if (!editHist.TryGetValue(h, out e)) { ResetEditHist(h); return; }
        string t = CtlText(h);
        if (e.Pos >= 0 && e.Texts[e.Pos] == t) return;
        int sel = SendMessageW(h, 0x00B0, IntPtr.Zero, IntPtr.Zero).ToInt32() >> 16; // EM_GETSEL: fim da seleção
        string prev = e.Pos >= 0 ? e.Texts[e.Pos] : "";
        bool oneChar = t.Length == prev.Length + 1 && sel > 0 && sel <= t.Length && !char.IsWhiteSpace(t[sel - 1]) && !char.IsPunctuation(t[sel - 1]);
        if (e.Pos < e.Texts.Count - 1) { e.Texts.RemoveRange(e.Pos + 1, e.Texts.Count - e.Pos - 1); e.Sels.RemoveRange(e.Pos + 1, e.Sels.Count - e.Pos - 1); }
        if (oneChar && e.Pos > 0 && (DateTime.Now - e.Last).TotalMilliseconds < 1500) { e.Texts[e.Pos] = t; e.Sels[e.Pos] = sel; } // mesma palavra
        else
        {
            e.Texts.Add(t);
            e.Sels.Add(sel);
            e.Pos = e.Texts.Count - 1;
            if (e.Texts.Count > 200) { e.Texts.RemoveAt(0); e.Sels.RemoveAt(0); e.Pos--; }
        }
        e.Last = DateTime.Now;
    }

    static void EditUndo(IntPtr h, bool redo)
    {
        EditHist e;
        if (!editHist.TryGetValue(h, out e)) return;
        int np = e.Pos + (redo ? 1 : -1);
        if (np < 0 || np >= e.Texts.Count) return;
        e.Pos = np;
        e.Last = DateTime.MinValue; // depois de desfazer, a próxima digitação começa um passo novo
        editApplying = true;
        try
        {
            SetWindowTextW(h, e.Texts[np]);
            int sel = Math.Min(e.Sels[np], e.Texts[np].Length);
            SendMessageW(h, 0x00B1, (IntPtr)sel, (IntPtr)sel); // EM_SETSEL
            SendMessageW(h, 0x00B7, IntPtr.Zero, IntPtr.Zero); // EM_SCROLLCARET
        }
        finally { editApplying = false; }
    }

    // which: 0 = clips, 1 = atalhos por expansão, 2 ou 4 = configurações
    static void DoUndo(int which, bool redo)
    {
        if (which == 2 || which == 4 || which == 5)
        {
            List<List<string>> cf = redo ? cfgRedo : cfgUndo, ct = redo ? cfgUndo : cfgRedo;
            if (cf.Count == 0) return;
            List<string> snap = cf[cf.Count - 1];
            cf.RemoveAt(cf.Count - 1);
            ct.Add(TakeCfgSnap());
            RestoreCfgSnap(snap);
            InvalidateUndoButtons();
            return;
        }
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
            else if (c.Empty) body = "[Vaga]"; // posição livre guardada
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

    static readonly string ICO_B64 =
        "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EnqcD+p9oA//eZwP/nueEf91mQf/n7hR/87bpv/O26b/z9uo/87bp//O26f/ydee/46sM/94nA3/fZ8U/3yeE/98nhL/ep0P/3qdD/95nA/+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//eZwP/nqdDv97nhH/b5UA/2iQAP9pkAD/aJAA/2iQAP9okAD/aJAA/22UAP9ylwD/cZYA/3GWAP9zmAP/e50Q/3qdD/95nA/+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//eZwP/nueEf91mQb/m7ZL/87bpv/O26b/" +
        "z9yo/8/cqP/P3Kj/z9yo/87bpv/N2qX/zNqj/83apf+jvFr/dZkG/3ueEf95nA/+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//ep0Q/nmcDf9/oRj/+Pnx////////////////////////////////////////////////////////////iakr/3abCP97nRH+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//epwP/nqdD/95nA7/3OW////////////////////////////////////////////////////////o7tb/gKEa/3icDP96nRD+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//eZwP/nudEP93mwv/f6EY/5m0R/+atEj/mrVJ/5q1Sf+atUn/mrVJ/5q1Sf+atUn/mrRI/5q1Sf+DpB//d5sJ/3ueEP95nA/+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//eZwP/nqdD/96nQ7/dZkG/3ueEv98nhL/fJ4S/3yeEv98nhL/fJ4S/3yeEv98nhL/fJ4S/3yeEv91mQb/eZwN/3qdD/95nA/+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//eZwP/nudEP93mwn/xtWY//X47f/1+O3/9vjv//b47//2+O//9vjv//b47//2+O//9fjt//b47//T3q//e54Q/3qdDv96nA/+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//ep0Q/nmcDP9/oRn//f78////////////////////////////////////////////////////////////i6kt/3aaB/97nRH+faAP/3qcD+p/mw4SeJYPEXqcD+p9oA//eZwP/nueEf91mQf/qsFm/9vkvv/b5b7/3OXA/9zlwP/c5cD/3OXA/9zlwP/c5cD/2+W+/9zlwP+1yHn/d5sJ/3udEP95nA/+faAP/3qcD+p4lg8R//8AAXqdDs5/oxD+eZ0P/XqdD/96nRD/c5gD/3SZBP90mQX/dJkF/3SZBf90mQX/dJkF/3SZBf90mQX/dJkF/3SZBf9zmAL/ep0P/3qdD/95nQ/9f6MQ/nqdDs7//wABAAAAAHqdDnuFqxD/eZwP+XqdD/96nQ//fJ4S/3ueEf97nhH/e54R/3ueEf97nhH/e54R/3ueEf97nhH/e54R/3ueEf98nhL/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I/+FpSP/haUj/4WlI/+FpSP/haUj/4WlI/+EpSL/g6Qf/4OkH/+DpB//g6Qf/4GiG/90mQT/ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/97nhL/dJgF/6zCav/x9OX/9fjs//b47v/2+O//9vjv//b47//2+O//9vjv//b47//2+O//9vjv//b47//1+O3/8/bp/7nLgf91mQb/e54R/3qdD/96nQ//eZwP/n2gD/96nA/qf5sOEn+bDhJ6nA/qfaAP" +
        "/3mcD/56nQ//e50Q/3mcDP+AoRn/+Prz///////+//7//////////////////////////////////////////////////////////////////////4uqLf92mgf/e54R/3qdD/95nA/+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//eZwP/nqdD/96nQ//ep0P/3qdD//m7NH////////////////////////////////////////////////////////////////////////////z9un/gaIc/3icDP97nRD/ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/97nhH/dpoI/4qpLf/G1Zn/zdql/83bpf/O26b/ztum/87bpv/O26b/ztum/87bpv/O26b/ztum/83bpv/N2qX/ydee/5KvO/91mQb/e54R/3qdD/96nQ//eZwP/n2gD/96nA/qf5sOEn+bDhJ6nA/qfaAP/3mcD/56nQ//ep0P/3qdD/97nhD/eJsL/2+VAP9vlQD/b5UA/2+VAP9vlQD/b5UA/2+VAP9vlQD/b5UA/2+VAP9vlQD/b5UA/2+VAP9vlQD/d5sJ/3ueEf96nQ//ep0P/3qdD/95nA/+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//eZwP/nqdD/96nQ//ep0P/3qdEP91mQb/gaIc/4WlI/+FpSL/haUj/4WlI/+FpSP/haUj/4WlI/+FpSP/haUj/4WlI/+FpSP/haUj/4OjH/91mgb/ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/97nhL/dJgF/6zCav/x9OX/9fjs//b47v/2+O//9vjv//b47//2+O//9vjv//b47//2+O//9vjv//b47//1+O3/8/bp/7nLgf91mQb/e54R/3qdD/96nQ//eZwP/n2gD/96nA/qf5sOEn+bDhJ6nA/qfaAP/3mcD/56nQ//e50Q/3mcDP+AoRn/+Prz///////+//7//////////////////////////////////////////////////////////////////////4uqLf92mgf/e54R/3qdD/95nA/+faAP/3qcD+p/mw4Sf5sOEnqcD+p9oA//eZwP/nqdD/96nQ//ep0P/3qdD//m7NH////////////////////////////////////////////////////////////////////////////z9un/gaIc/3icDP97nRD/ep0P/3mcD/59oA//epwP6n+bDhJ4kw0TeZwP632gD/95nA/+ep0P/3qdD/97nhH/dpoI/4qpLf/G1Zj/zNqk/83apP/N2qX/zdql/83apf/N2qX/zdql/83apf/N2qX/zdql/83apf/M2qP/yded/5KvO/91mQb/e54R/3qdD/96nQ//" +
        "eZwP/n2gD/95nA/reJMNE2aZAAV5nQ/afqIQ/3mdD/16nQ//ep0P/3qdD/97nhD/d5oJ/3GWAP9xlwD/cZYA/3GWAP9xlgD/cZYA/3GWAP9xlgD/cZYA/3GWAP9xlgD/cZYA/3GXAP9xlgD/dZoH/3ueEf96nQ//ep0P/3qdD/95nQ/9fqIQ/3mdD9pmmQAFAAAAAHqcDquCpxD+eZ0P+3qdD/96nQ//ep0P/3qdD/97nhH/fJ8T/3yeE/98nhP/fJ4T/3yeE/98nhP/fJ4T/3yeE/98nhP/fJ4T/3yeE/98nhP/fJ4T/3yfE/97nhH/ep0P/3qdD/96nQ//ep0P/3mdD/uCpxD+epwOqwAAAAAAAAAAepwNS4OoEP95nA/7ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//eZwP+4OnEP94mw1KAAAAAH9/AAJ/fwACepwPuoOpEP95nA/5ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/mEqRD/eZsPuAAAAAB/fwACf38AAv//AAF/ogsWfZ8P1IOpEP95nA/7eZwP/HmdD/15nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZ0P/XmcD/x5nA/7hKoQ/32fD9R/ogsW//8AAX9/AAIAAAAAf38AAgAAAABukAsXfaEPt4OoEP+CpxD/fqIQ/32gD/99oA//faAP/32gD/99oA//faAP/32gD/99oA//faAP/32gD/99oA//faAP/32gD/99oA//faAP/32gD/9+ohD/gqcQ/4KnEP99oQ+xbpALFwAAAAB/fwACAAAAAAAAAAAAAAAAf38AAgAAAAAAAAAAf6ENTHygD6h7ng/Xep0P6XqdD+l6nA/qepwP6nqcD+p6nA/qepwP6nqcD+p6nA/qepwP6nqcD+p6nA/qepwP6nqcD+p6nQ/pep0P6XueD9d8oQ+kfKENTAAAAAAAAAAA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D/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/97nhH/ep0P/3aaB/91mgf/dZoH/3WaBv91mgb/dZoG/3WaBv91mgb/dZoG/3WaB/91mgb/d5sK/3yeEv96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/yApRD/ep0OxgAAAACLohcLep0O436hD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3ueEf92mgj/eZwM" +
        "/42rMf+OrDT/jqw0/46sNP+OrDT/jqw0/46sNP+OrDT/jqw0/46sM/+PrDT/haUi/3KXAf96nRD/ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/5+oQ//ep0O44uiFwuGoQ0TeZ0P632gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//e54R/3aaCP+KqSz/4OjH//39+//+/v3////+//////////////////////////////////7//v/+/v3/9vju/7jLf/91mQb/e54R/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//eZ0P64ahDRN/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//e54R/3WZB//c5cD///////7+/f////////////////////////////////////////////////////////////////+btUv/dJgE/3yeEv96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/97nRD/eJsK/4SlIP/5+vT//f37//7//v////7////+/////v////7////+/////v////7////+/////v////7//Pz5//39+//A0Y3/b5UA/3yfE/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//e50Q/3ebCv/k683///////z9+v/+/v3//v79//7+/f/+/v3//v79//7+/f/+/v3//v79//7+/f/+/v3//v79//////+iu1j/c5gC/3yeEv96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//e54S/3WZBv+TsD3/7fHe/////////////////////////////////////////////////////////////v79/8fWmf92mgj/e50Q/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//" +
        "ep0P/3qdD/96nQ//ep0P/3ueEf91mQX/gKEZ/5q1Sf+ctkz/nLZN/5y2Tf+ctk3/nLZN/5y2Tf+ctk3/nLZN/5y2TP+ctk3/kK03/3SYBP96nQ7/ep0Q/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/97nhH/eZwN/3SYBP90mAP/dJgD/3OYA/9zmAP/c5gD/3OYA/9zmAP/c5gD/3SYA/90mAP/dpoH/3yeEv97nhH/e50Q/3udEP97nRD/e50Q/3udEP97nRD/ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/97nhD/fJ4S/3mdDv95nA7/eZwO/3mcDv95nA7/eZwO/3mcDv95nA7/eZwO/3mcDv95nA7/eZwN/3ibC/94mwv/eJsL/3ibC/94mwv/eJsL/3ibC/94mwv/e54Q/3ueEf96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3ueEP93mwr/dJkE/4KjHf+DpB//g6Qf/4OkH/+DpB//g6Qf/4OkH/+DpB//g6Qf/4OkH/+DpB//g6Qf/4OkH/+DpB//g6Qf/4OkH/+DpB//g6Qf/4OkH/+Dox7/dpoI/3aaCP97nhH/ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//e50Q/3ebCv+Cox//0t6t//T36//1+O7/9vjv//b47//2+O//9vjv//b47//2+O//9vjv//b47//2+O//9vjv//b47//2+O//9vjv//b47//2+O//9vjv//X47v/19+z/2eO7/4qpLP92mgj/e54R/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//e54R/3OYBP/U37D//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////+PqzP93mwv/ep0Q/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/" +
        "mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/97nRD/eJsL/4OkH//4+vP//f37//3+/f/+//7//v/+//7//v/+//7//v/+//7//v/+//7//v/+//7//v/+//7//v/+//7//v/+//7//v/+//7//v/+//7//v/+//7//v/+//7//f77//////+PrTX/dZoG/3ueEf96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mdDv/q79n///////z9+f/9/vz//f77//3++//9/vv//f77//3++//9/vv//f77//3++//9/vv//f77//3++//9/vv//f77//3++//9/vv//f77//3+/P/8/fr///////f57/+Boxz/eJwL/3udEP96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//fJ4S/3OYBP+dt0//9/nw////////////////////////////////////////////////////////////////////////////////////////////////////////////+/z4/6vBZ/9zlwL/fJ4S/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3ueEf90mQT/iagq/6rAZf+rwWj/rMJp/6zCaf+swmn/rMJp/6zCaf+swmn/rMJp/6zCaf+swmn/rMJp/6zCaf+swmn/rMJp/6zCaf+swmn/rMJp/6vCaP+rwWf/j6w0/3SYBP97nhH/ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/97nhH/d5sJ/3GXAP9xlwD/cZcA/3GXAP9xlwD/cZcA/3GXAP9xlwD/cZcA/3GXAP9xlwD/cZcA/3GXAP9xlwD/cZcA/3GXAP9xlwD/cZcA/3GXAP9xlwD/dpoH/3ueEv96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nRD/fZ8U/3yeEv98nhL/fJ4S/3yeEv98nhL/fJ4S/3yeEv98nhL/fJ4S/3yeEv98nhL/fJ4S/3yeEv98nhL/fJ4S/3yeEv98nhL/fJ4S/3yeEv98nhL/fZ8U/3ueEP96nQ//ep0P/3qdD/96nQ//ep0P/3qd" +
        "D/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3udEP95nA3/cpcB/3mdDv96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//c5gC/3ibC/97nhD/ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0Q/3mcDP98nxT/wtKR/+ju1v/q79n/6vDa/+vw2v/r8Nr/6/Da/+vw2v/r8Nr/6/Da/+vw2v/r8Nr/6/Da/+vw2v/r8Nr/6/Da/+vw2v/r8Nr/6vDa/+rw2f/p79f/ytif/4KjHv93mwr/e50Q/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//fJ4S/3KXAv/K2J///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////9rju/91mgf/e54R/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/97nRD/eJsL/4KjHf/3+fH//f37//3+/P/+/v3//v79//7+/f/+/v3//v79//7+/f/+/v3//v79//7+/f/+/v3//v79//7+/f/+/v3//v79//7+/f/+/v3//v79//7+/f/+/v3//f77//////+NqzL/dZoG/3ueEf96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//epwO/3yeEv/v8+L///////v9+f/9/fv//f37//39+//9/fv//f37//39+//9/fv//f37//39+//9/fv//f37//39+//9/fv//f37//39+//9/fv//f37//39+//8/fr////+//r79v+FpSP/d5sK/3udEP96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhJ/mw4SepwP6n2gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//fJ4S/3KXAv+ov2P////+////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
        "/7fKff9ylwL/fJ4S/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//epwP6n+bDhKGoQ0Tep0P632gD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3ueEP90mQT/lbFA/7rMg/+8zob/vM6H/7zOh/+8zof/vM6H/7zOh/+8zof/vM6H/7zOh/+8zof/vM6H/7zOh/+8zof/vM6H/7zOh/+8zof/vM6H/7zOhv+7zYX/nLZM/3WZBv96nRD/ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/59oA//ep0P64ahDROLohcLeZ0O436hD/95nA/+ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/97nhH/dZkF/3CWAP9wlgD/cJYA/3CWAP9wlgD/cJYA/3CWAP9wlgD/cJYA/3CWAP9wlgD/cJYA/3CWAP9wlgD/cJYA/3CWAP9wlgD/cJYA/3CWAP9wlgD/dJgD/3ueEf96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mcD/5+oQ//eZ0O44uiFwsAAAAAeZ0Ox4CkEP96nQ/8ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//e54R/3yfE/98nxP/fJ8T/3yfE/98nxP/fJ8T/3yfE/98nxP/fJ8T/3yfE/98nxP/fJ8T/3yfE/98nxP/fJ8T/3yfE/98nxP/fJ8T/3yfE/98nxP/e54S/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/yApBD/eZ0OxwAAAAAAAAAAepwPloOoEP55nQ/7ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3mdD/uDqBD+epwPlgAAAAAAAAAAeZwPQYClEP94nA/9ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3icD/2ApRD/eZwPQQAAAAB/fwACf38AAnibDs9/pBD+epwP/HqdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//" +
        "ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//eZwP/ICkEP94mw7Pf38AAn9/AAKqqgADAAAAAHudD0SBphD/eZ0P+3qdD/56nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ/+ep0P+4ClEP97ng9CAAAAAKqqAAMAAAAAqqoAAwAAAAB5mw6PhawQ/3mcD/l6nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/95nA/5hasQ/3maDosAAAAAVaoAAwAAAAAAAAAAAAAAAaqqAAN/fwACep0Pp4WrEP95nA/8epwP/HqdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//ep0P/3qdD/96nQ//eZwP/HqcD/yFqxD/ep0PpwD/AAGqqgADAAAAAQAAAAAAAAAAAAAAAAD/AAH//wABAAAAAXueDo6ApRD/f6QQ/3mcD/15nQ/7eZwP/XmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+eZwP/nmcD/55nA/+epwP/HmcD/t5nA/9f6QQ/4CkEP96nQ6LAAAAAX9/AAIA/wABAAAAAAAAAAAAAAAAAAAAAAAAAAAA/wAB//8AAQAAAAB4mw1KfqEPyIClEP+DqBD/gKQQ/32hD/99oA//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";

    static string backupDir = "";
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern void SHChangeNotify(int ev, uint flags, string item1, IntPtr item2);

    // Dá à pasta o ícone do ClipAtalho (desktop.ini + ícone escondido dentro dela).
    static void SetFolderIcon(string dir)
    {
        try
        {
            string ico = Path.Combine(dir, "ClipAtalho.ico"), ini = Path.Combine(dir, "desktop.ini");
            foreach (string f in new string[] { ico, ini }) if (File.Exists(f)) File.SetAttributes(f, FileAttributes.Normal);
            File.WriteAllBytes(ico, Convert.FromBase64String(ICO_B64));
            File.WriteAllText(ini, "[.ShellClassInfo]\r\nIconResource=ClipAtalho.ico,0\r\nIconFile=ClipAtalho.ico\r\nIconIndex=0\r\nInfoTip=Backups do ClipAtalho\r\n", Encoding.Unicode);
            File.SetAttributes(ico, FileAttributes.Hidden | FileAttributes.System);
            File.SetAttributes(ini, FileAttributes.Hidden | FileAttributes.System);
            File.SetAttributes(dir, File.GetAttributes(dir) | FileAttributes.ReadOnly); // o Windows só lê o desktop.ini assim
            SHChangeNotify(0x00001000, 0x0005, dir, IntPtr.Zero); // SHCNE_UPDATEDIR
        }
        catch (Exception e) { Log("Ícone da pasta de backups: " + e.Message); }
    }

    // Onde salvar o backup: a pasta "Backups do ClipAtalho" (criada na 1ª vez), ou outra que a pessoa escolher.
    static string BackupFolder()
    {
        if (backupDir.Length > 0 && Directory.Exists(backupDir))
        {
            dlgEscResult = 3;
            dlgPrimary = 1;
            int r = AskDialog("Backup", "Salvar o backup em:\n" + backupDir, "Salvar aqui", "Escolher outra pasta", "Cancelar");
            if (r == 1) return backupDir;
            if (r != 2) return null;
        }
        string p = PickFolder("Escolha onde criar a pasta \"Backups do ClipAtalho\" (os próximos backups vão para ela)");
        if (p == null) return null;
        string dir = Path.GetFileName(p.TrimEnd('\\')).Equals("Backups do ClipAtalho", StringComparison.OrdinalIgnoreCase) ? p : Path.Combine(p, "Backups do ClipAtalho");
        try { Directory.CreateDirectory(dir); }
        catch (Exception e) { Info("Não foi possível criar a pasta de backups:\n" + e.Message); return null; }
        SetFolderIcon(dir);
        backupDir = dir;
        SaveLocal();
        return dir;
    }

    static bool DoBackup(bool doClips, bool doX, bool doCfg)
    {
        if (!ResolveDirty(false)) return false;
        string dir = BackupFolder();
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
        dlgEscResult = 3;
        dlgPrimary = 2;
        int r = AskDialog("Restaurar backup", "Como encontrar o backup?\n• Escolher arquivo: você aponta o(s) arquivo(s).\n• Procurar backups: o ClipAtalho procura numa pasta ou num disco inteiro e mostra os que encontrar.",
            "Escolher arquivo", "Procurar backups", "Cancelar");
        if (r == 1) RestoreFiles(PickBackupFiles());
        else if (r == 2) ScanBackups();
    }

    static void RestoreFiles(List<string> files)
    {
        files.Sort(delegate(string x, string y) { return RestoreRank(x).CompareTo(RestoreRank(y)); });
        foreach (string f in files)
        {
            string n = Path.GetFileName(f).ToLowerInvariant();
            if (n.StartsWith("backup automático") || n.StartsWith("backup automatico")) RestoreFullIni(f);
            else if (n.EndsWith(".ini")) RestoreConfig(f);
            else RestoreList(f);
        }
    }

    static readonly Regex BackupName = new Regex(@"^(ClipAtalho .*? Backup (da Lista de (Clipes|Expansões)|de Configurações) (\d{2}-\d{2}-\d{4})( \(\d+\))?\.(txt|ini)|Backup automático - .+\.ini)$", RegexOptions.IgnoreCase);

    // Procura backups numa pasta ou disco (sem travar a janela) e mostra os encontrados, do mais novo ao mais antigo.
    static void ScanBackups()
    {
        string root = PickFolder("Onde procurar os backups? Pode ser uma pasta ou um disco inteiro (ex.: C:\\ ou um pendrive).");
        if (root == null) return;
        List<string> found = new List<string>();
        string err = RunWorker(delegate
        {
            Stack<KeyValuePair<string, int>> st = new Stack<KeyValuePair<string, int>>();
            st.Push(new KeyValuePair<string, int>(root, 0));
            while (st.Count > 0 && found.Count < 500)
            {
                KeyValuePair<string, int> cur = st.Pop();
                try
                {
                    foreach (string f in Directory.GetFiles(cur.Key)) if (BackupName.IsMatch(Path.GetFileName(f))) found.Add(f);
                    if (cur.Value >= 10) continue;
                    foreach (string d in Directory.GetDirectories(cur.Key))
                    {
                        string dn = Path.GetFileName(d);
                        if (dn.StartsWith("$") || dn == "Windows" || dn == "System Volume Information" || dn == "WinSxS") continue;
                        if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0) continue;
                        st.Push(new KeyValuePair<string, int>(d, cur.Value + 1));
                    }
                }
                catch { } // pasta sem permissão: pula
            }
            return null;
        }, "Procurando backups em " + root + "…");
        if (err != null) { Info("Não foi possível procurar: " + err); return; }
        // agrupa os arquivos de um mesmo backup (mesma pasta e mesma data): o "completo" restaura tudo de uma vez
        Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();
        foreach (string f in found)
        {
            Match m = BackupName.Match(Path.GetFileName(f));
            string key = Path.GetDirectoryName(f) + "|" + (m.Groups[4].Success ? m.Groups[4].Value + m.Groups[5].Value : Path.GetFileName(f));
            if (!groups.ContainsKey(key)) groups[key] = new List<string>();
            groups[key].Add(f);
        }
        List<List<string>> sets = new List<List<string>>(groups.Values);
        sets.Sort(delegate(List<string> a, List<string> b) { return NewestTime(b).CompareTo(NewestTime(a)); });
        if (sets.Count == 0) { Info("Nenhum backup do ClipAtalho encontrado em:\n" + root); return; }
        List<MI> mi = new List<MI>();
        for (int i = 0; i < sets.Count && i < 30; i++) mi.Add(Item(1 + i, SetLabel(sets[i])));
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_RESTOREBK), out r);
        int cmd = ShowMenu(mi, r.Left - Px(300), r.Bottom + Px(4), hwndMgr);
        if (cmd < 1) return;
        RestoreFiles(new List<string>(sets[cmd - 1]));
    }

    static DateTime NewestTime(List<string> set)
    {
        DateTime t = DateTime.MinValue;
        foreach (string f in set) { try { DateTime w = File.GetLastWriteTime(f); if (w > t) t = w; } catch { } }
        return t;
    }

    static string SetLabel(List<string> set)
    {
        string kind;
        string n0 = Path.GetFileName(set[0]).ToLowerInvariant();
        if (n0.StartsWith("backup automático")) kind = "Automático (" + Path.GetFileNameWithoutExtension(set[0]).Substring(19) + ")";
        else if (set.Count >= 3) kind = "Completo";
        else
        {
            List<string> parts = new List<string>();
            foreach (string f in set)
            {
                string n = Path.GetFileName(f).ToLowerInvariant();
                parts.Add(n.Contains("clipes") ? "clipes" : n.Contains("expans") ? "expansões" : "configurações");
            }
            kind = string.Join(" + ", parts.ToArray());
            kind = char.ToUpper(kind[0]) + kind.Substring(1);
        }
        string dir = Path.GetDirectoryName(set[0]);
        if (dir.Length > 38) dir = "…" + dir.Substring(dir.Length - 37);
        return NewestTime(set).ToString("dd/MM/yyyy HH:mm") + "  ·  " + kind + "  ·  " + dir;
    }

    // Backup automático: um por dia da semana, guardando os últimos 7 dias, na pasta do programa.
    static DateTime autoBackupDay = DateTime.MinValue;
    static string AutoBackupDir { get { return Path.Combine(exeDir, "Backups automáticos"); } }

    static void AutoDailyBackup()
    {
        if (autoBackupDay == DateTime.Today) return;
        autoBackupDay = DateTime.Today;
        try
        {
            string text = ReadIni();
            if (text == null || text.Length == 0) return;
            Directory.CreateDirectory(AutoBackupDir);
            string f = Path.Combine(AutoBackupDir, "Backup automático - " + WeekNames[(int)DateTime.Today.DayOfWeek] + ".ini");
            if (File.Exists(f) && File.GetLastWriteTime(f).Date == DateTime.Today) return; // o de hoje já foi feito
            File.WriteAllText(f, text, new UTF8Encoding(true));
            Log("Backup automático: " + Path.GetFileName(f));
        }
        catch (Exception e) { Log("Backup automático falhou: " + e.Message); }
    }

    static void RestoreFullIni(string file)
    {
        string text = ReadTextFile(file);
        if (text == null) { Info("Não foi possível ler o arquivo."); return; }
        dlgEscResult = 2;
        dlgPrimary = 1;
        if (AskDialog("Restaurar backup automático", "Restaurar o backup automático de " + File.GetLastWriteTime(file).ToString("dd/MM/yyyy") +
            " (" + Path.GetFileNameWithoutExtension(file).Substring(19) + ")?\nSubstitui clips, atalhos e configurações deste computador (uma cópia dos atuais fica guardada).",
            "Restaurar", "Cancelar", null) != 1) return;
        CopyIniAside("antes de restaurar");
        WriteIni(text.TrimStart((char)0xFEFF));
        ReloadAll();
        Info("Backup automático restaurado.");
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
            if (body.Trim() == "[Vaga]") { Clip vg = new Clip("", true); vg.Empty = true; got.Add(vg); continue; }
            if (body.Length > 0) got.Add(new Clip(body.Replace("\n", "\r\n"), pin));
        }
        List<Clip> res = replace ? new List<Clip>() : new List<Clip>(clips);
        int skipped = 0;
        foreach (Clip c in got)
        {
            if (!c.Empty && res.Exists(delegate(Clip o) { return !o.Empty && Same(o, c); })) { skipped++; continue; }
            if (res.Count >= MaxClips)
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
        Info((got.Count - skipped) + " clip(s) restaurado(s)." + (skipped > 0 ? "\n" + skipped + " não entraram (repetidos ou lista cheia: máximo " + MaxClips + ")." : ""));
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
        MI none = Item(1, "Nunca"); none.Check = autoMode == 0; mi.Add(none);
        for (int i = 0; i < AutoDelNames.Length; i++) { MI am = Item(10 + i, AutoDelNames[i]); am.Check = autoMode == 1 && cfgAutoDel == AutoDelMinutes[i]; mi.Add(am); }
        mi.Add(Sep());
        MI d = Item(2, "Zerar todo dia em um horário…"); d.Check = autoMode == 2; mi.Add(d);
        MI w = Item(3, "Zerar toda semana…"); w.Check = autoMode == 3; mi.Add(w);
        MI e = Item(4, "Zerar a cada X horas…"); e.Check = autoMode == 4; mi.Add(e);
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_AUTODEL), out r);
        int cmd = ShowMenu(mi, r.Left, r.Bottom + Px(4), hwndMgr);
        if (cmd < 1) return;
        string val = null, hm = autoHour.ToString("00") + ":" + autoMinute.ToString("00");
        int h, m;
        if (cmd == 1) val = "nunca";
        else if (cmd >= 10) val = AutoDelKeys[cmd - 10];
        else if (cmd == 2)
        {
            string t = InputDialog("Zerar todo dia", "Horário em que a lista é zerada (HH:MM):", autoMode >= 2 ? hm : "18:00");
            if (t == null) return;
            if (!ParseHM(t, out h, out m)) { Info("Horário inválido. Use o formato HH:MM, por exemplo 18:00."); return; }
            val = "diario " + h.ToString("00") + ":" + m.ToString("00");
        }
        else if (cmd == 3)
        {
            List<MI> days = new List<MI>();
            for (int i = 0; i < 7; i++) { MI x = Item(1 + i, "Toda " + WeekNames[i]); x.Check = autoMode == 3 && autoWeekDay == i; days.Add(x); }
            int dsel = ShowMenu(days, r.Left, r.Bottom + Px(4), hwndMgr);
            if (dsel < 1) return;
            string t = InputDialog("Zerar toda " + WeekNames[dsel - 1], "Horário em que a lista é zerada (HH:MM):", autoMode >= 2 ? hm : "18:00");
            if (t == null) return;
            if (!ParseHM(t, out h, out m)) { Info("Horário inválido. Use o formato HH:MM, por exemplo 08:00."); return; }
            val = "semanal " + (dsel - 1) + " " + h.ToString("00") + ":" + m.ToString("00");
        }
        else if (cmd == 4)
        {
            string t = InputDialog("Zerar a cada X horas", "De quantas em quantas horas a lista é zerada (1 a 720)?", autoMode == 4 ? autoEvery.ToString() : "24");
            if (t == null) return;
            int n;
            if (!int.TryParse(t.Trim(), out n) || n < 1 || n > 720) { Info("Digite um número de horas entre 1 e 720."); return; }
            val = "cada " + n;
        }
        if (val == null) return;
        SetIniValue("geral", "autoexclusao", val);
        if (cmd >= 2 && cmd <= 4) MarkCleaned(DateTime.Now); // conta a partir de agora
        LoadConfig();
        RunAutoDelete();
        RepaintAll();
    }

    static void DateFmtMenu()
    {
        List<MI> mi = new List<MI>();
        int keep = cfgDateFmt;
        for (int i = 0; i < 3; i++) { cfgDateFmt = i; MI m = Item(1 + i, DateStr(DateTime.Now)); m.Check = i == keep; mi.Add(m); }
        cfgDateFmt = keep;
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_DATEFMT), out r);
        int cmd = ShowMenu(mi, r.Left, r.Bottom + Px(4), hwndMgr);
        if (cmd < 1) return;
        SetIniValue("geral", "data_formato", DateFmtKeys[cmd - 1]);
        LoadConfig();
        RepaintAll();
    }

    static void YearFmtMenu()
    {
        List<MI> mi = new List<MI>();
        MI a = Item(1, "Ano com 4 dígitos (" + DateTime.Now.ToString("yyyy") + ")"); a.Check = !cfgYear2; mi.Add(a);
        MI b = Item(2, "Ano com 2 dígitos (" + DateTime.Now.ToString("yy") + ")"); b.Check = cfgYear2; mi.Add(b);
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_YEARFMT), out r);
        int cmd = ShowMenu(mi, r.Left, r.Bottom + Px(4), hwndMgr);
        if (cmd < 1) return;
        SetIniValue("geral", "ano", cmd == 2 ? "2" : "4");
        LoadConfig();
        RepaintAll();
    }

    static void HourFmtMenu()
    {
        DateTime n = DateTime.Now;
        List<MI> mi = new List<MI>();
        MI a = Item(1, "24h  (" + n.ToString("HH:mm") + "h)"); a.Check = !cfgAmPm; mi.Add(a);
        MI b = Item(2, "AM/PM  (" + n.ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture) + ")"); b.Check = cfgAmPm; mi.Add(b);
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_HOURFMT), out r);
        int cmd = ShowMenu(mi, r.Left, r.Bottom + Px(4), hwndMgr);
        if (cmd < 1) return;
        SetIniValue("geral", "hora_formato", cmd == 2 ? "ampm" : "24h");
        LoadConfig();
        RepaintAll();
    }

    static void LimitMenu()
    {
        List<MI> mi = new List<MI>();
        for (int i = 0; i < LimitValues.Length; i++)
        {
            MI m = Item(1 + i, LimitValues[i] == 0 ? "Sem limite" : LimitValues[i] + " itens");
            m.Check = cfgLimit == LimitValues[i];
            mi.Add(m);
        }
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_LIMIT), out r);
        int cmd = ShowMenu(mi, r.Left, r.Bottom + Px(4), hwndMgr);
        if (cmd < 1) return;
        int v = LimitValues[cmd - 1];
        if (v > 0 && clips.Count > v)
        {
            int pinned = clips.FindAll(delegate(Clip c) { return c.Pinned; }).Count;
            dlgEscResult = 2;
            dlgPrimary = 1;
            if (AskDialog("Tamanho da lista", "A lista tem " + clips.Count + " itens. Ao reduzir para " + v + ", os não fixados mais antigos saem" +
                (pinned > v ? " (os " + pinned + " fixados ficam todos)." : ".") + "\nDá para desfazer com Ctrl+Z.", "Reduzir", "Cancelar", null) != 1) return;
            PushClipUndo();
            while (clips.Count > v)
            {
                int last = clips.FindLastIndex(delegate(Clip c) { return !c.Pinned; });
                if (last < 0) break;
                clips.RemoveAt(last);
            }
            SaveClips();
            RefreshList();
        }
        SetIniValue("geral", "limite", v.ToString());
        LoadConfig();
        RepaintAll();
    }

    // ------------------------------------------------------------------ pasta de dados
    static Clip convertFrom; // clip que está virando expansão

    static void ConvertToExpansion(Clip c)
    {
        if (c == null || c.IsImage || c.Empty || c.Text.Length == 0) { Info("Só clips de texto podem virar expansão."); return; }
        HidePopup();
        if (hwndMgr == IntPtr.Zero || !IsWindowVisible(hwndMgr)) OpenManager(1);
        else NavTo(1);
        if (page != 1 || !ResolveDirty(false)) return;
        if (xFilter.Length > 0) { SetWindowTextW(hXSearch, ""); xFilter = ""; }
        xSel = null;
        xNew = true;
        RefreshX(true);
        mgrLoading = true;
        SetWindowTextW(hXText, c.Text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        mgrLoading = false;
        ResetEditHist(hXText);
        SetXDirty(true);
        convertFrom = c;
        SetFocus(hXAbbr);
        RepaintAll();
    }

    // Depois de salvar o atalho convertido: tirar o clip da lista ou manter os dois?
    static void AfterConvertSaved()
    {
        Clip src = convertFrom;
        convertFrom = null;
        if (src == null || !clips.Contains(src)) return;
        dlgEscResult = 2;
        dlgPrimary = 2;
        dlgRedNote = src.Pinned ? "Atenção: este clip está FIXADO. \"Remover Duplicata\" também o tira da sua lista de clips fixados." : "";
        int r = AskDialog("Atalho criado", "O texto agora também está em Atalhos por Expansão.\nRemover o clip da lista de clips, para ficar só em Expansões, ou manter os dois?",
            "Remover Duplicata", "Manter Duplicata", null);
        dlgRedNote = "";
        if (r != 1) return;
        if (src.Pinned) DeleteClipAsk(src);
        else
        {
            PushClipUndo();
            clips.Remove(src);
            if (mgrSel == src) mgrSel = null;
            SaveClips();
            RefreshList();
        }
    }

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

    // ------------------------------------------------------------------ nuvem (OneDrive pela internet ou uma pasta sincronizada)
    // ID do ClipAtalho no portal da Microsoft (registro do aplicativo). Vazio = conexão com o OneDrive ainda não liberada.
    const string ONEDRIVE_CLIENT_ID = "83295991-f0f7-4371-a9a6-c062010e68d6";
    const string OD_SCOPE = "Files.ReadWrite.AppFolder offline_access";
    const string OD_AUTH = "https://login.microsoftonline.com/consumers/oauth2/v2.0/";
    static string cloudClientOverride = "";
    static string ClientId { get { return cloudClientOverride.Length > 0 ? cloudClientOverride : ONEDRIVE_CLIENT_ID; } }

    static int cloudProv, cloudMode, cloudAuthState;   // prov: 0 nenhum, 1 OneDrive, 2 pasta
    static string cloudDir = "", cloudRefresh, cloudLastSync = "", cloudBusyText = "", cloudAccess;
    static DateTime cloudAccessExp, cloudLastCheck = DateTime.MinValue;
    static bool cloudCheck = true, cloudBusy, cloudDirty, cloudQuiet, cloudApplying, cloudAuthWaiting;
    static volatile bool cloudAuthCancel;
    static readonly object tokenLock = new object();
    static readonly string[] CloudModeKeys = { "perguntar", "auto", "nada" };
    static readonly string[] CloudModeNames = { "perguntar se deseja enviar", "enviar sozinho", "não fazer nada" };

    class CloudInfo { public DateTime When; public string Machine = "", Stamp = ""; public int Clips, Abbr; }
    static CloudInfo cloudInfo, cloudCheckResult;

    static bool CloudReady()
    {
        return (cloudProv == 1 && cloudRefresh != null) || (cloudProv == 2 && cloudDir.Length > 0);
    }

    static string StampText(string stamp)
    {
        string[] p = stamp.Split('|');
        DateTime d;
        if (!DateTime.TryParseExact(p[0], "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out d)) return stamp;
        return d.ToString("dd/MM/yyyy") + " às " + d.ToString("HH:mm") + (p.Length > 1 ? " (" + p[1] + ")" : "");
    }

    static string InfoText(CloudInfo i)
    {
        return "enviado em " + i.When.ToString("dd/MM/yyyy") + " às " + i.When.ToString("HH:mm") + " pelo computador " + i.Machine +
            " (" + i.Clips + " clips, " + i.Abbr + " atalhos)";
    }

    static CloudInfo ParseInfo(string text)
    {
        if (text == null) return null;
        CloudInfo i = new CloudInfo();
        string data = null;
        foreach (string raw in text.Replace("\r", "").TrimStart((char)0xFEFF).Split('\n'))
        {
            int eq = raw.IndexOf('=');
            if (eq < 1 || raw.StartsWith(";")) continue;
            string k = raw.Substring(0, eq).Trim(), v = raw.Substring(eq + 1).Trim();
            if (k == "data") data = v;
            else if (k == "computador") i.Machine = v;
            else if (k == "clips") int.TryParse(v, out i.Clips);
            else if (k == "atalhos") int.TryParse(v, out i.Abbr);
        }
        if (data == null || !DateTime.TryParseExact(data, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out i.When)) return null;
        i.Stamp = data + "|" + i.Machine;
        return i;
    }

    // ---- DPAPI: o acesso ao OneDrive fica criptografado pelo Windows, válido só para este usuário neste computador
    [StructLayout(LayoutKind.Sequential)] struct DATA_BLOB { public int cbData; public IntPtr pbData; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref DATA_BLOB data, string desc, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DATA_BLOB output);
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref DATA_BLOB data, IntPtr desc, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DATA_BLOB output);
    [DllImport(K)] static extern IntPtr LocalFree(IntPtr p);
    [DllImport(U)] static extern bool IsWindowEnabled(IntPtr hWnd);

    static byte[] Dpapi(byte[] input, bool protect)
    {
        DATA_BLOB a = new DATA_BLOB(), b;
        GCHandle h = GCHandle.Alloc(input, GCHandleType.Pinned);
        try
        {
            a.cbData = input.Length;
            a.pbData = h.AddrOfPinnedObject();
            bool ok = protect ? CryptProtectData(ref a, "ClipAtalho", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out b)
                              : CryptUnprotectData(ref a, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out b);
            if (!ok) return null;
            byte[] r = new byte[b.cbData];
            Marshal.Copy(b.pbData, r, 0, b.cbData);
            LocalFree(b.pbData);
            return r;
        }
        finally { h.Free(); }
    }

    static string Protect(string s)
    {
        try { byte[] b = Dpapi(Encoding.UTF8.GetBytes(s), true); return b == null ? "" : Convert.ToBase64String(b); }
        catch { return ""; }
    }

    static string Unprotect(string s)
    {
        if (s.Length == 0) return null;
        try { byte[] b = Dpapi(Convert.FromBase64String(s), false); return b == null ? null : Encoding.UTF8.GetString(b); }
        catch { return null; }
    }

    // ---- HTTP e JSON (sem bibliotecas extras)
    static string JStr(string json, string key)
    {
        Match m = Regex.Match(json ?? "", "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (!m.Success) return null;
        return Regex.Unescape(m.Groups[1].Value);
    }

    static int JInt(string json, string key, int def)
    {
        Match m = Regex.Match(json ?? "", "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\\d+)");
        int v;
        return m.Success && int.TryParse(m.Groups[1].Value, out v) ? v : def;
    }

    static byte[] Http(string method, string url, string ctype, byte[] body, bool auth, out int status)
    {
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = method;
        req.Timeout = 60000;
        req.ReadWriteTimeout = 180000;
        req.UserAgent = "ClipAtalho/" + ShortVersion();
        if (req.Proxy != null) req.Proxy.Credentials = CredentialCache.DefaultCredentials; // proxy da empresa
        if (auth) req.Headers["Authorization"] = "Bearer " + GetAccessToken();
        if (body != null)
        {
            req.ContentType = ctype;
            req.ContentLength = body.Length;
            using (Stream st = req.GetRequestStream()) st.Write(body, 0, body.Length);
        }
        else if (method != "GET") req.ContentLength = 0;
        HttpWebResponse resp;
        try { resp = (HttpWebResponse)req.GetResponse(); }
        catch (WebException we)
        {
            resp = we.Response as HttpWebResponse;
            if (resp == null) throw new Exception("sem conexão com a internet ou bloqueado pela rede (" + we.Status + ")");
        }
        using (resp)
        {
            status = (int)resp.StatusCode;
            using (Stream st = resp.GetResponseStream())
            using (MemoryStream ms = new MemoryStream())
            {
                st.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }

    static string PostForm(string url, string form, out int status)
    {
        byte[] r = Http("POST", url, "application/x-www-form-urlencoded", Encoding.UTF8.GetBytes(form), false, out status);
        return Encoding.UTF8.GetString(r);
    }

    class CloudAuthException : Exception { public CloudAuthException(string m) : base(m) { } }

    static string GetAccessToken()
    {
        lock (tokenLock)
        {
            if (cloudAccess != null && DateTime.UtcNow < cloudAccessExp) return cloudAccess;
            if (cloudRefresh == null) throw new CloudAuthException("o OneDrive não está conectado neste computador");
            int st;
            string j = PostForm(OD_AUTH + "token", "client_id=" + Enc(ClientId) + "&grant_type=refresh_token&scope=" + Enc(OD_SCOPE) +
                "&refresh_token=" + Enc(cloudRefresh), out st);
            string at = JStr(j, "access_token");
            if (at == null)
            {
                string err = JStr(j, "error") ?? ("erro " + st);
                if (err == "invalid_grant" || err == "interaction_required")
                {
                    cloudRefresh = null;
                    SaveLocal();
                    throw new CloudAuthException("o acesso ao OneDrive expirou ou foi removido; conecte de novo");
                }
                throw new Exception("login do OneDrive: " + err);
            }
            cloudAccess = at;
            cloudAccessExp = DateTime.UtcNow.AddSeconds(Math.Max(60, JInt(j, "expires_in", 3600) - 120));
            string rt = JStr(j, "refresh_token");
            if (rt != null && rt != cloudRefresh) { cloudRefresh = rt; SaveLocal(); }
            return at;
        }
    }

    static string GPath(string name)
    {
        string[] parts = name.Split('/');
        for (int i = 0; i < parts.Length; i++) parts[i] = Uri.EscapeDataString(parts[i]);
        return "https://graph.microsoft.com/v1.0/me/drive/special/approot:/" + string.Join("/", parts);
    }

    static byte[] OneDrive(string method, string url, byte[] body, out int status)
    {
        byte[] r = Http(method, url, "application/octet-stream", body, true, out status);
        if (status == 401) // acesso venceu: renova uma vez
        {
            lock (tokenLock) cloudAccess = null;
            r = Http(method, url, "application/octet-stream", body, true, out status);
        }
        return r;
    }

    static string CloudFolder { get { return Path.Combine(cloudDir, "ClipAtalho nuvem"); } }

    static byte[] CloudGet(string name)
    {
        if (cloudProv == 2)
        {
            string f = Path.Combine(CloudFolder, name.Replace('/', '\\'));
            return File.Exists(f) ? File.ReadAllBytes(f) : null;
        }
        int st;
        byte[] r = OneDrive("GET", GPath(name) + ":/content", null, out st);
        if (st == 404) return null;
        if (st >= 300) throw new Exception("OneDrive respondeu " + st + " ao ler " + name + ": " + (JStr(Encoding.UTF8.GetString(r), "message") ?? ""));
        return r;
    }

    static void CloudPut(string name, byte[] data)
    {
        if (cloudProv == 2)
        {
            string f = Path.Combine(CloudFolder, name.Replace('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(f));
            File.WriteAllBytes(f, data);
            return;
        }
        int st;
        byte[] r = OneDrive("PUT", GPath(name) + ":/content", data, out st);
        if (st >= 300) throw new Exception("OneDrive respondeu " + st + " ao gravar " + name + ": " + (JStr(Encoding.UTF8.GetString(r), "message") ?? ""));
    }

    static List<string> CloudListImages()
    {
        List<string> names = new List<string>();
        if (cloudProv == 2)
        {
            string d = Path.Combine(CloudFolder, "imagens");
            if (Directory.Exists(d)) foreach (string f in Directory.GetFiles(d, "img_*.png")) names.Add(Path.GetFileName(f));
            return names;
        }
        string url = GPath("imagens") + ":/children?$select=name&$top=500";
        while (url != null)
        {
            int st;
            string j = Encoding.UTF8.GetString(OneDrive("GET", url, null, out st));
            if (st == 404) break;
            if (st >= 300) throw new Exception("OneDrive respondeu " + st + " ao listar as imagens");
            foreach (Match m in Regex.Matches(j, "\"name\"\\s*:\\s*\"([^\"]+)\"")) names.Add(m.Groups[1].Value);
            url = JStr(j, "@odata.nextLink");
        }
        return names;
    }

    static CloudInfo CloudReadInfo()
    {
        byte[] b = CloudGet("ClipAtalho-info.txt");
        return b == null ? null : ParseInfo(Encoding.UTF8.GetString(b));
    }

    // Roda o trabalho de rede fora da janela (que continua respondendo) e espera o fim.
    static string RunWorker(Func<string> work, string busy)
    {
        if (cloudBusy) return "já existe uma sincronização em andamento";
        cloudBusy = true;
        cloudBusyText = busy;
        RepaintAll();
        bool mgrOn = hwndMgr != IntPtr.Zero && IsWindowEnabled(hwndMgr);
        if (mgrOn) EnableWindow(hwndMgr, false);
        string[] result = new string[1];
        bool[] done = new bool[1];
        ThreadPool.QueueUserWorkItem(delegate(object o)
        {
            try { result[0] = work(); }
            catch (Exception e) { result[0] = e.Message; }
            lock (done) done[0] = true;
            PostMessageW(hwndMain, WM_CLOUDDONE, IntPtr.Zero, IntPtr.Zero);
        });
        MSG msg;
        while (true)
        {
            lock (done) if (done[0]) break;
            int g = GetMessageW(out msg, IntPtr.Zero, 0, 0);
            if (g <= 0) { PostQuitMessage(0); break; }
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        if (mgrOn) EnableWindow(hwndMgr, true);
        cloudBusy = false;
        cloudBusyText = "";
        RepaintAll();
        return result[0];
    }

    static void CloudError(string what, string err, bool quiet)
    {
        Log("Nuvem: falha ao " + what + ": " + err);
        if (quiet) Balloon("ClipAtalho", "Não foi possível " + what + ": " + err);
        else Info("Não foi possível " + what + ":\n" + err);
        RepaintAll();
    }

    static string ImgNameOf(Clip c) { return c.ImgFile ?? ("img_" + c.Hash + ".png"); }

    // ☁↑ Enviar: confirma (mostrando o backup que já está na nuvem) e envia tudo.
    static void CloudUpload(bool ask, bool quiet)
    {
        if (!CloudReady()) { if (!quiet) CloudConnButton(); return; }
        if (!quiet && hwndMgr != IntPtr.Zero && IsWindowVisible(hwndMgr) && !ResolveDirty(false)) return;
        CloudInfo remote = null;
        string err = RunWorker(delegate { remote = CloudReadInfo(); return null; }, "Consultando a nuvem…");
        if (err != null) { CloudError("consultar a nuvem", err, quiet); return; }
        cloudInfo = remote;
        bool foreign = remote != null && remote.Stamp != cloudLastSync && remote.Machine != Environment.MachineName;
        if (remote != null && (ask || foreign))
        {
            dlgEscResult = 2;
            dlgPrimary = 1;
            dlgWarn = foreign;
            int r = AskDialog("Substituir o backup da nuvem?",
                "Já existe um backup na nuvem, " + InfoText(remote) + "." +
                (foreign ? "\nATENÇÃO: ele veio de outro computador depois da última sincronização deste. Se substituir, o que foi feito lá se perde (baixe antes, se precisar)." : "") +
                "\nSubstituir pelo deste computador (" + clips.Count + " clips, " + cfgAbbr.Count + " atalhos)?",
                "Substituir", "Cancelar", null);
            dlgWarn = false;
            if (r != 1) return;
        }
        string ini = ReadIni() ?? "";
        List<string> imgs = new List<string>();
        foreach (Clip c in clips) if (c.IsImage && EnsureImageFile(c)) imgs.Add(ImgNameOf(c));
        DateTime now = DateTime.Now;
        string info = "; ClipAtalho - backup na nuvem\r\ndata=" + now.ToString("yyyyMMddHHmmss") + "\r\ncomputador=" + Environment.MachineName +
            "\r\nclips=" + clips.Count + "\r\natalhos=" + cfgAbbr.Count + "\r\nversao=" + VERSION + "\r\n";
        string imgDir = ImgDir;
        err = RunWorker(delegate
        {
            List<string> have = CloudListImages();
            foreach (string f in imgs)
            {
                if (have.Contains(f)) continue;
                string lp = Path.Combine(imgDir, f);
                if (File.Exists(lp)) CloudPut("imagens/" + f, File.ReadAllBytes(lp));
            }
            CloudPut("ClipAtalho.ini", Encoding.UTF8.GetBytes(ini));
            CloudPut("ClipAtalho-info.txt", Encoding.UTF8.GetBytes(info)); // por último: só fica valendo se tudo subiu
            return null;
        }, "Enviando para a nuvem…");
        if (err != null) { CloudError("enviar para a nuvem", err, quiet); return; }
        cloudInfo = ParseInfo(info);
        cloudLastSync = cloudInfo.Stamp;
        cloudDirty = false;
        SaveLocal();
        Log("Nuvem: enviado (" + clips.Count + " clips, " + cfgAbbr.Count + " atalhos)");
        RepaintAll();
        if (quiet) Balloon("ClipAtalho", "Alterações enviadas para a nuvem.");
        else Info("Pronto: enviado para a nuvem.");
    }

    // 💻↓ Baixar: mostra o backup da nuvem e, confirmado, substitui os dados deste computador.
    static void CloudDownload(bool alreadyAsked)
    {
        if (!CloudReady()) { CloudConnButton(); return; }
        if (hwndMgr != IntPtr.Zero && IsWindowVisible(hwndMgr) && !ResolveDirty(false)) return;
        CloudInfo remote = null;
        string err = RunWorker(delegate { remote = CloudReadInfo(); return null; }, "Consultando a nuvem…");
        if (err != null) { CloudError("consultar a nuvem", err, false); return; }
        cloudInfo = remote;
        RepaintAll();
        if (remote == null) { Info("Ainda não há backup na nuvem.\nEnvie primeiro, pelo computador que está com os dados atualizados."); return; }
        if (!alreadyAsked)
        {
            dlgEscResult = 2;
            dlgPrimary = 1;
            if (AskDialog("Baixar da nuvem?", "Backup na nuvem: " + InfoText(remote) + ".\nOs clips, atalhos e configurações deste computador (" + clips.Count + " clips, " +
                cfgAbbr.Count + " atalhos) serão substituídos. Uma cópia dos atuais fica guardada na pasta do programa.", "Baixar", "Cancelar", null) != 1) return;
        }
        string ini = null;
        string imgDir = ImgDir;
        err = RunWorker(delegate
        {
            byte[] b = CloudGet("ClipAtalho.ini");
            if (b == null) return "o arquivo de dados não está na nuvem";
            ini = Encoding.UTF8.GetString(b).TrimStart((char)0xFEFF);
            Directory.CreateDirectory(imgDir);
            foreach (string f in CloudListImages())
            {
                string lp = Path.Combine(imgDir, Path.GetFileName(f));
                if (File.Exists(lp)) continue;
                byte[] ib = CloudGet("imagens/" + f);
                if (ib != null) File.WriteAllBytes(lp, ib);
            }
            return null;
        }, "Baixando da nuvem…");
        if (err != null) { CloudError("baixar da nuvem", err, false); return; }
        CopyIniAside("antes de baixar da nuvem");
        cloudApplying = true;
        try
        {
            WriteIni(ini);
            ReloadAll();
        }
        finally { cloudApplying = false; }
        cloudLastSync = remote.Stamp;
        cloudDirty = false;
        KillTimer(hwndMain, (IntPtr)TIMER_CLOUDASK);
        SaveLocal();
        Log("Nuvem: baixado (" + remote.Stamp + ")");
        RepaintAll();
        Info("Pronto: dados baixados da nuvem.\n" + clips.Count + " clips e " + cfgAbbr.Count + " atalhos.");
    }

    // Depois de cada alteração feita à mão: pergunta (ou envia sozinho) alguns segundos depois da última.
    static void MarkCloudDirty()
    {
        if (cloudQuiet || cloudApplying || !CloudReady() || cloudMode == 2 || hwndMain == IntPtr.Zero) return;
        cloudDirty = true;
        SetTimer(hwndMain, (IntPtr)TIMER_CLOUDASK, 6000, IntPtr.Zero);
    }

    static void OnCloudAskTimer()
    {
        KillTimer(hwndMain, (IntPtr)TIMER_CLOUDASK);
        if (!cloudDirty || !CloudReady() || cloudMode == 2) return;
        bool mgrVisible = hwndMgr != IntPtr.Zero && IsWindowVisible(hwndMgr);
        if (cloudMode == 0 && (!mgrVisible || hDlg != IntPtr.Zero || hMenuWnd != IntPtr.Zero || cloudBusy || editDirty || expDirty))
        {
            SetTimer(hwndMain, (IntPtr)TIMER_CLOUDASK, 5000, IntPtr.Zero); // pergunta quando a janela estiver aberta e livre
            return;
        }
        if (cloudBusy || hDlg != IntPtr.Zero) { SetTimer(hwndMain, (IntPtr)TIMER_CLOUDASK, 5000, IntPtr.Zero); return; }
        if (cloudMode == 1) { CloudUpload(false, true); return; }
        dlgEscResult = 2;
        dlgPrimary = 1;
        int r = AskDialog("Enviar para a nuvem?", "Você fez alterações. Enviar agora para a nuvem (" + (cloudProv == 1 ? "OneDrive" : "pasta") +
            "), para ficarem iguais no outro computador?", "Enviar", "Agora não", "Sempre enviar");
        if (r == 3) { cloudMode = 1; SaveLocal(); CloudUpload(false, false); }
        else if (r == 1) CloudUpload(true, false);
        else cloudDirty = false; // pergunta de novo na próxima alteração
    }

    // Ao abrir: confere, sem travar o programa, se outro computador enviou uma versão mais nova.
    static void CloudCheckAsync()
    {
        if (!CloudReady() || !cloudCheck || cloudBusy) return;
        cloudLastCheck = DateTime.Now;
        ThreadPool.QueueUserWorkItem(delegate(object o)
        {
            CloudInfo i = null;
            try { i = CloudReadInfo(); }
            catch (Exception e) { Log("Nuvem: verificação ao abrir falhou: " + e.Message); return; }
            cloudCheckResult = i;
            PostMessageW(hwndMain, WM_CLOUDCHECK, IntPtr.Zero, IntPtr.Zero);
        });
    }

    static void OnCloudCheckResult()
    {
        CloudInfo i = cloudCheckResult;
        cloudCheckResult = null;
        if (i == null) return;
        cloudInfo = i;
        RepaintAll();
        if (i.Stamp == cloudLastSync || i.Machine == Environment.MachineName || hDlg != IntPtr.Zero || cloudBusy) return;
        dlgEscResult = 2;
        dlgPrimary = 1;
        if (AskDialog("Versão mais nova na nuvem", "Há na nuvem uma versão mais nova dos seus clips e atalhos, " + InfoText(i) +
            ".\nBaixar agora? (os dados deste computador serão substituídos; uma cópia fica guardada)", "Baixar", "Agora não", null) == 1)
            CloudDownload(true);
        else { cloudLastSync = i.Stamp; SaveLocal(); } // não pergunta de novo por esta mesma versão
    }

    // ---- tempo real: cada Ctrl+C vai para a nuvem; o outro computador confere a cada 5 segundos
    static bool cloudLive, cloudLiveImg, livePolling, liveInit, liveApplying;
    static string liveLastId = "";
    class LivePacket { public string Id = "", Machine = "", Type = "texto", Img, Text = ""; public byte[] ImgBytes; }
    static LivePacket liveIncoming;

    static void StartLive()
    {
        KillTimer(hwndMain, (IntPtr)TIMER_LIVE);
        liveInit = false;
        if (cloudLive && CloudReady()) SetTimer(hwndMain, (IntPtr)TIMER_LIVE, 5000, IntPtr.Zero);
    }

    static void LiveSend(Clip c)
    {
        if (!cloudLive || !CloudReady() || liveApplying || c == null || c.Empty) return;
        if (c.IsImage && (!cloudLiveImg || !EnsureImageFile(c))) return;
        if (!c.IsImage && c.Text.Length == 0) return;
        string id = DateTime.UtcNow.Ticks + "-" + Environment.MachineName;
        liveLastId = id; // o próprio envio não volta para cá
        string img = c.IsImage ? ImgNameOf(c) : null;
        byte[] ib = img != null ? File.ReadAllBytes(Path.Combine(ImgDir, img)) : null;
        string pay = "id=" + id + "\r\ncomputador=" + Environment.MachineName + "\r\ntipo=" + (img != null ? "imagem\r\nimagem=" + img : "texto") + "\r\n---\r\n" + (img != null ? "" : c.Text);
        ThreadPool.QueueUserWorkItem(delegate(object o)
        {
            try
            {
                if (ib != null) CloudPut("agora/" + img, ib);
                CloudPut("ClipAtalho-agora.txt", Encoding.UTF8.GetBytes(pay));
            }
            catch (Exception e) { Log("Tempo real: falha ao enviar: " + e.Message); }
        });
    }

    static LivePacket ParseLive(string t)
    {
        LivePacket p = new LivePacket();
        int sep = t.IndexOf("\r\n---\r\n");
        if (sep < 0) return null;
        foreach (string raw in t.Substring(0, sep).Split('\n'))
        {
            int eq = raw.IndexOf('=');
            if (eq < 1) continue;
            string k = raw.Substring(0, eq).Trim(), v = raw.Substring(eq + 1).Trim();
            if (k == "id") p.Id = v;
            else if (k == "computador") p.Machine = v;
            else if (k == "tipo") p.Type = v;
            else if (k == "imagem") p.Img = Path.GetFileName(v);
        }
        p.Text = t.Substring(sep + 7);
        return p.Id.Length > 0 ? p : null;
    }

    static void OnLiveTimer()
    {
        if (!cloudLive || !CloudReady()) { KillTimer(hwndMain, (IntPtr)TIMER_LIVE); return; }
        if (livePolling || cloudBusy) return;
        livePolling = true;
        bool wantImg = cloudLiveImg;
        string imgDir = ImgDir, last = liveLastId;
        ThreadPool.QueueUserWorkItem(delegate(object o)
        {
            try
            {
                byte[] b = CloudGet("ClipAtalho-agora.txt");
                LivePacket p = b == null ? null : ParseLive(Encoding.UTF8.GetString(b).TrimStart((char)0xFEFF));
                if (p != null && p.Id != last && p.Type == "imagem" && wantImg && p.Img != null && !File.Exists(Path.Combine(imgDir, p.Img)))
                    p.ImgBytes = CloudGet("agora/" + p.Img);
                if (p != null && p.Id != last) { liveIncoming = p; PostMessageW(hwndMain, WM_CLOUDLIVE, IntPtr.Zero, IntPtr.Zero); }
                else if (p == null) { liveInit = true; }
            }
            catch (Exception e) { Log("Tempo real: falha ao conferir: " + e.Message); }
            finally { livePolling = false; }
        });
    }

    static void OnLiveIncoming()
    {
        LivePacket p = liveIncoming;
        liveIncoming = null;
        if (p == null || p.Id == liveLastId) return;
        liveLastId = p.Id;
        bool first = !liveInit;
        liveInit = true;
        if (first || p.Machine == Environment.MachineName) return; // ao ligar, não cola algo antigo
        liveApplying = true;
        try
        {
            if (p.Type == "imagem")
            {
                if (!cloudLiveImg || p.Img == null) return;
                string lp = Path.Combine(ImgDir, p.Img);
                if (!File.Exists(lp) && p.ImgBytes != null) { Directory.CreateDirectory(ImgDir); File.WriteAllBytes(lp, p.ImgBytes); }
                Clip ic = LoadImageClip(p.Img, false);
                if (ic == null || ic.Image == IntPtr.Zero) return;
                AddCopiedClip(ic);
                SetClipboardImage(ic);
            }
            else
            {
                if (p.Text.Length == 0) return;
                AddCopiedClip(new Clip(p.Text, false));
                SetClipboardText(p.Text);
            }
            RefreshPopupList();
            if (popMode == MODE_NONE) ShowNotice("Recebido do computador " + p.Machine + " — já dá para colar (Ctrl+V).");
        }
        catch (Exception e) { Log("Tempo real: falha ao receber: " + e.Message); }
        finally { liveApplying = false; }
    }

    static void CloudProvMenu()
    {
        List<MI> mi = new List<MI>();
        MI a = Item(1, "OneDrive (direto pela internet, sem instalar nada)"); a.Check = cloudProv == 1; mi.Add(a);
        MI b = Item(2, "Pasta: Google Drive, Dropbox, rede ou pendrive…"); b.Check = cloudProv == 2; mi.Add(b);
        if (cloudProv != 0) { mi.Add(Sep()); mi.Add(Item(3, "Não usar nuvem neste computador")); }
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_CLOUDPROV), out r);
        int cmd = ShowMenu(mi, r.Left, r.Bottom + Px(4), hwndMgr);
        if (cmd == 1) { cloudProv = 1; cloudInfo = null; SaveLocal(); RepaintAll(); if (cloudRefresh == null) CloudConnectOneDrive(); }
        else if (cmd == 2) CloudChooseFolder();
        else if (cmd == 3) { cloudProv = 0; cloudInfo = null; KillTimer(hwndMain, (IntPtr)TIMER_CLOUDASK); SaveLocal(); RepaintAll(); }
    }

    static void CloudModeMenu()
    {
        List<MI> mi = new List<MI>();
        for (int i = 0; i < CloudModeNames.Length; i++) { MI m = Item(1 + i, "Depois de alterar algo: " + CloudModeNames[i]); m.Check = i == cloudMode; mi.Add(m); }
        RECT r;
        GetWindowRect(GetDlgItem(hwndMgr, IDC_CLOUDMODE), out r);
        int cmd = ShowMenu(mi, r.Left, r.Bottom + Px(4), hwndMgr);
        if (cmd < 1) return;
        cloudMode = cmd - 1;
        SaveLocal();
        RepaintAll();
    }

    static void CloudConnButton()
    {
        if (cloudProv == 0) { CloudProvMenu(); return; }
        if (cloudProv == 2) { CloudChooseFolder(); return; }
        if (cloudRefresh == null) { CloudConnectOneDrive(); return; }
        dlgEscResult = 2;
        dlgPrimary = 1;
        if (AskDialog("Desconectar o OneDrive", "Desconectar o OneDrive deste computador?\nO backup que está na nuvem continua lá. Para tirar o acesso do ClipAtalho também na sua conta Microsoft: account.microsoft.com › Privacidade › Aplicativos e serviços.",
            "Desconectar", "Cancelar", null) != 1) return;
        lock (tokenLock) { cloudRefresh = null; cloudAccess = null; }
        cloudInfo = null;
        SaveLocal();
        RepaintAll();
    }

    static void CloudChooseFolder()
    {
        string d = PickFolder("Escolha a pasta da nuvem (ex.: a pasta do Google Drive ou do Dropbox). O ClipAtalho cria dentro dela a pasta \"ClipAtalho nuvem\".");
        if (d == null) return;
        cloudProv = 2;
        cloudDir = d;
        cloudInfo = null;
        SaveLocal();
        RepaintAll();
        AfterCloudReady();
    }

    // Acabou de conectar: se já houver backup, oferece baixar; se não, oferece enviar.
    static void AfterCloudReady()
    {
        CloudInfo remote = null;
        string err = RunWorker(delegate { remote = CloudReadInfo(); return null; }, "Consultando a nuvem…");
        if (err != null) { CloudError("consultar a nuvem", err, false); return; }
        cloudInfo = remote;
        RepaintAll();
        dlgEscResult = 2;
        dlgPrimary = 1;
        if (remote != null && remote.Stamp != cloudLastSync)
        {
            if (AskDialog("Backup encontrado na nuvem", "Já existe um backup na nuvem, " + InfoText(remote) + ".\nBaixar para este computador agora?",
                "Baixar", "Agora não", null) == 1) CloudDownload(true);
        }
        else if (remote == null)
        {
            if (AskDialog("Nuvem pronta", "Ainda não há nada na nuvem. Enviar os clips e atalhos deste computador agora?", "Enviar", "Agora não", null) == 1)
                CloudUpload(false, false);
        }
    }

    // Login do OneDrive pela página oficial da Microsoft (código de dispositivo): a senha nunca passa pelo programa.
    static void CloudConnectOneDrive()
    {
        if (ClientId.Length == 0)
        {
            Info("A conexão direta com o OneDrive será liberada em uma atualização do ClipAtalho.\nPor enquanto, use \"Pasta: Google Drive, Dropbox, rede ou pendrive\".");
            return;
        }
        string dj = null;
        string err = RunWorker(delegate
        {
            int st;
            dj = PostForm(OD_AUTH + "devicecode", "client_id=" + Enc(ClientId) + "&scope=" + Enc(OD_SCOPE), out st);
            return JStr(dj, "device_code") == null ? ("a Microsoft recusou o pedido: " + (JStr(dj, "error_description") ?? JStr(dj, "error") ?? ("erro " + st))) : null;
        }, "Preparando o login do OneDrive…");
        if (err != null) { CloudError("conectar ao OneDrive", err, false); return; }
        string code = JStr(dj, "user_code"), device = JStr(dj, "device_code"), uri = JStr(dj, "verification_uri") ?? "https://microsoft.com/devicelogin";
        int interval = Math.Max(3, JInt(dj, "interval", 5)), expires = JInt(dj, "expires_in", 900);
        SetClipboardText(code);
        try { Process.Start(uri); } catch { }
        cloudAuthState = 0;
        cloudAuthCancel = false;
        string authErr = null;
        string client = ClientId;
        ThreadPool.QueueUserWorkItem(delegate(object o)
        {
            DateTime end = DateTime.UtcNow.AddSeconds(expires);
            int wait = interval;
            while (!cloudAuthCancel && DateTime.UtcNow < end)
            {
                Thread.Sleep(wait * 1000);
                if (cloudAuthCancel) return;
                try
                {
                    int st;
                    string j = PostForm(OD_AUTH + "token", "client_id=" + Enc(client) + "&grant_type=urn:ietf:params:oauth:grant-type:device_code&device_code=" + Enc(device), out st);
                    string rt = JStr(j, "refresh_token"), e = JStr(j, "error");
                    if (rt != null)
                    {
                        lock (tokenLock)
                        {
                            cloudRefresh = rt;
                            cloudAccess = JStr(j, "access_token");
                            cloudAccessExp = DateTime.UtcNow.AddSeconds(Math.Max(60, JInt(j, "expires_in", 3600) - 120));
                        }
                        PostMessageW(hwndMain, WM_CLOUDAUTH, (IntPtr)1, IntPtr.Zero);
                        return;
                    }
                    if (e == "slow_down") wait += 5;
                    else if (e != "authorization_pending") { authErr = JStr(j, "error_description") ?? e; break; }
                }
                catch (Exception ex) { authErr = ex.Message; break; }
            }
            if (!cloudAuthCancel) PostMessageW(hwndMain, WM_CLOUDAUTH, (IntPtr)2, IntPtr.Zero);
        });
        while (cloudAuthState == 0)
        {
            cloudAuthWaiting = true;
            dlgEscResult = 2;
            dlgPrimary = 3;
            int r = AskDialog("Conectar ao OneDrive",
                "1. Na página da Microsoft que abriu no navegador, cole o código:  " + code + "  (já está copiado)\n2. Entre com a sua conta Microsoft e autorize o ClipAtalho.\nEsta janela fecha sozinha quando terminar.",
                "Abrir a página de novo", "Cancelar", null);
            cloudAuthWaiting = false;
            if (r == 1) { try { Process.Start(uri); } catch { } continue; }
            if (r == 100) break;
            cloudAuthCancel = true;
            return;
        }
        if (cloudAuthState != 1) { CloudError("conectar ao OneDrive", authErr ?? "o código expirou; tente de novo", false); return; }
        cloudProv = 1;
        SaveLocal();
        StartLive();
        Log("Nuvem: OneDrive conectado");
        RepaintAll();
        Info("OneDrive conectado!");
        AfterCloudReady();
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

    static void PaintInput(IntPtr hdc)
    {
        int w = Px(420);
        DrawTextAt(hdc, dlgTitle, hHeadFont, C_TEXT, Px(22), Px(12), w - Px(22), Px(42), 0x8024);
        DrawTextAt(hdc, dlgPrompt, hUiFont, C_GRAY, Px(22), Px(52), w - Px(22), Px(72), 0x8024);
        RoundRect(hdc, Px(18), Px(78), Px(384), Px(32), Px(8), A_WHITE, A_BTNBORDER);
        RoundRect(hdc, 0, 0, w, Px(176), Px(WIN_R), 0, A_BTNBORDER);
    }

    // Janela pequena para digitar um valor (horário, número). Devolve null se cancelada.
    static string InputDialog(string title, string prompt, string value)
    {
        dlgLabels = new string[] { "OK", "Cancelar", null };
        dlgMessage = "";
        dlgTitle = title;
        dlgPrompt = prompt;
        dlgResult = 0;
        dlgInput = true;
        dlgPrimary = 1;
        int w = Px(420), h = Px(176);
        RECT o;
        int x = (GetSystemMetrics(0) - w) / 2, y = (GetSystemMetrics(1) - h) / 2;
        if (hwndMgr != IntPtr.Zero && GetWindowRect(hwndMgr, out o)) { x = o.Left + (o.Right - o.Left - w) / 2; y = o.Top + (o.Bottom - o.Top - h) / 2; }
        hDlg = CreateWindowExW(0x10000 | 0x80000, "ClipAtalhoDialog", title, 0x80000000 | 0x02000000, x, y, w, h, hwndMgr, IntPtr.Zero, hInst, IntPtr.Zero);
        if (hDlg == IntPtr.Zero) { dlgInput = false; return null; }
        IntPtr me = hDlg;
        RoundWindow(me, w, h);
        AttachShade(me, NewShadeWindow(false));
        IntPtr ed = Child(me, "EDIT", value, 0x10000 | 0x80, 28, 85, 364, 20, IDC_IN_EDIT, -2);
        Child(me, "BUTTON", "", 0x10000 | 0xB, 196, 124, 100, 36, IDC_DLG + 1, -2);
        Child(me, "BUTTON", "", 0x10000 | 0xB, 304, 124, 98, 36, IDC_DLG + 2, -2);
        if (hwndMgr != IntPtr.Zero) EnableWindow(hwndMgr, false);
        FadeWindow(me, true);
        ShowWindow(me, 5);
        PlaceShade(me);
        SetForegroundWindow(me);
        SetFocus(ed);
        SendMessageW(ed, 0x00B1, IntPtr.Zero, (IntPtr)(-1)); // seleciona tudo
        MSG msg;
        while (dlgResult == 0)
        {
            int g = GetMessageW(out msg, IntPtr.Zero, 0, 0);
            if (g <= 0) { PostQuitMessage(0); dlgResult = 2; break; }
            if (msg.message == 0x0100 && msg.wParam.ToInt32() == 0x0D) { dlgResult = 1; break; } // Enter = OK
            if (IsDialogMessageW(me, ref msg)) continue;
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
        string text = CtlText(ed);
        int result = dlgResult;
        dlgInput = false;
        dlgPrimary = 3;
        if (hwndMgr != IntPtr.Zero) EnableWindow(hwndMgr, true);
        FadeOutAndWait(me);
        DropShade(me);
        winAlpha.Remove(me);
        DestroyWindow(me);
        hDlg = IntPtr.Zero;
        if (hwndMgr != IntPtr.Zero) SetForegroundWindow(hwndMgr);
        return result == 1 ? text : null;
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
        ResetEditHist(hXAbbr);
        ResetEditHist(hXText);
        SetXDirty(false);
        InvalidateRect(GetDlgItem(hwndMgr, IDC_XPIN), IntPtr.Zero, true);
    }

    // reloadFields=false preserva o que está sendo digitado (usado ao reordenar/fixar).
    static void RefreshX(bool reloadFields)
    {
        if (hwndMgr == IntPtr.Zero || !IsWindowVisible(hwndMgr)) return;
        string xf = Fold(xFilter.Trim());
        xKeys = xf.Length == 0 ? new List<string>(cfgAbbrOrder)
            : cfgAbbrOrder.FindAll(delegate(string k) { string v; return Fold(k).Contains(xf) || (cfgAbbr.TryGetValue(k, out v) && Fold(v).Contains(xf)); });
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
    // Versões anteriores: acrescenta hj1, hr1, dh1 e ext1 uma única vez (sem mexer nos atalhos que já existem).
    static void EnsureDateAbbr()
    {
        if (cfgDateAbbrDone) return;
        List<string> order = new List<string>(cfgAbbrOrder);
        Dictionary<string, string> d = new Dictionary<string, string>(cfgAbbr);
        List<string> loose = new List<string>(cfgAbbrLoose);
        bool added = false;
        foreach (string[] a in DefaultAbbr)
        {
            if (a[1].IndexOf('{') < 0 || d.ContainsKey(a[0])) continue;
            order.Add(a[0]);
            d[a[0]] = a[1];
            cfgAbbrDate[a[0]] = DateTime.Now;
            added = true;
        }
        if (added)
        {
            xUndoing = true;
            try { WriteAbbr(order, d, loose); }
            finally { xUndoing = false; }
            Log("Atalhos de data e hora acrescentados (hj1, hr1, dh1, ext1)");
        }
        cfgUndoing = true;
        try { SetIniValue("geral", "atalhos_data", "sim"); }
        finally { cfgUndoing = false; }
        LoadConfig();
    }

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
            Info("O atalho pode ter letras, números, símbolos e espaços (até 32 caracteres).\nNão pode ter \"=\", nem começar com \"-\", \";\" ou \"[\".\nExemplos: bn1, @end, bom dia!");
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
        if (convertFrom != null) AfterConvertSaved();
        return true;
    }

    // Aviso em vermelho ao lado do campo do atalho.
    static string XAbbrWarning()
    {
        if (hXAbbr == IntPtr.Zero) return null;
        string a = CtlText(hXAbbr).Trim().ToLowerInvariant();
        if (a.Length == 0) return null;
        if (!ValidAbbr(a)) return "Atalho inválido";
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
                    xDragFrom = xFilter.Trim().Length == 0 ? idx : -1; // com busca ativa não dá para arrastar
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

    // Limpeza automática dos não fixados (os fixados nunca saem). Se o PC estava desligado no horário, limpa ao abrir.
    static void RunAutoDelete()
    {
        if (autoMode == 0) return;
        cloudQuiet = true; // limpeza automática não pergunta se envia para a nuvem
        try { RunAutoDeleteCore(); }
        finally { cloudQuiet = false; }
    }

    static void RunAutoDeleteCore()
    {
        DateTime now = DateTime.Now;
        int removed;
        if (autoMode == 1)
        {
            if (!clips.Exists(delegate(Clip c) { return !c.Pinned && (now - c.Added).TotalMinutes >= cfgAutoDel; })) return;
            PushClipUndo();
            removed = clips.RemoveAll(delegate(Clip c) { return !c.Pinned && (now - c.Added).TotalMinutes >= cfgAutoDel; });
        }
        else
        {
            if (lastClean == DateTime.MinValue) { MarkCleaned(now); return; } // começa a contar a partir de agora
            bool due = autoMode == 4 ? (now - lastClean).TotalHours >= autoEvery : lastClean < LastScheduled(now);
            if (!due) return;
            MarkCleaned(now);
            if (!clips.Exists(delegate(Clip c) { return !c.Pinned; })) return;
            PushClipUndo();
            removed = clips.RemoveAll(delegate(Clip c) { return !c.Pinned; });
        }
        if (removed == 0) return;
        Log("Limpeza automática: " + removed + " clip(s) não fixado(s) apagado(s)");
        SaveClips();
        RefreshList();
        RefreshPopupList();
    }

    static void MarkCleaned(DateTime t)
    {
        lastClean = t;
        SetIniValue("geral", "ultima_limpeza", t.ToString("yyyyMMddHHmm"));
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
            case IDC_SEG3: return "Nuvem";
            case IDC_T_DATEKEY: return "Ctrl+Shift+D cola a data de hoje  ·  formato da data (também dos atalhos hj1 e dh1):";
            case IDC_DATEFMT: return DateStr(DateTime.Now) + "   ▼";
            case IDC_YEARFMT: return (cfgYear2 ? "Ano com 2 dígitos" : "Ano com 4 dígitos") + "   ▼";
            case IDC_HOURFMT: return (cfgAmPm ? "AM/PM" : "24h") + "  ▼";
            case IDC_CLOUDPROV: return "Nuvem: " + (cloudProv == 1 ? "OneDrive" : cloudProv == 2 ? "pasta (Google Drive, Dropbox…)" : "escolher…") + "   ▼";
            case IDC_CLOUDCONN: return cloudProv == 1 ? (cloudRefresh != null ? "Desconectar" : "Conectar ao OneDrive") : cloudProv == 2 ? "Trocar pasta…" : "Escolher nuvem…";
            case IDC_CLOUDMODE: return "Depois de alterar algo: " + CloudModeNames[cloudMode] + "   ▼";
            case IDC_GOCLOUD: return "Quer guardar online e usar em outro computador? Use a aba Nuvem  →";
            case IDC_T_CLOUDLIVE: return "Tempo real: o que você copiar aqui aparece no outro computador em poucos segundos (e vice-versa)";
            case IDC_T_CLOUDLIVEIMG: return "Incluir imagens no tempo real (usa mais internet)";
            case IDC_T_CLOUDCHECK: return "Ao abrir o programa, avisar se a nuvem tiver uma versão mais nova (enviada por outro computador)";
            case IDC_BK_CLIPS: return "Backup dos clips";
            case IDC_BK_X: return "Backup das expansões";
            case IDC_BK_ALL: return "Backup completo";
            case IDC_RESTOREBK: return "Restaurar backup…";
            case IDC_AUTODEL: return AutoDelLabel() + "   ▼";
            case IDC_LIMIT: return (cfgLimit <= 0 ? "Sem limite" : cfgLimit + " itens") + "   ▼";
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
        bool onCard = id >= IDC_MOD && id != IDC_DELPIN && id != IDC_XSAVENEW && id != IDC_XSORT && id != IDC_CLOSE && id != IDC_SEG && id != IDC_SEG + 1 && id != IDC_SEG3 && id != IDC_HOURFMT && !(id >= IDC_XSAVE && id <= IDC_XCLEAR); // dentro de cartão ou sobre o fundo
        FillRect(d.hDC, ref d.rcItem, onCard ? hWhiteBrush : hBgBrush);
        int l = d.rcItem.Left, t = d.rcItem.Top, w = d.rcItem.Right - d.rcItem.Left, h = d.rcItem.Bottom - d.rcItem.Top;

        if (id == IDC_UNDO || id == IDC_REDO || id == IDC_XUNDO || id == IDC_XREDO || id == IDC_CUNDO || id == IDC_CREDO) // desfazer / refazer
        {
            bool can = id == IDC_UNDO ? clipUndo.Count > 0 : id == IDC_REDO ? clipRedo.Count > 0 : id == IDC_XUNDO ? xUndo.Count > 0
                : id == IDC_XREDO ? xRedo.Count > 0 : id == IDC_CUNDO ? cfgUndo.Count > 0 : cfgRedo.Count > 0;
            bool onBg = id == IDC_CUNDO || id == IDC_CREDO;
            if (onBg) FillRect(d.hDC, ref d.rcItem, hBgBrush);
            if (can) RoundRect(d.hDC, l, t, w, h, Px(7), down ? A_DOWN : (onBg ? A_BG : A_WHITE), A_BTNBORDER);
            DrawTextAt(d.hDC, (id == IDC_UNDO || id == IDC_XUNDO || id == IDC_CUNDO) ? "\uE7A7" : "\uE7A6", hIconFont, can ? C_ACCENT : C_LINE, l, t, l + w, t + h, 0x25);
            return;
        }
        if (id == IDC_GOCLOUD) // link para a aba Nuvem
        {
            DrawTextAt(d.hDC, ButtonLabel(id), down ? hUiBold : hUiFont, C_ACCENT, l, t, l + w, t + h, 0x26); // à direita
            return;
        }
        if (id == IDC_CLOUDUP || id == IDC_CLOUDDOWN) // enviar (nuvem ↑) e baixar (PC ↓)
        {
            bool up = id == IDC_CLOUDUP, ok = CloudReady() && !cloudBusy;
            uint f = !ok ? A_DOWN : up ? (down ? A_ACCENT_DOWN : A_ACCENT) : (down ? A_DOWN : A_WHITE);
            RoundRect(d.hDC, l, t, w, h, Px(11), f, up && ok ? 0 : A_BTNBORDER);
            uint tc = !ok ? C_GRAY : up ? C_WHITE : C_ACCENT;
            int ix = l + Px(22);
            DrawTextAt(d.hDC, up ? "\uE753" : "\uE7F4", hIconBig, tc, ix, t, ix + Px(30), t + h, 0x25);           // nuvem / computador
            DrawTextAt(d.hDC, up ? "\uE74A" : "\uE74B", hIconFont, tc, ix + Px(28), t, ix + Px(46), t + h, 0x25); // seta
            DrawTextAt(d.hDC, up ? "Enviar para a nuvem" : "Baixar da nuvem", hUiBold, tc, ix + Px(56), t, l + w - Px(10), t + h, 0x24);
            return;
        }
        if ((id >= IDC_T_STARTUP && id <= IDC_T_EXPAND) || id == IDC_T_IMAGES || id == IDC_T_DATADIR || id == IDC_T_DATEKEY || id == IDC_T_CLOUDCHECK || id == IDC_T_CLOUDLIVE || id == IDC_T_CLOUDLIVEIMG) // chave liga/desliga
        {
            bool on = id == IDC_T_CLOUDLIVE ? cloudLive : id == IDC_T_CLOUDLIVEIMG ? cloudLiveImg : id == IDC_T_DATEKEY ? cfgDateKey : id == IDC_T_CLOUDCHECK ? cloudCheck : id == IDC_T_DATADIR ? dataOn : id == IDC_T_IMAGES ? cfgImages : (id == IDC_T_STARTUP ? startupOn : (id == IDC_T_SAVEALL ? cfgSaveAll : cfgExpand));
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
            bool active = page == id - IDC_NAV || ((page == 4 || page == 5) && id == IDC_NAV + 2);
            fill = active ? A_SEL : (down ? A_DOWN : A_BG);
            border = active ? A_ACCENT : 0;
            color = active ? C_ACCENT : C_GRAY;
        }
        else if (id == IDC_SEG || id == IDC_SEG + 1 || id == IDC_SEG3)
        {
            bool active = page == (id == IDC_SEG ? 2 : id == IDC_SEG3 ? 5 : 4);
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
        int li = (int)d.itemID;
        if (li < 0 || li >= listView.Count) return;
        Clip c = listView[li];
        int i = clips.IndexOf(c); // número real da posição (a busca não muda os números)
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
            DrawTextAt(d.hDC, c.Empty ? "(posição livre — selecione e digite um texto para preencher)" : c.Text.Length == 0 ? "(vazio — digite o texto e clique em Salvar)" : Preview(c.Text, 90),
                hUiFont, c.Text.Length == 0 ? C_GRAY : C_TEXT, tx, y + Px(7), tr, y + Px(28), 0x8024);
            DrawTextAt(d.hDC, c.Empty ? "★ Fixado  ·  vaga guardada" : (c.Pinned ? "★ Fixado" : "Recente") + "  ·  " + c.Text.Length + " caracteres",
                hSmallFont, c.Pinned ? C_ACCENT : C_GRAY, tx, y + Px(28), tr, y + h - Px(6), 0x8024);
        }

        if (dragging) // marca onde o clip arrastado vai entrar
        {
            RECT line = d.rcItem;
            if (li == dragTarget) { line.Bottom = line.Top + Px(3); FillRect(d.hDC, ref line, hAccentBrush); }
            else if (dragTarget >= clips.Count && li == clips.Count - 1) { line.Top = line.Bottom - Px(3); FillRect(d.hDC, ref line, hAccentBrush); }
        }
    }

    static void PaintManager(IntPtr hwnd)
    {
        PAINTSTRUCT ps;
        IntPtr hdc = BeginPaint(hwnd, out ps);
        DrawAppIcon(hdc, Px(18), Px(8), Px(16)); // nome do programa sempre visível
        DrawTextAt(hdc, "ClipAtalho", hUiBold, C_ACCENT, Px(40), Px(4), Px(336), Px(28), 0x24);
        DrawTextAt(hdc, PageTitles[page], hTitleFont, C_TEXT, Px(18), Px(22), Px(336), Px(52), 0x24);
        if (page == 0 || page == 1)
        {
            int sy = page == 0 ? 78 : 114;
            string sf = page == 0 ? clipFilter : xFilter;
            RoundRect(hdc, Px(16), Px(sy), Px(336), Px(30), Px(9), A_WHITE, sf.Length > 0 ? A_ACCENT : A_BTNBORDER);
            DrawTextAt(hdc, "\uE721", hIconFont, sf.Length > 0 ? C_ACCENT : C_GRAY, Px(22), Px(sy), Px(44), Px(sy + 30), 0x25);
            if (sf.Length == 0 && GetFocus() != (page == 0 ? hSearch : hXSearch))
                DrawTextAt(hdc, page == 0 ? "Pesquisar nos clips…   (Ctrl+F)" : "Pesquisar atalho ou texto…   (Ctrl+F)", hUiFont, C_GRAY, Px(48), Px(sy), Px(346), Px(sy + 30), 0x24);
            int shown = page == 0 ? listView.Count : xKeys.Count, total = page == 0 ? clips.Count : cfgAbbrOrder.Count;
            if (sf.Trim().Length > 0)
                DrawTextAt(hdc, shown == 0 ? "Nada encontrado" : shown + " de " + total, hSmallFont, shown == 0 ? C_RED : C_ACCENT, Px(16), Px(482), Px(352), Px(500), 0x24);
        }
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
                DrawTextAt(hdc, editDirty ? "Edição não salva — clique em Salvar para valer"
                    : (editClip != null && editClip.HasFormat ? "Clip com formatação (negrito, cores...)  ·  cola formatado" : "Texto do clip selecionado  ·  edite e clique em Salvar"),
                    hSmallFont, editDirty ? C_RED : C_GRAY, Px(376), Px(86), Px(726), Px(108), 0x8024);
        }
        else if (page == 1)
        {
            DrawTextAt(hdc, "Digite o atalho em qualquer programa e aperte Tab  ·  arraste para reordenar  ·  botão direito para mais opções" +
                (cfgExpand ? "" : "  ·  DESLIGADA (ligue em Configurações)"), hUiFont, cfgExpand ? C_GRAY : C_RED, Px(18), Px(50), Px(804), Px(72), 0x8024);
            RoundRect(hdc, Px(362), Px(78), Px(442), Px(400), Px(11), A_WHITE, A_BORDER);
            DrawTextAt(hdc, "Atalho  ·  o que você digita (letras, números, símbolos, espaço; ex.: bn1)", hSmallFont, C_GRAY, Px(376), Px(86), Px(726), Px(106), 0x8024);
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
            DrawTextAt(hdc, "Data:", hUiBold, C_TEXT, Px(36), Px(422), Px(92), Px(452), 0x24);
            DrawTextAt(hdc, "Tudo fica no ClipAtalho.ini (e as imagens na pasta \"ClipAtalho imagens\"). Backup e padrão de fábrica: \"Dados e backup\"; outro computador: \"Nuvem\".",
                hSmallFont, C_GRAY, Px(36), Px(496), Px(780), Px(518), 0x8024);
        }
        else if (page == 4)
        {
            DrawTextAt(hdc, "Backup, restauração e onde os dados ficam.", hUiFont, C_GRAY, Px(18), Px(50), Px(540), Px(72), 0x8024);
            RoundRect(hdc, Px(16), Px(78), Px(788), Px(444), Px(11), A_WHITE, A_BORDER);
            DrawTextAt(hdc, "Backup", hUiBold, C_TEXT, Px(36), Px(88), Px(780), Px(108), 0x24);
            DrawTextAt(hdc, "Listas em texto (.txt) que dá para abrir, editar e restaurar. O backup completo leva também as configurações (.ini).",
                hUiFont, C_GRAY, Px(36), Px(108), Px(780), Px(128), 0x8024);
            DrawTextAt(hdc, "Tamanho máximo da lista", hUiBold, C_TEXT, Px(36), Px(178), Px(530), Px(208), 0x24);
            DrawTextAt(hdc, "Limpeza automática  ·  os fixados (★) nunca saem", hUiBold, C_TEXT, Px(36), Px(214), Px(530), Px(244), 0x24);
            DrawTextAt(hdc, "Pasta de dados", hUiBold, C_TEXT, Px(36), Px(256), Px(780), Px(276), 0x24);
            string where = !dataOn ? "Desligada: os dados ficam na pasta do programa (" + exeDir + ")"
                : (dataMissing ? "INDISPONÍVEL agora: " + dataDir + "  (usando a pasta do programa)" : "Pasta atual: " + dataDir);
            DrawTextAt(hdc, where, hSmallFont, dataMissing ? C_RED : C_GRAY, Px(36), Px(310), Px(780), Px(330), 0x8024);
            DrawTextAt(hdc, "Se mudar em outro computador:", hUiFont, C_GRAY, Px(196), Px(334), Px(410), Px(366), 0x8024);
            DrawTextAt(hdc, "Padrão de fábrica", hUiBold, C_TEXT, Px(36), Px(380), Px(780), Px(400), 0x24);
            DrawTextAt(hdc, "Apaga clips, atalhos e configurações deste computador e volta tudo como na primeira instalação.",
                hUiFont, C_GRAY, Px(36), Px(398), Px(780), Px(418), 0x8024);
            DrawTextAt(hdc, "Na pasta de dados, os computadores que apontarem para ela usam os mesmos clips e atalhos. A escolha da pasta vale só para este computador.",
                hSmallFont, C_GRAY, Px(36), Px(468), Px(780), Px(512), 0x10);
        }
        else if (page == 5)
        {
            DrawTextAt(hdc, "Leve clips, atalhos e configurações para outro computador.", hUiFont, C_GRAY, Px(18), Px(50), Px(376), Px(72), 0x8024);
            RoundRect(hdc, Px(16), Px(78), Px(788), Px(444), Px(11), A_WHITE, A_BORDER);
            DrawTextAt(hdc, "Sincronizar com a nuvem", hUiBold, C_TEXT, Px(36), Px(88), Px(780), Px(108), 0x24);
            DrawTextAt(hdc, "Envie deste computador e baixe no outro. Vão juntos: clips, imagens, atalhos por expansão e configurações.",
                hUiFont, C_GRAY, Px(36), Px(108), Px(780), Px(128), 0x8024);
            string st = cloudProv == 0 ? "Escolha onde guardar: OneDrive (direto pela internet, sem instalar nada) ou uma pasta (Google Drive, Dropbox, rede)."
                : cloudProv == 1 ? (cloudRefresh != null ? "OneDrive conectado neste computador. Os arquivos ficam em OneDrive › Aplicativos › ClipAtalho."
                    : "OneDrive ainda não conectado neste computador: clique em \"Conectar ao OneDrive\".")
                : (Directory.Exists(cloudDir) ? "Pasta: " + cloudDir : "Pasta INDISPONÍVEL: " + cloudDir);
            DrawTextAt(hdc, st, hSmallFont, cloudProv == 2 && !Directory.Exists(cloudDir) ? C_RED : C_GRAY, Px(36), Px(172), Px(780), Px(196), 0x8024);
            string ci = cloudBusy ? cloudBusyText : cloudInfo != null ? "Na nuvem: " + InfoText(cloudInfo) : (CloudReady() ? "Na nuvem: (ainda não consultado)" : "");
            DrawTextAt(hdc, ci, cloudBusy ? hUiBold : hUiFont, cloudBusy ? C_ACCENT : C_TEXT, Px(36), Px(266), Px(780), Px(288), 0x8024);
            DrawTextAt(hdc, cloudLastSync.Length > 0 ? "Última sincronização deste computador: " + StampText(cloudLastSync) : "Este computador ainda não sincronizou.",
                hSmallFont, C_GRAY, Px(36), Px(290), Px(780), Px(312), 0x8024);
            DrawTextAt(hdc, "No OneDrive, o ClipAtalho só enxerga a própria pasta (Aplicativos › ClipAtalho), não os seus outros arquivos. O login é feito na página da Microsoft: a senha não passa pelo programa. Tempo real: com o OneDrive chega em uns 5 a 10 segundos; com pasta do Google Drive/Dropbox depende do aplicativo deles.",
                hSmallFont, C_GRAY, Px(36), Px(466), Px(780), Px(518), 0x10);
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
            if ((DateTime.Now - cloudLastCheck).TotalMinutes >= 5) CloudCheckAsync(); // outro computador enviou algo?
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
        ResetEditHist(hEdit);
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

    static List<Clip> listView = new List<Clip>(); // clips mostrados na lista (todos, ou só os que batem com a busca)
    static string clipFilter = "", xFilter = "", popFilter = "";

    // Sem acentos e em minúsculas: "acao" encontra "Ação".
    static string Fold(string s)
    {
        if (s == null) return "";
        string n = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder b = new StringBuilder(n.Length);
        foreach (char ch in n)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark) b.Append(ch);
        return b.ToString();
    }

    static bool ClipMatches(Clip c, string f)
    {
        if (f.Length == 0) return true;
        if (c.IsImage) return Fold("imagem").Contains(f);
        return Fold(c.Text).Contains(f);
    }

    static void RefreshList()
    {
        if (hwndMgr == IntPtr.Zero || !IsWindowVisible(hwndMgr)) return;
        string f = Fold(clipFilter.Trim());
        listView = clips.FindAll(delegate(Clip c) { return ClipMatches(c, f); });
        mgrLoading = true;
        SendMessageW(hList, 0x000B, IntPtr.Zero, IntPtr.Zero); // WM_SETREDRAW off (sem piscar)
        int top = SendMessageW(hList, 0x018E, IntPtr.Zero, IntPtr.Zero).ToInt32(); // LB_GETTOPINDEX
        SendMessageW(hList, 0x0184, IntPtr.Zero, IntPtr.Zero); // LB_RESETCONTENT
        for (int i = 0; i < listView.Count; i++)
            SendMessageStr(hList, 0x0180, IntPtr.Zero, ""); // LB_ADDSTRING (o desenho usa listView)
        if (mgrSel == null || !clips.Contains(mgrSel)) mgrSel = listView.Count > 0 ? listView[0] : (f.Length == 0 && clips.Count > 0 ? clips[0] : null);
        if (top > 0 && top < listView.Count) SendMessageW(hList, 0x0197, (IntPtr)top, IntPtr.Zero); // LB_SETTOPINDEX
        if (mgrSel != null) SendMessageW(hList, 0x0186, (IntPtr)listView.IndexOf(mgrSel), IntPtr.Zero); // LB_SETCURSEL
        SendMessageW(hList, 0x000B, (IntPtr)1, IntPtr.Zero);
        InvalidateRect(hList, IntPtr.Zero, true);
        mgrLoading = false;
        if (editClip != mgrSel) LoadEdit();
        else InvalidateRect(GetDlgItem(hwndMgr, IDC_PIN), IntPtr.Zero, true);
    }

    static void SelectIndex(int idx)
    {
        if (idx < 0 || idx >= listView.Count) return;
        mgrSel = listView[idx];
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
        return idx < listView.Count ? idx : -1;
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
                    if (editDirty && idx >= 0 && listView[idx] != editClip)
                    {
                        // pergunta antes de trocar de clip; o clique original é descartado
                        if (ResolveDirty(false)) SelectIndex(idx);
                        return IntPtr.Zero;
                    }
                    dragFrom = clipFilter.Trim().Length == 0 ? idx : -1; // com busca ativa não dá para arrastar
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
                    if (idx >= 0 && listView[idx] != mgrSel && ResolveDirty(false)) SelectIndex(idx);
                    return IntPtr.Zero;
                }
                case 0x0205: // WM_RBUTTONUP: menu do clip
                {
                    int idx = ItemAt(lParam);
                    if (idx < 0 || listView[idx] != mgrSel) return IntPtr.Zero;
                    List<MI> mi = new List<MI>();
                    mi.Add(Item(IDC_PIN, mgrSel.Pinned ? "☆  Soltar (deixar de fixar)" : "★  Fixar (vai para baixo dos fixados)"));
                    mi.Add(Item(IDC_UP, "↑  Subir"));
                    mi.Add(Item(IDC_DOWN, "↓  Descer"));
                    MI toExp = Item(IDC_TOEXP, "↪  Converter em expansão");
                    toExp.Off = mgrSel.IsImage || mgrSel.Empty || mgrSel.Text.Length == 0;
                    mi.Add(toExp);
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
            Clip target = (cur >= 0 && cur < listView.Count) ? listView[cur] : null;
            if (target != editClip && !ResolveDirty(false))
            {
                // continua editando: volta a seleção para o clip em edição
                SendMessageW(hList, 0x0186, (IntPtr)listView.IndexOf(mgrSel), IntPtr.Zero);
                return;
            }
            mgrSel = target;
            if (editClip != mgrSel) LoadEdit();
            return;
        }
        if (id == IDC_SEARCH || id == IDC_XSEARCH)
        {
            if (code == 0x0300) // EN_CHANGE: filtra a cada letra
            {
                if (id == IDC_SEARCH) { clipFilter = CtlText(hSearch); RefreshList(); }
                else { xFilter = CtlText(hXSearch); RefreshX(false); }
            }
            else if (code == 0x0200) { UpdateSearchBoxes(); RepaintAll(); } // EN_KILLFOCUS: vazio = volta a dica
            return;
        }
        if (id == IDC_EDIT)
        {
            if (code == 0x0300 && !mgrLoading) { SetDirty(true); RecordEdit(hEdit); } // EN_CHANGE
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
            convertFrom = null;
            LoadX(); // sem recriar a lista no meio do clique
            return;
        }
        if (id == IDC_XABBR || id == IDC_XTEXT)
        {
            if (code == 0x0300 && !mgrLoading) { SetXDirty(true); RecordEdit(id == IDC_XABBR ? hXAbbr : hXText); } // EN_CHANGE
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
                if (idx >= 0) PinToEnd(mgrSel);
                break;
            case IDC_DEL:
                if (idx < 0) return;
                DeleteClipAsk(mgrSel);
                return;
            case IDC_NEW:
                if (!ResolveDirty(false)) return;
                idx = mgrSel == null ? -1 : clips.IndexOf(mgrSel);
                if (clips.Count >= MaxClips)
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
                convertFrom = null;
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
            case IDC_SEG3: ShowPage(5); return;
            case IDC_T_DATEKEY:
                SetIniValue("geral", "tecla_data", cfgDateKey ? "nao" : "sim");
                LoadConfig();
                RepaintAll();
                return;
            case IDC_DATEFMT: DateFmtMenu(); return;
            case IDC_YEARFMT: YearFmtMenu(); return;
            case IDC_HOURFMT: HourFmtMenu(); return;
            case IDC_CLOUDPROV: CloudProvMenu(); return;
            case IDC_CLOUDCONN: CloudConnButton(); return;
            case IDC_CLOUDUP: CloudUpload(true, false); return;
            case IDC_CLOUDDOWN: CloudDownload(false); return;
            case IDC_CLOUDMODE: CloudModeMenu(); return;
            case IDC_T_CLOUDCHECK:
                cloudCheck = !cloudCheck;
                SaveLocal();
                RepaintAll();
                return;
            case IDC_T_CLOUDLIVE:
                if (!cloudLive && !CloudReady()) { Info("Escolha e conecte a nuvem primeiro (OneDrive ou pasta)."); return; }
                cloudLive = !cloudLive;
                SaveLocal();
                StartLive();
                RepaintAll();
                return;
            case IDC_T_CLOUDLIVEIMG:
                cloudLiveImg = !cloudLiveImg;
                SaveLocal();
                RepaintAll();
                return;
            case IDC_BK_CLIPS: DoBackup(true, false); return;
            case IDC_BK_X: DoBackup(false, true); return;
            case IDC_BK_ALL: DoBackup(true, true, true); return;
            case IDC_RESTOREBK: DoRestore(); return;
            case IDC_AUTODEL: AutoDelMenu(); return;
            case IDC_LIMIT: LimitMenu(); return;
            case IDC_T_DATADIR: ToggleDataFolder(); return;
            case IDC_DATADIR: ChooseDataFolder(); return;
            case IDC_CONFLICT: ConflictMenu(); return;
            case IDC_FACTORY: FactoryReset(); return;
            case IDC_UNDO: case IDC_REDO: DoUndo(0, id == IDC_REDO); return;
            case IDC_XUNDO: case IDC_XREDO: DoUndo(1, id == IDC_XREDO); return;
            case IDC_TOEXP: ConvertToExpansion(mgrSel); return;
            case IDC_GOCLOUD: ShowPage(5); return;
            case IDC_CUNDO: case IDC_CREDO: DoUndo(2, id == IDC_CREDO); return;
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
                case 0x0201: // WM_LBUTTONDOWN: clique na caixa "Pesquisar…" abre o campo de busca
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF), my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    int by = page == 0 ? Px(78) : Px(114);
                    if ((page == 0 || page == 1) && mx >= Px(16) && mx < Px(352) && my >= by && my < by + Px(30)) OpenSearch();
                    break;
                }
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

    // Janelinha breve, no estilo da sugestão de expansão, que some sozinha.
    static void ShowNotice(string text)
    {
        if (hwndPopup == IntPtr.Zero) return;
        popAbbr = null;
        popClips = new List<Clip>();
        popNumbered = false;
        popLines = new List<string>();
        popLines.Add("ClipAtalho");
        popLines.Add(text);
        popMode = MODE_NOTICE;
        ShowPopup(2500);
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
        int x, y;
        PlaceNearText(w, h, out x, out y);
        FadeWindow(hwndPopup, true);
        SetWindowPos(hwndPopup, (IntPtr)(-1), x, y, w, h, 0x10 | 0x40); // topo, sem ativar
        RoundWindow(hwndPopup, w, h);
        PlaceShade(hwndPopup);
        InvalidateRect(hwndPopup, IntPtr.Zero, true);
        SetTimer(hwndMain, (IntPtr)TIMER_POPUP, ms, IntPtr.Zero);
    }

    [DllImport("oleacc.dll")]
    static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object obj);

    // Onde está o cursor de texto (a barrinha piscando) do programa em uso, em coordenadas da tela.
    static bool GetCaretRect(out RECT r)
    {
        r = new RECT();
        GUITHREADINFO gti = new GUITHREADINFO();
        gti.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
        bool got = GetGUIThreadInfo(0, ref gti);
        if (got && gti.hwndCaret != IntPtr.Zero && gti.rcCaret.Bottom - gti.rcCaret.Top > 0)
        {
            POINT a = new POINT(), b = new POINT();
            a.X = gti.rcCaret.Left; a.Y = gti.rcCaret.Top;
            b.X = gti.rcCaret.Right; b.Y = gti.rcCaret.Bottom;
            ClientToScreen(gti.hwndCaret, ref a);
            ClientToScreen(gti.hwndCaret, ref b);
            r.Left = a.X; r.Top = a.Y; r.Right = Math.Max(b.X, a.X + 1); r.Bottom = b.Y;
            return true;
        }
        // Navegadores e programas modernos: pergunta pela acessibilidade do Windows
        try
        {
            IntPtr target = got && gti.hwndFocus != IntPtr.Zero ? gti.hwndFocus : GetForegroundWindow();
            if (target == IntPtr.Zero) return false;
            Guid iid = new Guid("00020400-0000-0000-C000-000000000046"); // IDispatch
            object acc;
            if (AccessibleObjectFromWindow(target, 0xFFFFFFF8, ref iid, out acc) != 0 || acc == null) return false; // OBJID_CARET
            object[] args = { 0, 0, 0, 0, 0 };
            System.Reflection.ParameterModifier pm = new System.Reflection.ParameterModifier(5);
            for (int i = 0; i < 4; i++) pm[i] = true;
            acc.GetType().InvokeMember("accLocation", System.Reflection.BindingFlags.InvokeMethod, null, acc, args,
                new System.Reflection.ParameterModifier[] { pm }, null, null);
            int l = Convert.ToInt32(args[0]), t = Convert.ToInt32(args[1]), cw = Convert.ToInt32(args[2]), ch = Convert.ToInt32(args[3]);
            Marshal.ReleaseComObject(acc);
            if (ch <= 0 || (l == 0 && t == 0)) return false;
            r.Left = l; r.Top = t; r.Right = l + Math.Max(1, cw); r.Bottom = t + ch;
            return true;
        }
        catch { return false; }
    }

    // Abre logo abaixo da linha onde se escreve (ou logo acima, se não couber), sem cobri-la.
    static void PlaceNearText(int w, int h, out int x, out int y)
    {
        RECT line;
        if (!GetCaretRect(out line)) // sem cursor de texto conhecido: usa o ponteiro do mouse
        {
            POINT pt;
            GetCursorPos(out pt);
            line.Left = pt.X; line.Right = pt.X + 1;
            line.Top = pt.Y - Px(12); line.Bottom = pt.Y + Px(14);
        }
        POINT at = new POINT();
        at.X = line.Left;
        at.Y = line.Bottom;
        RECT work = new RECT();
        work.Left = 0; work.Top = 0; work.Right = GetSystemMetrics(0); work.Bottom = GetSystemMetrics(1);
        MONITORINFO mi = new MONITORINFO();
        mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        if (GetMonitorInfoW(MonitorFromPoint(at, 2), ref mi)) work = mi.rcWork;
        int gap = Px(8);
        x = line.Left - Px(24);
        if (line.Bottom + gap + h <= work.Bottom) y = line.Bottom + gap;          // embaixo da linha
        else if (line.Top - gap - h >= work.Top) y = line.Top - gap - h;          // em cima da linha
        else // não cabe nem em cima nem embaixo: ao lado do cursor
        {
            x = line.Right + Px(24);
            if (x + w > work.Right) x = line.Left - Px(24) - w;
            y = line.Top - h / 3;
        }
        if (x + w > work.Right) x = work.Right - w;
        if (x < work.Left) x = work.Left;
        if (y + h > work.Bottom) y = work.Bottom - h;
        if (y < work.Top) y = work.Top;
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
            if (!PopRowVisible(i)) continue;
            int top = PopRowTop(i), bottom = top + Px(POP_ROW);
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
            int num = clips.IndexOf(c) + 1; // número real (a busca não muda os números)
            DrawTextAt(mem, num.ToString(), f, num <= 9 ? C_ACCENT : C_GRAY, Px(16) + shift / 2, top, Px(46) + shift, bottom, 0x24);
            int tx = Px(46) + shift;
            if (c.IsImage) // miniatura leve, ampliando com o efeito
            {
                int bw = Px(48) + (int)(Px(16) * sc), bh = Px(POP_ROW) - Px(6) + (int)(Px(4) * sc);
                int by = top + (Px(POP_ROW) - bh) / 2;
                RoundRect(mem, tx, by, bw, bh, Px(5), A_BG, A_BORDER);
                DrawImageFit(mem, ThumbOf(c), tx + Px(2), by + Px(2), bw - Px(4), bh - Px(4));
                tx += bw + Px(8);
            }
            int right = hover ? w - Px(86) : w - Px(16);
            DrawTextAt(mem, popLines[i], f, C_TEXT, tx, top, right, bottom, 0x8024);
            if (hover) // estrela para fixar/soltar
            {
                if (popHoverStar) RoundRect(mem, w - Px(80), top + Px(3), Px(30), Px(POP_ROW) - Px(6), Px(6), A_DOWN, 0);
                DrawTextAt(mem, c.Pinned ? "\uE735" : "\uE734", hIconFont, (c.Pinned || popHoverStar) ? C_ACCENT : C_GRAY,
                    w - Px(80), top, w - Px(50), bottom, 0x25);
            }
            if (hover) // lixeira para excluir o item
            {
                bool armed = popTrashArmed == c;
                if (popHoverTrash || armed) RoundRect(mem, w - Px(46), top + Px(3), Px(30), Px(POP_ROW) - Px(6), Px(6), A_DOWN, 0);
                DrawTextAt(mem, armed ? "?" : "", armed ? hFontBold : hIconFont, (popHoverTrash || armed) ? C_RED : C_GRAY,
                    w - Px(46), top, w - Px(16), bottom, 0x25);
            }
        }
        if (popItemDrag && popDropLine >= 1) // onde o item arrastado vai entrar
        {
            int ly = PopRowTop(popDropLine) - Px(1);
            RoundRect(mem, Px(12), ly, w - Px(24), Px(3), Px(2), A_ACCENT, 0);
        }
        int total = popLines.Count - 1, vis = PopVisible();
        if (popNumbered && total > vis) // barrinha de rolagem
        {
            int trackTop = Px(8 + POP_ROW), track = vis * Px(POP_ROW);
            int th = Math.Max(Px(24), track * vis / total);
            int ty = trackTop + (track - th) * popScroll / Math.Max(1, total - vis);
            RoundRect(mem, w - Px(8), ty, Px(4), th, Px(2), A_BTNBORDER, 0);
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
                if (popFilter.Length > 0) PostMessageW(hwndMain, WM_POPFILTER, (IntPtr)(-2), IntPtr.Zero); // limpa a busca
                else PostMessageW(hwndMain, WM_HIDEPOPUP, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            if (vk == VK_BACK) { PostMessageW(hwndMain, WM_POPFILTER, (IntPtr)(-1), IntPtr.Zero); return true; }
            if (!(vk >= '0' && vk <= '9') && !(vk >= 0x60 && vk <= 0x69) && !(ctrlAltWin && !AltGrDown()))
            {
                if (IsDeadKey(vk)) return true; // acento: a busca ignora acentos
                char fc = CharFromKey(vk);
                if (fc != '\0' && !char.IsControl(fc)) { PostMessageW(hwndMain, WM_POPFILTER, (IntPtr)fc, IntPtr.Zero); return true; } // digitar pesquisa
            }
            int n = -1;
            if (vk >= '1' && vk <= '9') n = vk - '1'; // com Shift: cola sem formatação
            else if (vk >= 0x61 && vk <= 0x69) n = vk - 0x61;
            PostMessageW(hwndMain, WM_HIDEPOPUP, IntPtr.Zero, IntPtr.Zero);
            if (n >= 0 && !ctrlAltWin)
            {
                if (n < clips.Count) PasteClip(clips[n], shift);
                else PostMessageW(hwndMain, WM_POPFILTER, (IntPtr)(-9), IntPtr.Zero); // aviso de posição vazia
                return true;
            }
            return false;
        }

        if (popMode == MODE_NOTICE && !IsModifierVk(vk)) PostMessageW(hwndMain, WM_HIDEPOPUP, IntPtr.Zero, IntPtr.Zero);

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
        IntPtr fg = GetForegroundWindow();
        if (fg != lastFg) typed.Length = 0; // trocou de janela: recomeça (sem limite de tempo para terminar o atalho)
        lastFg = fg;
        lastKey = DateTime.UtcNow;

        if (ctrlAltWin && !AltGrDown()) { typed.Length = 0; return false; }
        if (vk == VK_BACK) { if (typed.Length > 0) typed.Length--; }
        else if (vk == 0x0D || vk == VK_TAB || vk == VK_ESCAPE || (vk >= 0x21 && vk <= 0x28) || vk == 0x2D || vk == 0x2E)
        { typed.Length = 0; return false; } // Enter, Tab, setas, Home/End, PgUp/PgDn, Insert, Delete: o cursor mudou de lugar
        else
        {
            char ch = CharFromKey(vk);
            if (ch == '\0') { if (!IsDeadKey(vk)) typed.Length = 0; return false; }
            typed.Append(char.ToLowerInvariant(ch));
        }
        if (typed.Length > 64) typed.Remove(0, typed.Length - 64);
        // O atalho vale quando o que foi digitado TERMINA com ele (apagar e redigitar a última letra também funciona)
        string t = typed.ToString(), best = null;
        foreach (string k in cfgAbbr.Keys)
        {
            if (k.Length > t.Length || (best != null && k.Length <= best.Length) || !t.EndsWith(k, StringComparison.Ordinal)) continue;
            int at = t.Length - k.Length;
            if (at > 0 && char.IsLetterOrDigit(t[at - 1]) && char.IsLetterOrDigit(k[0])) continue; // no meio de uma palavra não vale
            best = k;
        }
        if (best != null)
        {
            matchAbbr = best;
            PostMessageW(hwndMain, WM_SHOWPOPUP, IntPtr.Zero, IntPtr.Zero);
        }
        else if (popMode == MODE_SUGGEST) PostMessageW(hwndMain, WM_HIDEPOPUP, IntPtr.Zero, IntPtr.Zero);
        return false;
    }

    static string matchAbbr;
    static int lastScan;
    [DllImport(U)] static extern int ToUnicodeEx(uint vk, uint scan, byte[] state, StringBuilder buf, int cch, uint flags, IntPtr hkl);
    [DllImport(U)] static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport(U)] static extern uint MapVirtualKeyExW(uint code, uint type, IntPtr hkl);
    [DllImport(U)] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport(U)] static extern short GetKeyState(int vk);

    static IntPtr FgLayout()
    {
        uint pid;
        return GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), out pid));
    }

    static bool AltGrDown() { return KeyDown(VK_CONTROL) && KeyDown(VK_MENU) && !KeyDown(VK_LWIN) && !KeyDown(VK_RWIN); }

    static bool IsDeadKey(int vk) { return (MapVirtualKeyExW((uint)vk, 2, FgLayout()) & 0x80000000) != 0; }

    // Caractere que a tecla produz no layout do teclado em uso (ç, @, !, espaço...). '\0' se não for caractere.
    static char CharFromKey(int vk)
    {
        try
        {
            IntPtr hkl = FgLayout();
            if ((MapVirtualKeyExW((uint)vk, 2, hkl) & 0x80000000) != 0) return '\0'; // tecla de acento: não mexe (não atrapalha o á, ã...)
            byte[] st = new byte[256];
            if (KeyDown(VK_SHIFT)) { st[0x10] = 0x80; st[0xA0] = 0x80; }
            if ((GetKeyState(0x14) & 1) != 0) st[0x14] = 1; // Caps Lock
            if (AltGrDown()) { st[0x11] = 0x80; st[0x12] = 0x80; st[0xA2] = 0x80; st[0xA5] = 0x80; }
            StringBuilder sb = new StringBuilder(8);
            int n = ToUnicodeEx((uint)vk, (uint)lastScan, st, sb, 8, 4, hkl); // 4 = não altera o estado do teclado
            return n == 1 && !char.IsControl(sb[0]) ? sb[0] : '\0';
        }
        catch { return '\0'; }
    }

    static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            uint msg = (uint)wParam.ToInt64();
            if (nCode == 0 && !paused && (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN))
            {
                int vk = Marshal.ReadInt32(lParam);
                lastScan = Marshal.ReadInt32(lParam, 4);
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
                    if (id == 30) { HidePopup(); StartPaste(DateStr(DateTime.Now), 0); return IntPtr.Zero; } // Ctrl+Shift+D
                    bool plainKey = id > 20; // com Shift: sem formatação
                    if (id > 20) id -= 20;
                    else if (id > 10) id -= 10; // teclado numérico
                    Log("Atalho recebido: " + cfgModsLabel + "+" + (id == ID_LIST ? 0 : id) + " (clips na lista: " + clips.Count + ")");
                    if (id == ID_LIST)
                    {
                        if (popMode == MODE_HISTORY) HidePopup(); else ShowHistory();
                    }
                    else if (id >= 1 && id <= 9)
                    {
                        HidePopup();
                        if (id - 1 < clips.Count) PasteClip(clips[id - 1], plainKey);
                        else ShowNotice("Você ainda não salvou nada nesta posição.");
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
                        case TIMER_CLOUDASK: OnCloudAskTimer(); break;
                        case TIMER_ARM:
                            KillTimer(hwndMain, (IntPtr)TIMER_ARM);
                            popClearArmed = false;
                            popTrashArmed = null;
                            if (popMode == MODE_HISTORY) InvalidateRect(hwndPopup, IntPtr.Zero, false);
                            break;
                        case TIMER_CONFIG:
                            if (File.Exists(iniPath) && File.GetLastWriteTimeUtc(iniPath) != iniMod) OnIniChangedOutside();
                            RunAutoDelete();
                            AutoDailyBackup();
                            break;
                        case TIMER_LIVE: OnLiveTimer(); break;
                    }
                    return IntPtr.Zero;
                case WM_SHOWPOPUP:
                    string abbr = matchAbbr;
                    if (abbr == null) return IntPtr.Zero;
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
                case WM_CLOUDDONE:
                    return IntPtr.Zero;
                case WM_CLOUDCHECK:
                    OnCloudCheckResult();
                    return IntPtr.Zero;
                case WM_CLOUDLIVE:
                    OnLiveIncoming();
                    return IntPtr.Zero;
                case WM_CLOUDAUTH:
                    cloudAuthState = wParam.ToInt32();
                    if (hDlg != IntPtr.Zero && cloudAuthWaiting) dlgResult = 100;
                    return IntPtr.Zero;
                case WM_POPFILTER:
                {
                    int code = wParam.ToInt32();
                    if (code == -9) { ShowNotice("Você ainda não salvou nada nesta posição."); return IntPtr.Zero; }
                    if (popMode != MODE_HISTORY) return IntPtr.Zero;
                    if (code == -1) { if (popFilter.Length > 0) popFilter = popFilter.Substring(0, popFilter.Length - 1); }
                    else if (code == -2) popFilter = "";
                    else popFilter += (char)code;
                    popScroll = 0;
                    RefreshPopupList();
                    SetTimer(hwndMain, (IntPtr)TIMER_POPUP, 20000, IntPtr.Zero);
                    return IntPtr.Zero;
                }
                case WM_POPSCROLL:
                    if (popMode == MODE_HISTORY) PopupScroll(wParam.ToInt32());
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
                    else if (popMode == MODE_HISTORY && popNumbered && popFilter.Length == 0 && !OnPopupTrash(lParam) && !OnPopupStar(lParam))
                    {
                        int pl = PopupLineAt(lParam);
                        if (pl >= 1 && pl - 1 < popClips.Count) // pode virar arraste do item para outra posição
                        {
                            popPress = pl;
                            popItemDrag = false;
                            popPressY = y;
                            SetCapture(hwnd);
                        }
                    }
                    return IntPtr.Zero;
                }
                case 0x0205: // WM_RBUTTONUP: menu do item
                {
                    if (popMode != MODE_HISTORY || !popNumbered) return IntPtr.Zero;
                    int pl = PopupLineAt(lParam);
                    if (pl >= 1 && pl - 1 < popClips.Count) PopupMenu(popClips[pl - 1]);
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
                    if (popPress >= 1) // arrastando um item: mostra onde ele vai entrar
                    {
                        int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                        if (!popItemDrag && Math.Abs(my - popPressY) > Px(6)) popItemDrag = true;
                        if (popItemDrag)
                        {
                            int dl = (my - Px(8) + Px(POP_ROW) / 2) / Px(POP_ROW) + popScroll;
                            dl = Math.Max(1 + popScroll, Math.Min(Math.Min(popLines.Count, popScroll + PopVisible() + 1), dl));
                            if (dl != popDropLine) { popDropLine = dl; InvalidateRect(hwnd, IntPtr.Zero, false); }
                            return IntPtr.Zero;
                        }
                    }
                    int line = OnPopupClose(lParam) ? -2 : (OnPopupClear(lParam) ? -3 : PopupLineAt(lParam));
                    if (line == 0) line = -1;
                    bool trash = line >= 1 && popNumbered && OnPopupTrash(lParam);
                    bool star = line >= 1 && popNumbered && OnPopupStar(lParam);
                    if (line != popHover || trash != popHoverTrash || star != popHoverStar)
                    {
                        int old = popHover;
                        popHover = line;
                        popHoverTrash = trash;
                        popHoverStar = star;
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
                    if (popItemDrag) { popItemDrag = false; popDropLine = -1; InvalidateRect(hwnd, IntPtr.Zero, false); }
                    popPress = -1;
                    break;
                case WM_LBUTTONUP: // clique: cola o item (a janela não tira o foco do programa em uso)
                {
                    if (popDrag) // fim do arraste da janela (vale só enquanto ela está aberta)
                    {
                        popDrag = false;
                        ReleaseCapture();
                        SetTimer(hwndMain, (IntPtr)TIMER_POPUP, 20000, IntPtr.Zero);
                        return IntPtr.Zero;
                    }
                    if (popPress >= 1) // item pressionado: arrastou para outra posição ou só clicou
                    {
                        int from = popPress;
                        bool moved = popItemDrag;
                        popPress = -1;
                        popItemDrag = false;
                        ReleaseCapture();
                        if (moved)
                        {
                            int drop = popDropLine;
                            popDropLine = -1;
                            if (from - 1 < popClips.Count && drop >= 1)
                            {
                                int fi = clips.IndexOf(popClips[from - 1]);
                                int ti = drop - 1 < popClips.Count ? clips.IndexOf(popClips[drop - 1]) : clips.Count;
                                if (fi >= 0 && ti >= 0) { MoveClip(fi, ti); RefreshPopupList(); }
                            }
                            InvalidateRect(hwnd, IntPtr.Zero, false);
                            SetTimer(hwndMain, (IntPtr)TIMER_POPUP, 20000, IntPtr.Zero);
                            return IntPtr.Zero;
                        }
                    }
                    if (OnPopupClose(lParam)) { HidePopup(); return IntPtr.Zero; } // botão X
                    if (popNumbered && OnPopupClear(lParam)) { PopupClear(); return IntPtr.Zero; } // "Limpar"
                    int line = PopupLineAt(lParam);
                    if (line >= 1 && popNumbered && OnPopupTrash(lParam) && line - 1 < popClips.Count) { PopupTrash(popClips[line - 1]); return IntPtr.Zero; }
                    if (line >= 1 && popNumbered && OnPopupStar(lParam) && line - 1 < popClips.Count) { PopupPin(popClips[line - 1]); return IntPtr.Zero; }
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
                            PasteClip(popClips[line - 1], KeyDown(VK_SHIFT)); // Shift+clique: sem formatação
                        }
                    }
                    else if (mode == MODE_SUGGEST && abbr != null)
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

    static bool OnPopupStar(IntPtr lParam)
    {
        int x = (short)(lParam.ToInt64() & 0xFFFF);
        int w = Px(540);
        return x >= w - Px(82) && x < w - Px(48);
    }

    // Estrela: fixa ou solta o item na hora.
    // Fixar: o item sobe e fica logo abaixo do último fixado (ordem de chegada). Soltar: fica onde está.
    static void PinToEnd(Clip c)
    {
        if (c.Pinned) { c.Pinned = false; return; }
        clips.Remove(c);
        int last = clips.FindLastIndex(delegate(Clip x) { return x.Pinned; });
        clips.Insert(last + 1, c);
        c.Pinned = true;
    }

    static void PopupPin(Clip c)
    {
        PushClipUndo();
        PinToEnd(c);
        SaveClips();
        RefreshList();
        RefreshPopupList();
    }

    // Botão direito num item da lista rápida.
    static void PopupMenu(Clip c)
    {
        List<MI> mi = new List<MI>();
        mi.Add(Item(1, "Colar"));
        MI plain = Item(2, "Colar sem formatação");
        plain.Off = c.IsImage || !c.HasFormat;
        mi.Add(plain);
        mi.Add(Sep());
        mi.Add(Item(3, c.Pinned ? "☆  Soltar (deixar de fixar)" : "★  Fixar nesta posição"));
        mi.Add(Item(4, "↑  Subir"));
        mi.Add(Item(5, "↓  Descer"));
        mi.Add(Item(6, "⤒  Mover para o topo"));
        mi.Add(Item(7, "⤓  Mover para o fim"));
        MI conv = Item(9, "↪  Converter em expansão");
        conv.Off = c.IsImage || c.Empty || c.Text.Length == 0;
        mi.Add(conv);
        mi.Add(Sep());
        mi.Add(Item(8, "Excluir"));
        IntPtr fg = GetForegroundWindow(); // o programa onde a pessoa está escrevendo
        POINT pt;
        GetCursorPos(out pt);
        KillTimer(hwndMain, (IntPtr)TIMER_POPUP);
        int cmd = ShowMenu(mi, pt.X, pt.Y, fg);
        if (popMode == MODE_HISTORY) SetTimer(hwndMain, (IntPtr)TIMER_POPUP, 20000, IntPtr.Zero);
        int i = clips.IndexOf(c);
        if (cmd == 0 || i < 0) return;
        switch (cmd)
        {
            case 1: case 2:
                HidePopup();
                if (!paused) PasteClip(c, cmd == 2);
                return;
            case 3: PopupPin(c); return;
            case 4: if (i > 0) MoveClip(i, i - 1); break;
            case 5: if (i < clips.Count - 1) MoveClip(i, i + 2); break;
            case 6: MoveClip(i, 0); break;
            case 7: MoveClip(i, clips.Count); break;
            case 8:
                PopupTrash(c);
                return;
            case 9:
                ConvertToExpansion(c);
                return;
        }
        RefreshPopupList();
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
        popTrashArmed = null;
        if (c.Pinned && !c.Empty) { HidePopup(); DeleteClipAsk(c); return; } // fixado: pergunta como fica a numeração
        PushClipUndo();
        clips.Remove(c);
        SaveClips();
        RefreshList();
        RefreshPopupList();
    }

    // Excluir um clip. Se for fixado e houver itens abaixo, pergunta se puxa os de baixo ou guarda a vaga.
    static bool DeleteClipAsk(Clip c)
    {
        int i = clips.IndexOf(c);
        if (i < 0) return false;
        int r = 1;
        if (c.Pinned && !c.Empty && i < clips.Count - 1)
        {
            dlgEscResult = 3;
            dlgPrimary = 2;
            dlgRedNote = "Puxar para cima muda o número de todos os itens abaixo (o " + (i + 2) + " vira " + (i + 1) +
                ", e assim por diante). Se você já decorou as posições, vai precisar decorar de novo — e decorar as posições é a ideia principal do ClipAtalho.";
            r = AskDialog("Excluir clip fixado", "Excluir o clip da posição " + (i + 1) + ".\nOs itens de baixo sobem para ocupar o lugar, ou a posição fica guardada como vaga livre?",
                "Puxar para cima", "Manter posições", "Cancelar");
            dlgRedNote = "";
            if (r != 1 && r != 2) return false;
        }
        PushClipUndo();
        if (r == 2)
        {
            Clip vaga = new Clip("", true);
            vaga.Empty = true;
            clips[i] = vaga;
            mgrSel = vaga;
        }
        else
        {
            clips.RemoveAt(i);
            mgrSel = clips.Count == 0 ? null : clips[Math.Min(i, clips.Count - 1)];
        }
        editClip = null;
        if (clips.Count == 0) LoadEdit();
        SaveClips();
        RefreshList();
        RefreshPopupList();
        return true;
    }

    // Linha da lista rápida sob o ponteiro (0 = cabeçalho) ou -1.
    static int PopupLineAt(IntPtr lParam)
    {
        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        if (y < Px(8)) return -1;
        int v = (y - Px(8)) / Px(POP_ROW);
        if (v == 0) return 0;
        if (v > PopVisible()) return -1;
        int line = v + popScroll;
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
        hIconBig = CreateFontW(-Px(22), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe MDL2 Assets");
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
        fmtHtml = RegisterClipboardFormatW("HTML Format");
        fmtRtf = RegisterClipboardFormatW("Rich Text Format");
        msgTaskbar = RegisterWindowMessageW("TaskbarCreated");

        AddTray();
        LoadConfig();
        LoadClips();
        EnsureAbbrDates();
        EnsureDateAbbr();
        if (dataMissing) Balloon("ClipAtalho", "A pasta de dados não está acessível agora. Usando os dados da pasta do programa.");
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { } // TLS 1.2
        CloudCheckAsync(); // a nuvem tem uma versão mais nova (de outro computador)?
        AutoDailyBackup();
        StartLive();
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
            if (msg.message == 0x0100 && hwndMgr != IntPtr.Zero && hDlg == IntPtr.Zero && hMenuWnd == IntPtr.Zero &&
                (msg.hwnd == hwndMgr || GetParent(msg.hwnd) == hwndMgr))
            {
                int k = msg.wParam.ToInt32();
                if (k == 'F' && KeyDown(0x11) && (page == 0 || page == 1)) { OpenSearch(); continue; } // Ctrl+F
                if (k == 0x1B && (msg.hwnd == hSearch || msg.hwnd == hXSearch)) { ClearSearch(); continue; } // Esc limpa a busca
                if (k == 0x0D && (msg.hwnd == hSearch || msg.hwnd == hXSearch)) { SetFocus(msg.hwnd == hSearch ? hList : hXList); continue; }
            }
            if (msg.message == 0x0100 && hwndMgr != IntPtr.Zero && hDlg == IntPtr.Zero && hMenuWnd == IntPtr.Zero && KeyDown(0x11) &&
                (msg.wParam.ToInt32() == 'Z' || msg.wParam.ToInt32() == 'Y') && (msg.hwnd == hwndMgr || GetParent(msg.hwnd) == hwndMgr))
            {
                bool redo = msg.wParam.ToInt32() == 'Y' || KeyDown(0x10); // Ctrl+Y ou Ctrl+Shift+Z refaz
                IntPtr f = GetFocus(); // dentro de um texto em edição: desfaz a digitação, várias vezes
                bool typing = (f == hEdit && editDirty) || ((f == hXText || f == hXAbbr) && expDirty);
                if (typing) { EditUndo(f, redo); continue; }
                if (page != 3) DoUndo(page, redo);
                continue;
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
