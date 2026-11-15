# CadCheck 1.1

[CadCheck11.zip をダウンロード](https://raw.githubusercontent.com/shimo8966/curve-drive-app/download-check11/CadCheck11.zip)

JW_CADの外部変形ファイル一覧を読み取る診断ツールです。1.0の起動時コンパイルでWindows標準DLLが二重参照されるCS1703が発生したため、Run.cmdは同梱Check.exeを直接起動するよう修正しました。開発者用のBuild.cmdは/noconfigと明示参照を使います。通常はRun.cmdだけを起動してください。

ZIPをすべて展開し、Run.cmdをダブルクリックします。JW_CADの「その他→外部変形」でファイル一覧を開き、BATを選ばず、CadCheckの黒い画面へ戻ってEnterを押します。同じフォルダーにできるCheck.txtを開発中のチャットでのみ共有してください。CurveDriveの入れ直しや再登録は不要です。

ファイルの選択や実行、CADへの入力、図面変更、ネットワーク送信は行いません。ファイル名、パス、入力値を記録せず、分類のみ記録します。公開ZIPにはソース、実行ファイル、起動/ビルドファイル、説明書、ハッシュだけを含み、実行後のCheck.txtは含みません。

C#5互換コンパイル、自己検査、起動時にコンパイルしないこと、明示参照、公開物の検査を実施済みです。Windows上の読み取り動作は実機で確認します。
