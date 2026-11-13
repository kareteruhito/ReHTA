using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Data.Sqlite;

namespace ReHTA;

public partial class MainWindow : Window
{
    // .rehtaファイルが存在するディレクトリ
    private string baseDirectory = "";
    private string databasePath = "";
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

            // データベースファイルのセット
            databasePath = Path.ChangeExtension(path, ".db");

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
            
            object? result = null;

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

                case "readText":
                {
                    string filename =
                        root.GetProperty("filename")
                            .GetString() ?? "";

                    result = await ReadTextAsync(filename);
                    break;
                }

                case "sql":
                {
                    string query = root.GetProperty("query").GetString() ?? "";
                    JsonElement parameters = root.GetProperty("parameters");
                    result = ExecuteSql(query, parameters);
                    break;
                }

                case "ps":
                {
                    string script = root.GetProperty("script").GetString() ?? "";
                    result = await ExecutePowerShellAsync(script);
                    break;
                }

                default:
                    throw new InvalidOperationException(
                        "未対応のコマンドです。");

            }

            SendResponse(new
            {
                id,
                success = true,
                result
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
    private async Task<string> ReadTextAsync(string filename)
    {
        if (!Regex.IsMatch(
            filename,
            @"\A[A-Za-z0-9_-]{1,64}\.(txt|json)\z",
            RegexOptions.IgnoreCase))
        {
            throw new ArgumentException(
                "使用できないファイル名です。");
        }

        string path = Path.Combine(baseDirectory, filename);

        if (!File.Exists(path))
        {
            return "";
        }
        return await File.ReadAllTextAsync(path);
    }


    private object ExecuteSql(string sql, JsonElement parameters)
    {

        
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQLが空です。");

        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString());

        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (parameters.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("SQLパラメータは配列で指定してください。");

        int index = 0;
        foreach (var value in parameters.EnumerateArray())
        {
            command.Parameters.AddWithValue($"${index}", ConvertParameter(value));
            index++;
        }

        using var reader = command.ExecuteReader();
        if (reader.FieldCount > 0)
        {
            var rows = new List<Dictionary<string, object?>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    object? field = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    if (field is byte[] bytes)
                        field = Convert.ToBase64String(bytes);
                    row[reader.GetName(i)] = field;
                }
                rows.Add(row);
            }
            return rows;
        }

        reader.Close();
        using var changes = connection.CreateCommand();
        changes.CommandText = "SELECT changes()";
        return Convert.ToInt32(changes.ExecuteScalar());
    }

    private static object ConvertParameter(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number when value.TryGetInt64(out long i) => i,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => 1,
        JsonValueKind.False => 0,
        JsonValueKind.Null => DBNull.Value,
        _ => throw new ArgumentException("SQLパラメータには文字列・数値・真偽値・nullのみ指定できます。")
    };

    private async Task<object> ExecutePowerShellAsync(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            throw new ArgumentException("PowerShellコマンドが空です。");

        if (script.Length > 16_384)
            throw new ArgumentException("PowerShellコマンドが長すぎます。");

        // Windows PowerShell 5.1。文字列は引数の引用符を気にせず渡す。
        string setup =
            "$ProgressPreference = 'SilentlyContinue'; " +
            "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
            "$OutputEncoding = [Console]::OutputEncoding; ";
        string encoded = Convert.ToBase64String(
            Encoding.Unicode.GetBytes(setup + script));

        var info = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = baseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        info.ArgumentList.Add("-NoLogo");
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-EncodedCommand");
        info.ArgumentList.Add(encoded);

        using var process = new Process { StartInfo = info };
        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("PowerShellの実行が30秒を超えました。");
        }

        return new
        {
            exitCode = process.ExitCode,
            stdout = await stdout,
            stderr = await stderr
        };
    }
}