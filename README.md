# カーブ推進アプリ v2.5 試験版

修正版ZIPと操作手順の受け取り用ブランチです。mainとGitHub Pagesは変更していません。

## ダウンロード

[CurveDrive_v25.zip をダウンロード](https://raw.githubusercontent.com/shimo8966/curve-drive-app/download-v25/CurveDrive_v25.zip)

## Windows 11での開き方

1. 上のリンクを押してZIPを保存します。
2. Edgeで Ctrl + J を押し、CurveDrive_v25.zip の「フォルダーに表示」を押します。
3. ZIPを右クリックし、「すべて展開」→「展開」を押します。
4. 展開した中の CurveDrive_v25 フォルダーを開きます。
5. 旧版の黒い連携画面を閉じ、Open.cmd をダブルクリックします。
6. 起動後は Readme.txt の「短い試験」に従ってください。最初はテスト図面だけで確認します。

保存時に警告やエラーが出た場合は、その画面を開発者とのチャットに送って確認してください。

## 内容と検証

- HTMLアプリ、C#ソースと実行ファイル、BAT/CMD、説明書、試験データ、検査コードと結果を同梱。
- 自動検査90件/チェック合格、C#コンパイル成功。
- Windows CMD/Edge/.NET Framework実機およびJW_CADでの作図・座標精度は未検証。
- 現段階は「Enter送信→CADで外部変形を一回実行」。完全自動操作は未実装。
- 過去点の訂正は新しいDXFを作り、テスト図面で開き直して終点登録。

ZIP内の調査報告はZIP作成時点の記録です。その後、この受け取り用ブランチにZIPを追加しました。

## 照合用

ZIP SHA256: `4a33c5fafd65ee86196a360f553a9998dca4518232dfa4b3bfb50a46e1d9c8f4`

ZIPを手動で編集・再圧縮する必要はありません。
