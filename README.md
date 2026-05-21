# ObjectMeasurement

上面から撮影した**単一色の物体**を画像から抽出し、**幅**と**長さ**を mm 単位で計測して出力する C# / .NET 8 アプリケーションです。  
画像処理には [OpenCvSharp4](../../../../shimat/opencvsharp) を使用しています。

---

## 対応プリセット

| プリセット名 | 対象物 | 特記 |
|---|---|---|
| `omurice` | オムライス（卵・黄橙色系） | デフォルト |
| `tomato` | ミニトマト（赤系） | 赤は HSV が折り返すため 2 色域マスクを使用 |
| `plate` | 白い皿 | 高 V・低 S 条件 |
| `custom` | 任意 | HSV 値をすべて手動指定 |

---

## ビルド手順

### 前提条件

| ツール | バージョン |
|---|---|
| .NET SDK | 8.0 以上 |
| OS | Windows 10/11 / Ubuntu 20.04+ / macOS 12+ |

```bash
# リポジトリをクローン
git clone https://github.com/atsushi2965/HutzperIntern_ObjectMeasurement.git
cd HutzperIntern_ObjectMeasurement

# ビルド（Release）
dotnet build -c Release ObjectMeasurement.sln
```

NuGet パッケージ（OpenCvSharp4 など）は `dotnet build` 時に自動復元されます。

---

## 実行手順

### 基本実行

```bash
# プリセット omurice でオムライスを計測
dotnet run --project src/ObjectMeasurement -c Release -- -i photo.png -p omurice

# ミニトマトを計測
dotnet run --project src/ObjectMeasurement -c Release -- -i photo.png -p tomato

# 中間画像も保存（デバッグ用）
dotnet run --project src/ObjectMeasurement -c Release -- -i photo.png -p omurice --save-intermediate -o ./debug_output
```

### ビルド済みバイナリを直接実行する場合

```bash
dotnet publish src/ObjectMeasurement -c Release -o ./publish
./publish/ObjectMeasurement -i photo.png -p omurice
```

### 引数なし（対話入力モード）

```bash
dotnet run --project src/ObjectMeasurement -c Release
```
対話入力モードで起動し、プロンプトに従って各パラメータを入力します。

---

## コマンドライン引数仕様

| オプション | 省略形 | 説明 | デフォルト |
|---|---|---|---|
| `--image <path>` | `-i` | 入力画像ファイルパス（PNG / JPEG 等） | ※必須 |
| `--scale <value>` | `-s` | スケール [mm/pixel] | `0.2` |
| `--preset <name>` | `-p` | 色プリセット（omurice / tomato / plate / custom） | `omurice` |
| `--kernel <size>` | `-k` | モルフォロジーカーネルサイズ（奇数推奨） | `25` |
| `--minarea <px²>` | `-a` | 最小検出面積 [px²] | `5000` |
| `--maxobjects <n>` | `-n` | 最大検出数（0=無制限） | `0` |
| `--save-intermediate` | — | 中間画像を保存する | `false` |
| `--output <dir>` | `-o` | 中間画像の出力ディレクトリ（指定すると自動で save-intermediate ON） | `./output` |
| `--help` | `-h` | ヘルプを表示 | — |

### custom プリセット時の HSV 引数

| オプション | 説明 | 範囲 |
|---|---|---|
| `--hmin` / `--hmax` | 色相 下限・上限（第1範囲） | 0–179 |
| `--smin` / `--smax` | 彩度 下限・上限（第1範囲） | 0–255 |
| `--vmin` / `--vmax` | 明度 下限・上限（第1範囲） | 0–255 |
| `--use-secondary` | 第2色域を有効化 | — |
| `--hmin2` / `--hmax2` | 色相 下限・上限（第2範囲） | 0–179 |
| `--smin2` / `--smax2` | 彩度 下限・上限（第2範囲） | 0–255 |
| `--vmin2` / `--vmax2` | 明度 下限・上限（第2範囲） | 0–255 |

---

## 入力画像の指定方法

- PNG 形式を推奨（JPEG でも動作します）
- 画像は**真上から撮影**したものを想定
- `-i` オプション、または位置引数として指定可能

```bash
# -i オプション
ObjectMeasurement -i /path/to/image.png -p omurice

# 位置引数（-i と同等）
ObjectMeasurement /path/to/image.png -p omurice
```

---

## パラメータ推奨値

### スケール（`--scale`）

本設定の既定値は `0.2 mm/pixel`（添付サンプル画像準拠）です。  
実際の撮影条件が異なる場合は、**既知サイズの基準物（例: 定規）を撮影して算出**してください。

```
スケール [mm/px] = 基準物の実寸 [mm] / 基準物のピクセル数 [px]
```

### HSV カーネルサイズ（`--kernel`）

| 対象物 | 推奨値 | 理由 |
|---|---|---|
| オムライス | 25 | 表面に凹凸があり、大きめのカーネルで穴を埋める必要がある |
| ミニトマト | 11 | 小型オブジェクトなのでカーネルを小さくして輪郭精度を保つ |

---

## 中間画像の出力内容

`--save-intermediate`（または `-o <dir>`）を指定すると、以下のファイルが出力されます。

| ファイル名 | 内容 |
|---|---|
| `01_hsv.png` | HSV 色空間変換後の画像 |
| `02_mask_raw.png` | 閾値処理直後の二値マスク |
| `03_mask_morphed.png` | モルフォロジー処理後のマスク |
| `04_contours.png` | 検出輪郭を原画像に描画した画像 |
| `05_result.png` | 最小外接矩形と計測値を原画像に描画した画像 |

---

## テストの実行

```bash
dotnet test ObjectMeasurement.sln
```

---

## 出力例

```
=== ObjectMeasurement v1.0 ===

--- 実行設定 ---
  画像       : オムライス画像.png
  スケール   : 0.2 mm/px
  プリセット : omurice
  HSV 第1範囲: H[15-35] S[60-255] V[140-255]
  カーネル   : 25
  最小面積   : 5000 px²

--- 計測結果 ---
検出数: 1 件
────────────────────────────────────────────────────────────
オブジェクト #1
  幅 (Width)  :    79.55 mm  (   397.8 px)
  長さ(Length):   168.26 mm  (   841.3 px)
  面積        :     250554 px²
  重心        : (496.7, 666.4) px
  傾き        :   57.3°
  アスペクト比: 2.12
────────────────────────────────────────────────────────────
```

---

## ライセンス

MIT License
