using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ReHTA;

public partial class MainWindow : Window
{
    // .rehtaファイルが存在するディレクトリ
    private string baseDirectory = "";
    private bool initialNavigation = true;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            // コマンドライン引数
            string[] args =
                Environment.GetCommandLineArgs();

            if (args.Length < 2)
            {
                throw new ArgumentException(
                    ".rehtaファイルを指定してください。");
            }

            // 絶対パスに変換
            string path = Path.GetFullPath(args[1]);

            // 拡張子を確認
            if (!string.Equals(
                Path.GetExtension(path),
                ".rehta",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    ".rehtaファイルを指定してください。");
            }

            // 基準ディレクトリ
            baseDirectory =
                Path.GetDirectoryName(path)!;

            // HTMLを読み込む
            string html =
                await File.ReadAllTextAsync(path);

            // WebView2初期化
            await WebView.EnsureCoreWebView2Async();

            // メッセージ受信イベント
            WebView.CoreWebView2.WebMessageReceived
                += WebMessageReceived;

            // 外部ページへの遷移を禁止
            WebView.CoreWebView2.NavigationStarting
                += NavigationStarting;

            // 共通JavaScriptを読み込む
            string scriptPath = Path.Combine(
                AppContext.BaseDirectory,
                "rehta.js"
            );

            string script =
                await File.ReadAllTextAsync(scriptPath);

            // HTMLの読み込み前に共通APIを登録
            await WebView.CoreWebView2
                .AddScriptToExecuteOnDocumentCreatedAsync(
                    script);

            // HTMLを表示
            WebView.CoreWebView2.NavigateToString(html);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "ReHTA 起動エラー"
            );

            Close();
        }
    }

    private void NavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (initialNavigation)
        {
            initialNavigation = false;
            return;
        }

        e.Cancel = true;
    }

    private async void WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        int id = 0;

        try
        {
            // 想定外の送信元からの要求は受け付けない
            if (!string.Equals(
                e.Source,
                "about:blank",
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using var doc =
                JsonDocument.Parse(e.WebMessageAsJson);

            var root = doc.RootElement;

            id = root.GetProperty("id").GetInt32();

            string command =
                root.GetProperty("command")
                    .GetString() ?? "";

            switch (command)
            {
                case "writeText":
                {
                    string filename =
                        root.GetProperty("filename")
                            .GetString() ?? "";

                    string text =
                        root.GetProperty("text")
                            .GetString() ?? "";

                    await WriteTextAsync(filename, text);
                    break;
                }

                default:
                    throw new InvalidOperationException(
                        "未対応のコマンドです。");
            }

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

    private async Task WriteTextAsync(
        string filename,
        string text)
    {
        // ファイル名の制限
        // ディレクトリ区切り文字は許可しない
        if (!Regex.IsMatch(
            filename,
            @"\A[A-Za-z0-9_-]{1,64}\.(txt|json)\z",
            RegexOptions.IgnoreCase))
        {
            throw new ArgumentException(
                "使用できないファイル名です。");
        }

        // .rehtaファイルと同じディレクトリ
        string outputPath = Path.Combine(
            baseDirectory,
            filename
        );

        // ファイル書き込み
        await File.WriteAllTextAsync(
            outputPath,
            text
        );
    }

    private void SendResponse(object response)
    {
        string json =
            JsonSerializer.Serialize(response);

        WebView.CoreWebView2
            .PostWebMessageAsJson(json);
    }
}