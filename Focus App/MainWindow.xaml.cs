using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using Notification.Wpf;
using Ookii.Dialogs.Wpf;

namespace Focus_App;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly Timer _timer = new();
    private readonly ObservableCollection<string> todoItems = new();
    private int notFocusCount;
    private int defultSce = 20;

    public MainWindow()
    {
        InitializeComponent();
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        foreach (var s in new SaveLoad().LoadListBoxContentFromFile())
        {
            todoItems?.Add(s);
        }

        listBox.ItemsSource = todoItems;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SetStartup(true);
        e.Cancel = true;
        Hide();
        Sec.Text = defultSce.ToString();
        if (StartButton.IsEnabled)
        {
            ButtonStart_Click(default, default);
        }
    }

    private static void SetStartup(bool enable)
    {
        // 取得啟動資料夾的路徑
        var startupFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup));

        // 取得你的應用程式的執行檔路徑
        var appPath = GetApplicationPath();

        try
        {
            var focusAppLnk = "Focus App.lnk";
            if (enable)
            {
                // 將應用程式的快捷方式複製到啟動資料夾
                var shortcutPath = Path.Combine(startupFolderPath, focusAppLnk);
                CreateShortcut(shortcutPath, appPath);
            }
            else
            {
                // 刪除應用程式的快捷方式從啟動資料夾
                var shortcutPath = Path.Combine(startupFolderPath, focusAppLnk);
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
        }
        catch (Exception ex)
        {
            // 處理例外情況
            Console.WriteLine("發生錯誤：" + ex.Message);
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        // 快捷方式的名称（不需要文件扩展名）
        var shortcutName = "MyShortcut";

        // 使用 COM 组件来创建快捷方式
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        var shortcut = shell.CreateShortcut(shortcutPath);

        // 设置快捷方式的属性
        shortcut.TargetPath = targetPath; // 设置目标路径
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath); // 设置工作目录
        shortcut.Description = "My shortcut description"; // 设置描述

        // 保存快捷方式
        shortcut.Save();
    }

    private static string GetApplicationPath() =>
        // 取得當前應用程式的執行檔路徑
        Process.GetCurrentProcess().MainModule.FileName;

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        var openFile = OpenFile();
        if (!string.IsNullOrWhiteSpace(openFile))
        {
            todoItems.Add(openFile);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var deleteButton = (Button) sender;
        var item = deleteButton.DataContext as string;
        todoItems.Remove(item ?? "");
    }

    private string OpenFile()
    {
        var dlg = new VistaOpenFileDialog
        {
            Filter = "exe|*.exe"
        };
        if (!dlg.ShowDialog() ?? false)
        {
            return string.Empty;
        }

        return dlg.FileName;
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        // 在這裡寫顯示視窗的邏輯，比如顯示視窗並將焦點設定到它
        Show();
        Activate();
    }

    private void ButtonStart_Click(object sender, RoutedEventArgs e)
    {
        new SaveLoad().SaveListBoxContentToFile(todoItems.ToList());
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        Info.Text = "專注中";
        if (!int.TryParse(Sec.Text, out var sec))
        {
            sec = defultSce;
            Sec.Text = sec.ToString();
        }

        _timer.Interval = TimeSpan.FromSeconds(sec).TotalMilliseconds;
        _timer.AutoReset = true;
        _timer.Elapsed += CheckWindows;
        _timer.Start();
    }

    private void CheckWindows(object? sender, ElapsedEventArgs e) =>
        Dispatcher.Invoke(() =>
        {
            try
            {
                var path = new WindowsControl().GetPath();
                var firstOrDefault = todoItems.FirstOrDefault(x => string.Equals(x, path, StringComparison.CurrentCultureIgnoreCase));
                if (string.IsNullOrEmpty(firstOrDefault))
                {
                    var _notificationManager = new NotificationManager();

                    var content = new NotificationContent
                    {
                        Title = "你又分心瞜",
                        Message = $"這是你第{++notFocusCount}分心瞜",
                        Type = NotificationType.Notification,
                        CloseOnClick = true // Set true if u want close message when left mouse button click on message (base = true),
                    };
                    _notificationManager.Show(content);
                    new WindowsControl().HideWindwos();
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        });

    protected override void OnClosed(EventArgs e)
    {
        _timer?.Dispose();
        base.OnClosed(e);
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var regex = new Regex("[^0-9]+"); // 只允許數字
        e.Handled = regex.IsMatch(e.Text);
    }

    private void ButtonStop_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        _timer.Stop();
        _timer.Elapsed -= CheckWindows;
    }
}

internal class SaveLoad
{
    private readonly string _filePath = "ListBoxContent.json";

    public List<string> LoadListBoxContentFromFile()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            var listBoxItems = JsonConvert.DeserializeObject<List<string>>(json);
            return listBoxItems ?? new List<string>();
        }

        return new List<string>();
    }

    public void SaveListBoxContentToFile(List<string> data)
    {
        var json = JsonConvert.SerializeObject(data);

        File.WriteAllText(_filePath, json);
    }
}

internal class WindowsControl
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public string GetForegroundWindowsName()
    {
        var foregroundWindow = GetForegroundWindow();
        uint processId;
        GetWindowThreadProcessId(foregroundWindow, out processId);
        var process = Process.GetProcessById((int) processId);

        var processName = process.ProcessName;
        return processName;
    }

    public void HideWindwos()
    {
        var foregroundWindow = GetForegroundWindow();
        const int SW_MINIMIZE = 6;
        ShowWindow(foregroundWindow, SW_MINIMIZE);
        Console.WriteLine("窗口已最小化");
    }

    public string GetPath()
    {
        var foregroundWindow = GetForegroundWindow();

        // 获取窗口所属进程的ID
        GetWindowThreadProcessId(foregroundWindow, out var processId);

        // 获取进程对象
        var process = Process.GetProcessById((int) processId);

        // 获取进程的执行文件路径
        var exePath = process.MainModule.FileName;
        return exePath;
    }
}