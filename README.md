# ReHTA

**ReHTA** は、HTML・CSS・JavaScriptでWindows向けの小さなデスクトップアプリケーションを作るための実験的な実行環境です。

かつての **HTA（HTML Application）** を現代のWeb技術で再構築するイメージで、WPF・WebView2・C#を組み合わせています。

通常のHTMLとしてUIを記述し、`.rehta` ファイルをReHTAで開いて実行します。ブラウザのJavaScriptだけでは扱いにくいローカルファイルの読み書きを、C#側の機能として提供します。

現在は、**JavaScriptからテキストファイルを読み書きできるプロトタイプ**が動作しています。

## 仕組み

```text
memo.rehta (HTML / CSS / JavaScript)
    │
    │ rehta.readText(...) / rehta.writeText(...)
    ▼
rehta.js（共通JavaScript API）
    │ WebView2のpostMessage()
    ▼
C# / WPF
    │ File.ReadAllTextAsync() / File.WriteAllTextAsync()
    ▼
ローカルファイル（.rehtaと同じフォルダ）
    │
    └─ 読み込み結果やエラーをWebView2経由でJavaScriptへ返す
```

WPFはウィンドウとOS側の処理を担当し、WebView2がHTMLの表示とJavaScriptの実行を担当します。`.rehta` ファイルにC#コードを埋め込む方式ではありません。

共通の通信処理は `rehta.js` に分離してあり、WebView2の初期化時に自動登録されます。`.rehta` 側で `rehta.js` を読み込む必要はありません。

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

リポジトリのルートから実行します。

```powershell
dotnet run --project src/ReHTA -- samples/hello.rehta
```

読み書き両方を確認するメモ帳サンプルは、次のコマンドで起動します。

```powershell
dotnet run --project src/ReHTA -- samples/memo.rehta
```

`.rehta` ファイルのパスはコマンドライン引数で渡します。現段階では、Windowsへの拡張子の関連付けは設定していません。

## サンプル

### `hello.rehta`：書き込みの確認

テキスト入力欄と保存ボタンを表示します。保存ボタンを押すと、次のAPIを呼び出します。

```javascript
await rehta.writeText("save.txt", "Hello ReHTA!");
```

`hello.rehta` と同じディレクトリに `save.txt` を保存します。

```text
samples/
├── hello.rehta
└── save.txt
```

PowerShellから内容を確認できます。

```powershell
Get-Content samples/save.txt
```

### `memo.rehta`：簡単なメモ帳

起動時に `memo.txt` を読み込み、テキストエリアに表示します。保存ボタンを押すと、編集内容を上書き保存します。

```javascript
// 起動時に読み込み
const text = await rehta.readText("memo.txt");

// 保存ボタンクリック時に書き込み
await rehta.writeText("memo.txt", text);
```

実際のサンプルでは、テキストエリアの内容を読み書きします。`memo.txt` が存在しない初回起動時は、`readText()` が空文字列を返すため、新規メモとして始められます。

```text
samples/
├── memo.rehta
└── memo.txt
```

## 現在のAPI

### `rehta.writeText(filename, text)`

指定したファイル名にテキストを保存します。既にファイルがある場合は上書きします。

- `filename`：保存するファイル名
- `text`：保存する文字列
- 戻り値：`Promise`（成功時の値は `undefined`）
- 失敗時：`Error` で拒否

```javascript
try {
    await rehta.writeText("save.txt", "Hello ReHTA!");
    console.log("保存しました");
} catch (error) {
    console.error(error.message);
}
```

### `rehta.readText(filename)`

指定したテキストファイルを読み込みます。

- `filename`：読み込むファイル名
- 戻り値：`Promise<string>`（ファイルの内容）
- ファイルが存在しない場合：空文字列 `""` を返す
- その他の読み込み失敗時：`Error` で拒否

```javascript
try {
    const text = await rehta.readText("memo.txt");
    console.log(text);
} catch (error) {
    console.error(error.message);
}
```

### ファイル名と保存先

読み書きともに、ファイル名は **英数字・アンダースコア・ハイフンからなる1～64文字の名前**に、`.txt` または `.json` の拡張子を付けたものだけを受け付けます。

ディレクトリ名を含む相対パスや、任意の絶対パスは指定できません。読み書き先は **実行している `.rehta` ファイルと同じディレクトリ** です。ReHTAの実行ファイルの場所や、コマンドを実行したディレクトリには依存しません。

なお、`.rehta` ファイルを置いたフォルダに書き込み権限がない場合、保存に失敗します。

## 現在の実装と制限

- `.rehta` ファイルの内容をHTMLとして読み込み、WebView2の `NavigateToString()` で表示します。
- `NavigateToString()` で渡せるHTML文字列には約2MBの上限があります。
- 相対パスで指定した外部CSS・JavaScript・画像などは、そのままでは読み込めません。
- テキストファイルの読み書きは `readText()` と `writeText()` で行えますが、バイナリファイル操作やディレクトリ操作には対応していません。
- ファイル名と拡張子には前述の制限があります。
- `.rehta` の拡張子関連付けや配布用インストーラーは未実装です。
- データベース操作などは現時点では実装していません。

## セキュリティについて

**現在のReHTAは、開発者自身が作成した信頼できる `.rehta` ファイルで動作確認するための試作版です。**

ファイル名や書き込み先を制限していますが、それだけで安全な実行環境になるわけではありません。現在の試作コードのページ遷移制御や、メッセージの送信元・フレームの検証は十分ではありません。第三者が作成した `.rehta` ファイルを安全に実行できるものとして扱わないでください。

一般公開向けのランタイムにするには、ナビゲーションとフレームの適切な制御、メッセージの検証、必要に応じたアクセス権限の制限などを検討する必要があります。

## 開発の方針

最初から大きなアプリケーションフレームワークを作るのではなく、**HTMLとJavaScriptで書いた小さなアプリケーションを、Windows上で簡単に実行できること**を目指します。

HTML・CSS・JavaScriptの標準機能で実現できることは、できるだけそのまま利用します。ReHTA独自の機能は、必要なものに絞って追加する方針です。

## 参考資料

- [Microsoft Learn: WebView2をWPFアプリで使用する](https://learn.microsoft.com/ja-jp/microsoft-edge/webview2/get-started/wpf)
- [Microsoft Learn: WebView2でWebコンテンツとネイティブ側のコードを通信させる](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/communicate-btwn-web-native)
