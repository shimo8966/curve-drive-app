# v2.9a 試験結果

既存のアプリを修正し、Windows上の本物のJW_CADを使った試験を追加した。
試験の図面・現場名・測定値はすべて架空データ。
提供された図面・ログ・画像は公開試験に含めていない。

## Windowsでの実行試験

環境はWindows Server 2022、.NET Framework、Microsoft Edge。
JW_CADは公式配布の8.25aと10.03.8。インストーラーのSHA256を検証した。
ZIP内のC#4ファイルとHTMLはWindows試験に使ったものとバイト単位で一致。
EXEもWindowsの.NET Frameworkでコンパイルしたもの。Open.cmdでは再ビルドする。

両バージョンで合格した内容：

1. 実際の外部変形一覧から、セッション専用BATだけを実行。
2. 最小化された一覧で旧方式の0x80020003を再現。修正方式では自動追加が成功。
3. Edgeの実測入力欄でEnterを押し、HTTP→C#→JW_CADの点・線追加まで実行。
4. 直線10.5m/左20mm、11m/右20mmを、CADから読み戻した実測円中心で照合。
5. 計画線100本が残り、同じ点を実行し直しても実測点が増えないことを照合。
6. JWWを上書き保存し、CADと連携プログラムを終了。両方を新しいプロセスで再起動。
7. 同じ登録・終点を保持し、Field_Runの一回選択後、本物の「再実行」ボタンで11.5m/左30mmを追加。
8. S10→左R100/15m→S8→右R80/12m→S5の50m計画で、11m/左20mm、40m/右20mm、49m/左30mmを自動追加。
9. 円中心から別に計算した座標と、CADの円中心・実測線の両端を0.05mmの許容差で照合。計画線100本も維持。

40m/右20mmの基準値はX=39.813536227702627m、Y=3.0405509370951309m。
偏差は進行方向に直角な向きで検査した。

実行記録：
https://github.com/shimo8966/curve-drive-app/actions/runs/43761769325

## 回帰試験

|検査|合格|
|---|---:|
|HTML構文・計算・入力・復元・詳細設定・出力|66/66|
|実EXEのHTTP・保存・排他・再起動・起動判定|34/34|
|BAT/CMDの文字コード・参照・引用・削除命令なし|10/10|
|Chromiumと実EXEの入力・送信・復元・隠したX/Yの保持|13/13|
|CAD座標・縮尺・用紙・計画基準・前点|15/15|
|起動元・対象画面・JWWタイトル・別対象拒否|17/17|
|一覧読取・対象再確認・一回だけの実行・例外|16/16|

合計171件/チェック合格。C#5互換コンパイルはMonoでも警告をエラー扱いにして成功。
Linuxでの検査と、WindowsでCADを実行した検査は別に記載した。

## 残る範囲

- 利用者のWindows 11、会社の設定、実際の現場図面では未検証。
- 初回の発進点は実際の図形から得た既知の点を試験入力として渡した。人による右クリック自体の自動試験ではない。
- v8の英語環境では新規保存名が文字化けするため、架空のJWWを用意して使用した。v8での上書き保存・再起動は実行したが、新規の名前を付けて保存は合格項目に含めない。
- 見えているEdgeでEnterを押し、作図後に次の延長入力欄へフォーカスが戻ることも両バージョンで確認した。利用者のWindows 11での画面配置・会社の設定は未検証。
- CADへの引渡しを描画成功と表示する機能にはしていない。試験ではCADから図形を読み戻して検証した。
- 既存の実測線の訂正・置換は未実装。訂正時は新しいDXFから別の作業図面を作る。

## 開発者用

node --test tests/frontend.cjs
python3 tests/bridge.py --exe Bridge.exe --mono /path/to/mono -v
python3 tests/batch.py
node tests/browser.cjs

C#はBridge.cs、AutoBridge.cs、CadContext.cs、CadNative.csを一緒にビルド。
/noconfig、C#5、System.dll、System.Core.dll、System.Web.Extensions.dllを明示する。
AutoTests、NativeTests、ListTestsは同じソースに各tests/*.csを加え、同名のエントリポイントを指定。
ブラウザ試験にはPlaywrightとCURVE_MONO_PATH、CURVE_CHROMIUM_PATHが必要。
固定ポートを使う検査は直列実行。試験コードは通常利用に不要。
