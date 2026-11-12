# ReHTA

**ReHTA** は、HTML・CSS・JavaScriptでWindows向けの小さなデスクトップアプリケーションを作るための実験的な実行環境です。

かつての **HTA（HTML Application）** を現代のWeb技術で再構築するイメージで、WPF・WebView2・C#を組み合わせています。

通常のHTMLとしてUIを記述し、`.rehta` ファイルをReHTAで開いて実行します。ブラウザだけでは扱いにくいローカルファイルの読み書きやSQLiteの操作を、C#側の機能として提供します。

現在の主なAPIは、**`readText()`、`writeText()`、`sql()`** の3つです。ReHTA上で動くメモ帳、SQLite CRUDサンプル、対話型JavaScriptコンソールなどを作成しています。

## 仕組み

```text
アプリ.rehta（HTML / CSS / JavaScript）
    │
    │ rehta.readText() / rehta.writeText() / rehta.sql()
    ▼
rehta.js（共通JavaScript API）
    │ WebView2のpostMessage()
    ▼
C# / WPF
    ├─ System.IO：テキストファイルの読み書き
    └─ Microsoft.Data.Sqlite：SQLの実行
         │
         ▼
    .rehtaと同じフォルダのファイル／対応する.db
```

C#の処理結果やエラーは、WebView2経由でJavaScriptに返します。WPFはウィンドウとOS側の処理を担当し、WebView2がHTMLの表示とJavaScriptの実行を担当します。`.rehta` にC#コードを埋め込む方式ではありません。

共通の通信処理は`rehta.js`に分離してあり、WebView2の初期化時に自動登録されます。`.rehta`側で`rehta.js`を読み込む必要はありません。

## 動作環境

- Windows 11
- .NET 10 SDK（ソースからビルド・実行する場合）
- Microsoft Edge WebView2 Runtime

NuGetパッケージとして`Microsoft.Web.WebView2`と`Microsoft.Data.Sqlite`を利用します。

## ビルドと実行

リポジトリを取得します。

```powershell
git clone https://github.com/kareteruhito/ReHTA.git
cd ReHTA
```

サンプルを指定して実行します。

```powershell
dotnet run --project src/ReHTA -- samples/hello.rehta
dotnet run --project src/ReHTA -- samples/memo.rehta
dotnet run --project src/ReHTA -- samples/crud.rehta
```

`.rehta`ファイルのパスはコマンドライン引数で渡します。現段階では、Windowsへの拡張子の関連付けや配布用インストーラーは用意していません。

## サンプル

### `hello.rehta`：ファイル書き込み

入力したテキストを`save.txt`へ保存します。

```javascript
await rehta.writeText("save.txt", "Hello ReHTA!");
```

保存先は`hello.rehta`と同じディレクトリです。

### `memo.rehta`：メモ帳

起動時に`memo.txt`を読み込み、テキストエリアに表示します。保存ボタンで編集内容を上書きします。

```javascript
const text = await rehta.readText("memo.txt");
await rehta.writeText("memo.txt", text);
```

`memo.txt`が存在しない場合、`readText()`は空文字列を返します。

### `crud.rehta`：SQLite CRUD

HTMLの入力欄とボタンから、メモの追加・一覧表示・変更・削除を行うサンプルです。

```powershell
dotnet run --project src/ReHTA -- samples/crud.rehta
```

初回起動時には`CREATE TABLE IF NOT EXISTS`で`memo`テーブルを用意します。SQLを初めて実行すると、`crud.rehta`と同じディレクトリに`crud.db`が作成されます。

```text
samples/
├── crud.rehta
└── crud.db   （SQLを利用したときに作成）
```

### 対話型JavaScriptコンソール

ReHTA上で動くJavaScriptの対話型実行環境も作成しています。コードを入力して実行し、Canvasへの描画やHTMLのDOM操作を試せます。

- **LIST**：記録したプログラムを表示・編集
- **RUN**：記録したJavaScriptを実行
- **NEW**：プログラム・入力欄・表示領域を初期化
- **SAVE / LOAD**：`program.txt`へ保存・読み込み（ファイル名は固定）
- **HELP**：操作説明とサンプル

Canvas・HTML・両方の表示モードを選べます。RUNは画面を自動消去しません。必要に応じてプログラム内で`cls()`や`htmlScreen.replaceChildren()`を実行します。

## JavaScript API

すべてのAPIは`Promise`を返します。失敗時は`Error`で拒否されるため、`try...catch`で処理できます。

### `rehta.writeText(filename, text)`

指定したテキストをファイルに保存します。既存ファイルは上書きします。成功時の戻り値は`undefined`です。

```javascript
await rehta.writeText("save.txt", "Hello ReHTA!");
```

### `rehta.readText(filename)`

テキストファイルを読み込み、文字列として返します。ファイルが存在しない場合は空文字列`""`を返します。

```javascript
const text = await rehta.readText("memo.txt");
```

### `rehta.sql(query, parameters = [])`

SQLiteにSQLを渡して実行します。パラメータは配列として渡せます。

```javascript
// テーブル作成
await rehta.sql(`
    CREATE TABLE IF NOT EXISTS memo (
        id INTEGER PRIMARY KEY,
        text TEXT NOT NULL
    )
`);

// CREATE（追加）
await rehta.sql(
    "INSERT INTO memo (text) VALUES ($0)",
    ["最初のメモ"]
);

// READ（取得）
const rows = await rehta.sql(
    "SELECT id, text FROM memo ORDER BY id DESC"
);

// UPDATE（変更）
await rehta.sql(
    "UPDATE memo SET text = $0 WHERE id = $1",
    ["変更したメモ", 1]
);

// DELETE（削除）
await rehta.sql(
    "DELETE FROM memo WHERE id = $0",
    [1]
);
```

SELECTなどの結果セットを返すSQLでは**オブジェクトの配列**、INSERT・UPDATE・DELETEなどでは**変更行数**を返す設計です。外部入力をSQLに渡す際は、文字列連結ではなくプレースホルダーを使ってください。

SQL実行では接続文字列やデータベースファイル名を指定する必要はありません。

- `sample.rehta` → `sample.db`
- `memo.rehta` → `memo.db`
- `crud.rehta` → `crud.db`

対応する`.db`ファイルは、`rehta.sql()`でSQLiteへの接続が行われるときに、存在しなければ作成されます。SQLを使わないアプリでは作成されません。

## ファイルの場所と制限

`readText()`と`writeText()`で指定できるファイル名は、英数字・アンダースコア・ハイフンからなる1～64文字の名前に`.txt`または`.json`の拡張子を付けたものです。サブディレクトリや絶対パスは指定できません。

ファイルの読み書き先は、実行中の`.rehta`と同じディレクトリです。書き込み権限のない場所では保存に失敗します。

SQLiteは`.rehta`と同じ名前の`.db`を利用しますが、SQL文そのものを受け付けるため、任意のSQLの危険性には注意が必要です。特に`ATTACH DATABASE`など、別ファイルを参照するSQLも存在します。

## 現在の実装上の制限

- `.rehta`ファイルをHTMLとして読み込み、WebView2の`NavigateToString()`で表示します。
- `NavigateToString()`に渡すHTML文字列には約2MBの上限があります。
- 相対パスで指定した外部CSS・JavaScript・画像などは、そのままでは読み込めません。
- ファイルAPIはテキスト専用です。バイナリファイル操作やディレクトリ操作には対応していません。
- SQLiteの処理はC#側で同期的に実行しています。JavaScriptのAPIは`Promise`形式ですが、大きなSQL処理による画面応答への影響には注意が必要です。
- JavaScriptコンソールの`eval()`は実行状態を引き継ぎます。無限ループ、タイマーの重複、グローバル変数の残存などに注意が必要です。
- `.rehta`の拡張子関連付けや配布用インストーラーは未実装です。

## セキュリティについて

**現在のReHTAは、開発者自身が作成した信頼できる`.rehta`ファイルで動作確認するための試作版です。**

ページ遷移制御やメッセージ送信元・フレームの検証は十分ではありません。`sql()`はデータベースへの変更を可能にし、`readText()`と`writeText()`もローカルファイルにアクセスします。第三者が作成した`.rehta`ファイルを安全に実行できるものとして扱わないでください。

## 開発の方針

最初から大きなアプリケーションフレームワークを作るのではなく、**HTMLとJavaScriptで書いた小さなアプリケーションをWindows上で簡単に実行できること**を目指します。

HTML・CSS・JavaScriptの標準機能で実現できることはできるだけそのまま利用し、ReHTA独自の機能は必要なものに絞って追加する方針です。

## 参考資料

- [ReHTA - GitHub](https://github.com/kareteruhito/ReHTA)
- [Microsoft Learn: WebView2をWPFアプリで使用する](https://learn.microsoft.com/ja-jp/microsoft-edge/webview2/get-started/wpf)
- [Microsoft Learn: WebView2でWebコンテンツとネイティブ側のコードを通信させる](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/communicate-btwn-web-native)
- [Microsoft Learn: Microsoft.Data.Sqlite](https://learn.microsoft.com/ja-jp/dotnet/standard/data/sqlite/)
