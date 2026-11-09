# ReHTA

**ReHTA** は、HTML・CSS・JavaScriptでWindows向けの小さなデスクトップアプリケーションを作るための実験的な実行環境です。

かつての **HTA（HTML Application）** を現代のWeb技術で再び使うイメージで、WPF・WebView2・C#を組み合わせています。

通常のHTMLとしてUIを記述し、`.rehta` ファイルをReHTAで開いて実行します。ローカルファイルの書き込みなど、ブラウザのJavaScriptだけでは扱いにくい機能を、C#側から提供する構想です。

現在は、**JavaScriptからテキストファイルを書き込む最小プロトタイプ**が動作しています。

## 仕組み

```text
hello.rehta (HTML / CSS / JavaScript)
    │
    │ JavaScript: rehta.writeText(...)
    ▼
WebView2
    │ postMessage()
    ▼
C# / WPF
    │ File.WriteAllTextAsync()
    ▼
ローカルファイル
    │
    └─ 処理結果をWebView2経由でJavaScriptへ通知
```

WPFはウィンドウとOS側の処理を担当し、WebView2がHTMLの表示とJavaScriptの実行を担当します。`.rehta` にC#コードを埋め込む方式ではありません。

## 動作環境

- Windows 11
- .NET 10 SDK（ソースからビルド・実行する場合）
- Microsoft Edge WebView2 Runtime

WebView2のNuGetパッケージ `Microsoft.Web.WebView2` を利用しています。

## ビルドと実行

リポジトリを取得します。

```powershell
git clone https://github.com/<ユーザー名>/ReHTA.git
cd ReHTA
```

`<ユーザー名>` は実際のGitHubアカウント名に置き換えてください。

リポジトリのルートから、サンプルを指定して実行します。

```powershell
dotnet run --project src/ReHTA -- samples/hello.rehta
```

`.rehta` ファイルはコマンドライン引数で渡します。現段階では、Windowsへの拡張子の関連付けは実装していません。

## サンプルの使い方

`hello.rehta` を開くと、テキスト入力欄と保存ボタンが表示されます。

保存ボタンを押すと、JavaScriptが次のAPIを呼び出します。

```javascript
await rehta.writeText("save.txt", "Hello ReHTA!");
```

C#側がファイルへ書き込み、成功・失敗をJavaScriptへ返します。失敗した場合は `Promise` が拒否されるため、`try...catch` で扱えます。

```javascript
try {
    await rehta.writeText("save.txt", "Hello ReHTA!");
    console.log("保存しました");
} catch (error) {
    console.error(error.message);
}
```

実際の保存先は次のフォルダです。

```text
%LOCALAPPDATA%\ReHTA\Saves\save.txt
```

PowerShellから内容を確認できます。

```powershell
Get-Content "$env:LOCALAPPDATA\ReHTA\Saves\save.txt"
```

## 現在のAPI

### `rehta.writeText(filename, text)`

指定したファイル名にテキストを保存します。非同期で動作し、`Promise` を返します。

- `filename`：保存するファイル名
- `text`：保存する文字列
- 成功時：`Promise` が完了
- 失敗時：エラーメッセージを持つ `Error` で拒否

現段階では、ファイル名は英数字・アンダースコア・ハイフンによる1～64文字の名前に、`.txt` または `.json` の拡張子を付けたものだけを受け付けます。ディレクトリ名や任意の絶対パスは指定できません。

## 現在の実装と制限

- `.rehta` の内容をHTMLとして読み込み、WebView2の `NavigateToString()` で表示します。
- `NavigateToString()` で渡せるHTML文字列には約2MBの上限があります。
- 相対パスで指定した外部CSS・JavaScript・画像などは、そのままでは読み込めません。
- ファイルの読み込み用独自APIはありません。独自APIは書き込みのみです。
- 保存先は共通の `Saves` フォルダで、`.rehta` ファイルごとに分離されていません。
- `.rehta` の拡張子関連付けや配布用インストーラーは未実装です。

## セキュリティについて

**現在のReHTAは、開発者自身が作成した信頼できる `.rehta` ファイルで動作確認するための試作版です。**

ファイル名と保存先を制限していますが、それだけで安全な実行環境になるわけではありません。現在の試作コードは、ページ遷移やフレームからのメッセージを十分に制限・検証していません。第三者が作成した `.rehta` ファイルを安全に実行できるものとして扱わないでください。

一般公開向けのランタイムにするには、メッセージの送信元確認、ページ遷移の制御、アプリごとの保存領域の分離などが必要です。

## 開発の方針

最初から大きなアプリケーションフレームワークを作るのではなく、**HTMLとJavaScriptで書いた小さなアプリケーションを、Windows上で簡単に実行できること**を目指します。

まずはローカルファイルの書き込みという最小限の拡張機能から始めています。HTML・CSS・JavaScriptの標準機能で実現できることは、できるだけそのまま利用する方針です。

## 参考資料

- [Microsoft Learn: WebView2をWPFアプリで使用する](https://learn.microsoft.com/ja-jp/microsoft-edge/webview2/get-started/wpf)
- [Microsoft Learn: WebView2のWebメッセージ](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/communicate-btwn-web-native)
