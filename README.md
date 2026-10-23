# DiaWheelFix

Windows 版 [Dia](https://www.diabrowser.com/) ブラウザで、マウスホイールを回してもページがスクロールしない問題を直す小さなツールです。

> 非公式ツールです。The Browser Company とは関係ありません。

## 使い方

1. [Releases](../../releases) から `DiaWheelFix.exe` をダウンロードして、好きなフォルダに置きます。置いたあとは、そのフォルダから動かさないでください。
2. `DiaWheelFix.exe` をダブルクリックして「はい」を押します。すぐに動き始め、次からは Windows にサインインすると自動で起動します。

やめるときは、`DiaWheelFix.exe` をもう一度ダブルクリックして「はい」を押します。ツールが止まり、自動起動も解除されます。

署名のない exe なので、ダウンロードした直後は SmartScreen の警告が出ることがあります。そのときは「詳細情報」を押してから「実行」を押すと起動できます。

### コマンドでの操作

上級者向けに、次のコマンドも使えます。

- `DiaWheelFix.exe --install`：確認の画面を出さずにインストールします
- `DiaWheelFix.exe --uninstall`：確認の画面を出さずにアンインストールします

## 自分でビルドする

Windows に標準で入っている C# コンパイラ（.NET Framework 4）だけでビルドできます。

```bat
src\build.cmd
```

## 注意

- Dia の内部構造に依存しています。Dia のアップデートで動かなくなる可能性があります。
- 動作を確認したバージョン：Dia 1.3.0.299

## ライセンス

[MIT](LICENSE)
