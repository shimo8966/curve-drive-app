# CadProbe / Jw_cadの自動連携を調べる読み取り専用ツール

[CadProbe.zipをダウンロード](https://raw.githubusercontent.com/shimo8966/curve-drive-app/download-probe/CadProbe.zip)

1. ZIPをすべて展開し、CadProbeフォルダーのRun.cmdを起動する。
2. Jw_cadで「その他→外部変形」を押し、BATのファイル選択画面を開く。
3. BATは選ばず、この黒い画面へ戻ってEnterを押す。
4. 同じフォルダーにできたProbe.txtを、開発中のチャットへ添付する。

この補助ツールはJw_winのメニューと画面項目を読むだけです。
キー入力、マウス操作、フォーカス変更、図面変更、ネットワーク送信はしません。
ファイル名・パス・編集欄の値を記録しないよう除外します。
既存CurveDrive v2.7は変更しません。

公開用ZIPはソース、実行ファイル、Run.cmd、説明書、ハッシュ一覧だけです。
実行後のProbe.txtは私的な調査資料であり、GitHubに公開しません。
C#5/.NET Framework互換コンパイル、読み取り専用APIとZIP内容の確認済み。
Windows上での画面項目読み取りはユーザーPCで確認が必要です。

ZIP SHA256: `ad87790d59e437fd6974d54b4e36bd4ef57c5021250786e4a98634958fd10aac`
