# DiaWheelFix

Windows 版 [Dia](https://www.diabrowser.com/) ブラウザで、マウスホイールを回してもページがスクロールしない問題を解消するツールです。

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
- 動作を確認したバージョン：Dia 1.3.1.2

## ライセンス

[MIT](LICENSE)

---

# DiaWheelFix (English)

A tool that fixes an issue in the Windows version of the [Dia](https://www.diabrowser.com/) browser where pages don't scroll when you turn the mouse wheel.

> This is an unofficial tool. It is not affiliated with The Browser Company.

## Usage

1. Download `DiaWheelFix.exe` from [Releases](../../releases) and put it in any folder you like. Don't move it out of that folder afterwards.
2. Double-click `DiaWheelFix.exe` and click "Yes". It starts right away, and from then on it starts automatically when you sign in to Windows.

To stop using it, double-click `DiaWheelFix.exe` again and click "Yes". The tool stops, and automatic startup is turned off.

The exe is not signed, so a SmartScreen warning may appear right after you download it. If it does, click "More info" and then "Run anyway" to start it.

### Command-line use

For advanced users, the following commands are also available:

- `DiaWheelFix.exe --install`: Installs without showing a confirmation dialog
- `DiaWheelFix.exe --uninstall`: Uninstalls without showing a confirmation dialog

## Building it yourself

You can build it with only the C# compiler that comes with Windows (.NET Framework 4).

```bat
src\build.cmd
```

## Notes

- It depends on Dia's internal structure. A Dia update may stop it from working.
- Tested with: Dia 1.3.1.2
- The confirmation dialogs are in Japanese.

## License

[MIT](LICENSE)
