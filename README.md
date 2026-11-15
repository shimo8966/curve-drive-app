# CadCheck 1.0

[CadCheck.zip をダウンロード](https://raw.githubusercontent.com/shimo8966/curve-drive-app/download-check/CadCheck.zip)

JW_CADの外部変形ファイル一覧を読み取る診断ツールです。ファイルの選択や実行、CADへの入力、図面変更、ネットワーク送信は行いません。既存のCurveDriveを入れ直す必要はありません。

ZIPを展開してRun.cmdを起動し、JW_CADの「その他→外部変形」でファイル一覧を開いてください。BATを選ばず、診断の黒い画面へ戻ってEnterを押すと、同じフォルダーにCheck.txtが作成されます。Check.txtは開発中のチャットでのみ共有してください。

Windows標準IAccessible APIと従来の宣言から、読み取り結果や例外の種類/HRESULTを比較します。ファイル名、パス、入力値は記録せず、セッション用ファイルかどうかの分類のみ記録します。実行後の診断結果は公開用ZIPに含みません。

C#5互換コンパイル、分類/名前省略/読み取りAPIの自己検査と公開物検査を実施済み。Windows上の読み取りは実機で確認します。
