using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;

namespace FindCursor
{
    public partial class MainWindow : Window
    {
        // Windows API関数の定義
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        // デリゲートの定義
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        // その他の定数と変数の定義
        private const uint SPI_SETMOUSESONAR = 0x2029;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const string ConfigFileName = "config.xml";

        private Key selectedKey = Key.None;
        private bool isWaitingForKey = false;
        private Dictionary<Key, bool> keyStates = new Dictionary<Key, bool>();

        public MainWindow()
        {
            InitializeComponent();
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            this.Closing += new System.ComponentModel.CancelEventHandler(OnWindowClosing);
            LoadSettings();
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            UnhookWindowsHookEx(_hookID); // ウィンドウが閉じられるときにフックを解除
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            var config = new XElement("Config",
                new XElement("Control", CtrlCheckBox.IsChecked ?? false),
                new XElement("Shift", ShiftCheckBox.IsChecked ?? false),
                new XElement("Tab", TabCheckBox.IsChecked ?? false),
                new XElement("Space", SpaceCheckBox.IsChecked ?? false),
                new XElement("SelectedKey", selectedKey != Key.None ? selectedKey.ToString() : "None")
            );

            config.Save(ConfigFileName);
        }

        private void LoadSettings()
        {
            if (File.Exists(ConfigFileName))
            {
                var config = XElement.Load(ConfigFileName);
                CtrlCheckBox.IsChecked = config.Element("Control") != null && bool.Parse(config.Element("Control").Value);
                ShiftCheckBox.IsChecked = config.Element("Shift") != null && bool.Parse(config.Element("Shift").Value);
                TabCheckBox.IsChecked = config.Element("Tab") != null && bool.Parse(config.Element("Tab").Value);
                SpaceCheckBox.IsChecked = config.Element("Space") != null && bool.Parse(config.Element("Space").Value);

                if (config.Element("SelectedKey") != null)
                {
                    var keyString = config.Element("SelectedKey").Value;
                    if (keyString != "None")
                    {
                        selectedKey = (Key)Enum.Parse(typeof(Key), keyString, true);
                        SelectedKeyText.Text = $"Selected Key: {selectedKey}";
                    }
                    else
                    {
                        selectedKey = Key.None;
                        SelectedKeyText.Text = "No key selected";
                    }
                }
                else
                {
                    selectedKey = Key.None;
                    SelectedKeyText.Text = "No key selected";
                }
            }
        }

        private void SelectKey_Click(object sender, RoutedEventArgs e)
        {
            isWaitingForKey = true;
            SelectedKeyText.Text = "Press a key to select...";
        }

        private void ClearSelectedKey_Click(object sender, RoutedEventArgs e)
        {
            selectedKey = Key.None;
            SelectedKeyText.Text = "No key selected";
        }

        private bool IsAllKeysPressed()
        {
            bool ctrlPressed = CtrlCheckBox.IsChecked == true && keyStates.ContainsKey(Key.LeftCtrl) && keyStates[Key.LeftCtrl];
            bool shiftPressed = ShiftCheckBox.IsChecked == true && keyStates.ContainsKey(Key.LeftShift) && keyStates[Key.LeftShift];
            bool tabPressed = TabCheckBox.IsChecked == true && keyStates.ContainsKey(Key.Tab) && keyStates[Key.Tab];
            bool spacePressed = SpaceCheckBox.IsChecked == true && keyStates.ContainsKey(Key.Space) && keyStates[Key.Space];
            bool selectedKeyPressed = selectedKey != Key.None && keyStates.ContainsKey(selectedKey) && keyStates[selectedKey];

            return (CtrlCheckBox.IsChecked != true || ctrlPressed) &&
                   (ShiftCheckBox.IsChecked != true || shiftPressed) &&
                   (TabCheckBox.IsChecked != true || tabPressed) &&
                   (SpaceCheckBox.IsChecked != true || spacePressed) &&
                   (selectedKey == Key.None || selectedKeyPressed);
        }

        private void KeyCombinationPressed()
        {
            if (selectedKey != Key.None)
            {
                Debug.WriteLine("キーの組み合わせが押されました。");
                SystemParametersInfoW(SPI_SETMOUSESONAR, 0, (IntPtr)400, SPIF_UPDATEINIFILE);
            }
            else
            {
                Debug.WriteLine("単体のキーが押されました。");

                SystemParametersInfoW(SPI_SETMOUSESONAR, 0, (IntPtr)400, SPIF_UPDATEINIFILE);

            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    if (isWaitingForKey)
                    {
                        selectedKey = key;
                        SelectedKeyText.Text = $"選択されたキー: {selectedKey}";
                        isWaitingForKey = false;
                    }

                    keyStates[key] = true;
                    Debug.WriteLine($"キーが押されました: {key}");

                    if (IsAllKeysPressed())
                    {
                        KeyCombinationPressed();
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    keyStates[key] = false;
                    Debug.WriteLine($"キーが離されました: {key}");

                    if (!IsAllKeysPressed())
                    {
                        SystemParametersInfoW(SPI_SETMOUSESONAR, 0, (IntPtr)32, SPIF_UPDATEINIFILE);
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

    }
}
