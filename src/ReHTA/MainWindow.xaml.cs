using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ReHTA;

public partial class MainWindow : Window
{
    private readonly string saveDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ReHTA",
            "Saves");

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(
        object sender, RoutedEventArgs e)
    {
        try
        {
            string[] args =
                Environment.GetCommandLineArgs();

            if (args.Length < 2)
            {
                MessageBox.Show(
                    "REHTAファイルを指定してください。");
                Close();
                return;
            }

            string path = Path.GetFullPath(args[1]);

            if (!string.Equals(
                Path.GetExtension(path),
                ".rehta",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    ".rehtaファイルを指定してください。");
            }

            string html =
                await File.ReadAllTextAsync(path);

            await WebView.EnsureCoreWebView2Async();

            WebView.CoreWebView2.WebMessageReceived
                += WebMessageReceived;

            WebView.CoreWebView2.NavigateToString(html);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ReHTA");
            Close();
        }
    }

    private async void WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        int id = 0;

        try
        {
            using var doc =
                JsonDocument.Parse(e.WebMessageAsJson);

            var root = doc.RootElement;

            id = root.GetProperty("id").GetInt32();

            string command =
                root.GetProperty("command").GetString() ?? "";

            if (command != "writeText")
                throw new InvalidOperationException(
                    "未対応のコマンドです。");

            string filename =
                root.GetProperty("filename").GetString() ?? "";

            string text =
                root.GetProperty("text").GetString() ?? "";

            // 単純なファイル名だけ許可する
            if (!Regex.IsMatch(
                filename,
                @"\A[A-Za-z0-9_-]{1,64}\.(txt|json)\z",
                RegexOptions.IgnoreCase))
            {
                throw new ArgumentException(
                    "使用できないファイル名です。");
            }

            Directory.CreateDirectory(saveDirectory);

            string path =
                Path.Combine(saveDirectory, filename);

            await File.WriteAllTextAsync(path, text);

            SendResponse(new
            {
                id,
                success = true
            });
        }
        catch (Exception ex)
        {
            SendResponse(new
            {
                id,
                success = false,
                error = ex.Message
            });
        }
    }

    private void SendResponse(object response)
    {
        string json = JsonSerializer.Serialize(response);

        WebView.CoreWebView2
            .PostWebMessageAsJson(json);
    }
}