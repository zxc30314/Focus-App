using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private readonly ObservableCollection<string> todoItems = new();
    private readonly Timer _timer = new();
    private int notFocusCount;

    public MainWindow()
    {
        InitializeComponent();
        foreach (var s in new SaveLoad().LoadListBoxContentFromFile())
        {
            todoItems?.Add(s);
        }

        listBox.ItemsSource = todoItems;
    }

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

    private void ButtonStart_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        Info.Text = "專注中";
        _timer.Interval = TimeSpan.FromSeconds(int.Parse(Sec.Text)).TotalMilliseconds;
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
                var firstOrDefault = todoItems.FirstOrDefault(x => x == path);
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
        new SaveLoad().SaveListBoxContentToFile(todoItems.ToList());
        base.OnClosed(e);
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9]+"); // 只允許數字
        e.Handled = regex.IsMatch(e.Text);
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