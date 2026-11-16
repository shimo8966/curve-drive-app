# v3.0 検証結果

## Windows上の本物のJW_CADによる検証

Windows Server 2022、.NET Framework、見えているMicrosoft Edge、公式JW_CAD 8.25a・10.03.8を使用。
インストーラーのSHA256を照合し、提供された図面やログを使わず架空の図面だけで実施した。

両バージョンで以下を確認した。

- 画面の案内から、入力確認・図面保存・登録・設定用BAT作成・実際の発進点登録・自動ON・EnterによるCAD反映まで実行。
- Edge上で「直線」「左カーブ・R100m」「右カーブ・R80m」と区間境界の表示を確認。入力確認段階ではCADへ送信しない。
- 設定用ファイルの場所を表示するボタンで、WindowsのExplorerが起動。
- CADとBridgeの両方を終了・再起動し、画面案内によって同じ登録と前点から継続。再登録せず次の点を追加。
- 実測点の位置、次の入力欄へのフォーカス、二重作図の防止、計画線100本の保持。
- 旧MSAA既定動作の0x80020003を再現し、修正済みの一覧操作で自動追加が成功。
- 直線の10.5m/左20mm、11m/右20mm、11.5m/左30mm、案内による再開後の12m/右10mmをCADの出力で照合。
- 直線・左R100・直線・右R80・直線の計画で、11m/左20mm、40m/右20mm、49m/左30mmを照合。円中心から別に計算した座標と0.05mmの許容差で一致し、実測線の両端と計画線100本も確認。

実行記録：
https://github.com/shimo8966/curve-drive-app/actions/runs/43851298387
ZIPのC#4ファイルとapp.htmlはWindowsで実行したものと一致させ、実際に試験したWindowsビルドのEXEを同梱する。

## 構文・計算・通信・画面の検証

|検査|合格|
|---|---:|
|HTML構文、従来の計算・入出力、新しい線形表示・案内状態|92/92|
|実EXEのHTTP、保存、排他、起動判定、案内API|40/40|
|BAT/CMDの文字コード、参照、引用、削除しない命令|10/10|
|Chromiumと実EXEの従来の手動送信互換・出力・復元|13/13|
|新しい案内、440px幅、通信切断の回復、再読み込み、起動案内|15/15|
|CAD座標、縮尺、用紙、計画基準、前点|15/15|
|起動元、対象画面、JWWタイトル、別対象の拒否|17/17|
|一覧読取、対象の再確認、一回だけの実行、例外|16/16|

合計218件/チェック合格。C#5互換のコンパイルはMonoでも警告をエラー扱いにして成功。
新しい案内のブラウザ検証は、実際のC#サーバーを一時コピーで起動して実施した。
登録応答・準備応答を途中で失っても、同じセッションとBATを照合して再開することを確認。
従来の手動送信の検証はJWC_TEMP契約の互換検査であり、本物のCAD実行とは別に数えている。

## 検証の限界

- 利用者のWindows 11、会社の設定、現場の図面では未検証。
- Windowsの案内検証では、保存済みの架空JWWと、案内から出力したDXFの内容が同じことを照合した。開く・保存する確認画面は、その既知の図面で進めた。
- 初回の発進点はCADの実際の図形から得た既知の点を試験入力で渡した。人の右クリックそのものを自動化した検査ではない。
- v8の英語環境では新規保存名に文字化けがあるため、架空のJWWを用意して上書き保存・再起動を検査した。v8の「名前を付けて保存」の操作は合格項目に含めない。
- 古いBridgeの起動案内はHTMLの描画と操作を検証。ファイルURLの制限がある検証環境ではHTML内容を読み込んで検査した。会社PCの既定ブラウザー設定は未検証。
- CADへの引渡しを描画成功と表示する機能にはしていない。試験ではCADから図形を読み戻して検証した。
- 既存実測線の訂正・置換は未実装。訂正時は入力を保持し、新しい作業図面を作る。

## 開発者用

C#はBridge.cs、AutoBridge.cs、CadContext.cs、CadNative.csを一緒にビルド。
/noconfig、C#5、System.dll、System.Core.dll、System.Web.Extensions.dllを明示する。

node --test tests/frontend.cjs
python tests/bridge.py --exe Bridge.exe --mono /path/to/mono -v
python tests/batch.py
node tests/browser.cjs
node tests/guide.cjs

ブラウザ検証はPlaywright、CURVE_MONO_PATH、CURVE_CHROMIUM_PATHが必要。
CURVE_GUIDE_SHOTSを指定すると案内画面の画像を保存する。固定ポートを使う検査は直列実行。
AutoTests、NativeTests、ListTestsは同じソースに各tests/*.csを追加し、同名のエントリポイントを指定。
現場の通常利用にNode・Python・Monoは不要。Open.cmdはWindowsの.NET Frameworkを使う。
