using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;

namespace Focus_App;

public partial class App : Application
{
    private const string UniqueMutexName = "FocusAppMutex";
    private const string UniquePipeName = "FocusAppPipe";
    private NamedPipeServerStream pipeServer;
    private Mutex singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        bool isNewInstance;
        singleInstanceMutex = new Mutex(true, UniqueMutexName, out isNewInstance);

        if (!isNewInstance)
        {
            // 如果不是第一個實例，則通知第一個實例並退出
            NotifyFirstInstance();
            Shutdown();
            return;
        }

        // 啟動命名管道伺服器
        StartNamedPipeServer();

        base.OnStartup(e);
    }

    private void NotifyFirstInstance()
    {
        try
        {
            using (var pipeClient = new NamedPipeClientStream(".", UniquePipeName, PipeDirection.Out))
            {
                pipeClient.Connect(200); // 嘗試連接200毫秒，如果無法連接則放棄
                if (pipeClient.IsConnected)
                {
                    using (var writer = new StreamWriter(pipeClient))
                    {
                        writer.WriteLine("ShowWindow");
                    }
                }
            }
        }
        catch
        {
            // 忽略連接失敗
        }
    }

    private void StartNamedPipeServer()
    {
        pipeServer = new NamedPipeServerStream(UniquePipeName, PipeDirection.In);

        // 開始監聽命名管道連線
        ThreadPool.QueueUserWorkItem(ListenForClient);
    }

    private void ListenForClient(object state)
    {
        try
        {
            pipeServer.WaitForConnection();

            // 有新實例連線，通知舊實例顯示視窗
            using (var reader = new StreamReader(pipeServer))
            {
                var message = reader.ReadLine();
                if (message == "ShowWindow")
                {
                    Current.Dispatcher.Invoke(() =>
                    {
                        if (MainWindow != null)
                        {
                            MainWindow.Show();
                            MainWindow.Activate();
                        }
                    });
                }
            }

            pipeServer?.Disconnect();
        }
        catch
        {
            // 處理例外狀況
        }
        finally
        {
            StartNamedPipeServer(); // 重新啟動管道伺服器等待下一個連線
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 釋放Mutex資源
        singleInstanceMutex.ReleaseMutex();
        singleInstanceMutex.Close();

        // 關閉命名管道伺服器
        pipeServer?.Close();

        base.OnExit(e);
    }
}