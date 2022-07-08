﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using KeyAsio.Gui.Configuration;
using KeyAsio.Gui.Models;
using KeyAsio.Gui.Realtime;
using KeyAsio.Gui.Utils;
using KeyAsio.Gui.Windows;
using Microsoft.Extensions.Logging;
using NLog.Config;
using OsuRTDataProvider.Listen;
using OrtdpLogger = OsuRTDataProvider.Logger;
using OrtdpSetting = OsuRTDataProvider.Setting;

namespace KeyAsio.Gui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly ILogger Logger = LogUtils.GetLogger("Application");
    internal readonly RichTextBox RichTextBox = new();

    [STAThread]
    internal static void Main()
    {
        var mutex = new Mutex(true, "KeyAsio.Net", out bool createNew);
        if (!createNew)
        {
            var process = Process
                .GetProcessesByName(Process.GetCurrentProcess().ProcessName)
                .FirstOrDefault(k => k.Id != Environment.ProcessId && k.MainWindowHandle != IntPtr.Zero);
            if (process == null) return;
            ProcessUtils.ShowWindow(process.MainWindowHandle, ProcessUtils.SW_SHOW);
            ProcessUtils.SetForegroundWindow(process.MainWindowHandle);
            return;
        }

        using var _ = new EmbeddedSentryConfiguration(options =>
        {
            options.HttpProxy = HttpClient.DefaultProxy;
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
            options.DefaultTags.Add("os.detail", HardwareInformationHelper.GetOsInformation());
            options.DefaultTags.Add("processor", HardwareInformationHelper.GetProcessorInformation());
            options.DefaultTags.Add("total_memory", HardwareInformationHelper.GetPhysicalMemory());
        });

        try
        {
            LogUtils.LogToSentry(LogLevel.Information, "Application started.");
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        finally
        {
            mutex.ReleaseMutex();
            LogUtils.LogToSentry(LogLevel.Information, "Application stopped.");
        }
    }

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        Dispatcher.UnhandledException += Dispatcher_UnhandledException;

        ConfigurationItemFactory
            .Default
            .Targets
            .RegisterDefinition(nameof(RichTextBoxTarget), typeof(RichTextBoxTarget));
        var shared = SharedViewModel.Instance;
        var settings = ConfigurationFactory.GetConfiguration<AppSettings>();

        shared.Debugging = settings.Debugging;

        if (settings.RealtimeOptions.RealtimeMode)
        {
            OrtdpLogger.SetLoggerFactory(LogUtils.LoggerFactory);
            OrtdpSetting.DisableProcessNotFoundInformation = true;
            OrtdpSetting.ListenInterval = 3;
            var manager = new OsuListenerManager();
            manager.OnPlayerChanged += player => RealtimeModeManager.Instance.Username = player;
            manager.OnModsChanged += modsInfo => RealtimeModeManager.Instance.PlayMods = modsInfo.Mod;
            manager.OnComboChanged += combo => RealtimeModeManager.Instance.Combo = combo;
            manager.OnScoreChanged += score => RealtimeModeManager.Instance.Score = score;
            manager.OnPlayingTimeChanged += playTime => RealtimeModeManager.Instance.PlayTime = playTime;
            manager.OnBeatmapChanged += beatmap => RealtimeModeManager.Instance.Beatmap = beatmap;
            manager.OnStatusChanged += (pre, current) => RealtimeModeManager.Instance.OsuStatus = current;
            manager.Start();
            RealtimeModeManager.Instance.OsuListenerManager = manager;
        }

        var miClearAll = new MenuItem
        {
            Header = "_Clear All"
        };
        RichTextBox.ContextMenu = new ContextMenu
        {
            Items = { miClearAll }
        };
        RichTextBox.Document.Blocks.Clear();
        miClearAll.Click += miClearAll_Click;

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    private void miClearAll_Click(object sender, RoutedEventArgs e)
    {
        RichTextBox.Document.Blocks.Clear();
        //TextBox.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Unhandled Exception (Dispatcher): " + e.Exception.Message, true);
        e.Handled = true;
    }
}