# DiaWheelFix

Windows 版 [Dia](https://www.diabrowser.com/) ブラウザで、マウスホイールを回してもページがスクロールしない問題を直す小さなツールです。

> 非公式ツールです。The Browser Company とは関係ありません。

## 原因

Windows 版 Dia は、WinUI の窓の中にページ表示用の Chromium の窓（`Chrome_RenderWidgetHostHWND`）を埋め込んでいます。ホイールの合図（`WM_MOUSEWHEEL`）が外側の WinUI 層で止まり、ページまで届いていません。

## しくみ

1. ホイールの合図を、ページに届く前に受け取ります（低レベルマウスフック）。
2. マウスが Dia のページの上にあるときだけ、その合図をページの窓へ直接届けます。
3. それ以外の場所やアプリでは何もしません。

- インターネット通信なし、記録なし、管理者権限不要、キーボードは見ません。
- Ctrl＋ホイール（拡大・縮小）、Shift＋ホイール（横スクロール）にも対応しています。

## 使い方

1. [Releases](../../releases) から `DiaWheelFix.exe` をダウンロードし、好きなフォルダに置きます（あとで動かさないでください）。
2. `DiaWheelFix.exe` をダブルクリックして「はい」を押します。今すぐ動き始め、次回から Windows にサインインすると自動で動きます。

やめるときは、`DiaWheelFix.exe` をもう一度ダブルクリックして「はい」を押します。停止して、自動起動も解除されます。

署名なしの exe なので、ダウンロード直後は SmartScreen の警告が出ることがあります。そのときは「詳細情報」→「実行」で起動できます。

### コマンド（上級者向け）

- `DiaWheelFix.exe --install`：無言でインストール
- `DiaWheelFix.exe --uninstall`：無言でアンインストール

## 自分でビルドする

Windows 標準の C# コンパイラ（.NET Framework 4）だけで作れます。

```bat
src\build.cmd
```

## 注意

- Dia の内部構造に依存しています。Dia のアップデートで効かなくなる可能性があります。
- 確認済みバージョン：Dia 1.3.0.299

## ライセンス

[MIT](LICENSE)
