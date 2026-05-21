# 解説ドキュメント — ObjectMeasurement 実装設計

---

## 1. オムライス領域の抽出方法と選択理由

### 採用手法：HSV 色空間による閾値処理 + モルフォロジー処理

```
BGR 画像 → HSV 変換 → inRange（色域マスク）→ Closing + Opening → 輪郭検出
```

#### なぜ HSV か

RGB/BGR のまま色抽出を行う場合、照明の明暗変化が R・G・B すべてのチャンネルに影響するため
「同じ色なのに閾値が合わない」という問題が起きやすい。

HSV では

| チャンネル | 役割 | 照明変化への影響 |
|---|---|---|
| H（色相） | 色の種類 | 小さい |
| S（彩度） | 色の鮮やかさ | 中程度 |
| V（明度） | 明るさ | 大きい（V のみに集約される） |

**「H と S で色を特定し、V で影や反射を除外する」** という直感的なチューニングが可能なため
HSV を採用した。

#### なぜモルフォロジー処理か

オムライスの表面には焦げ目・凹凸・反射光といった局所的な色変化があり、
閾値処理直後のマスクには **穴・途切れ・孤立ノイズ** が生じる。

| 処理 | 操作 | 目的 |
|---|---|---|
| Closing（膨張 → 収縮） | 先に実施 | 内部の穴・途切れを塞ぐ |
| Opening（収縮 → 膨張） | 後に実施 | 外周の突出ノイズを除去 |

Closing を先に行う順序が重要で、逆順だと穴が塞がる前にノイズ除去が走り
輪郭が欠けることがある。

---

## 2. 「幅」「長さ」の定義と測定方法

### 定義

> **幅（Width）** ＝ 最小外接矩形の**短辺**  
> **長さ（Length）** ＝ 最小外接矩形の**長辺**

### 使用 API

OpenCV の `cv2.minAreaRect`（OpenCvSharp: `Cv2.MinAreaRect`）は
輪郭点群に対して**回転を許容した最小面積の外接矩形**を返す。

```
                ┌──────────────────────┐
                │     RotatedRect      │
                │   ┌────────────┐     │
                │   │ オムライス │     │ ← 傾いた最小外接矩形
                │   └────────────┘     │
                └──────────────────────┘
                  short side = 幅
                  long  side = 長さ
```

軸平行な BoundingRect と比べ、傾いた物体でも実際の形状に近い幅・長さを取得できる。

### px → mm 変換

```
計測値 [mm] = ピクセル数 [px] × スケール [mm/px]
```

スケールは撮影条件（カメラ高さ・レンズ）に依存するため外部パラメータ化した。  
既知サイズの基準物（例: 定規）を同条件で撮影して実測値から逆算することが推奨される。

---

## 3. 実装上の工夫

### 3-1. 赤色（ミニトマト）の 2 色域対応

OpenCV の HSV では赤は **H ≒ 0** と **H ≒ 180** の両端にまたがる。
1 つの inRange だけでは片方しか捉えられないため、
2 つのマスクを `BitwiseOr` で合成する方式を採用した。

```csharp
Cv2.InRange(hsv, lower1, upper1, mask1);   // H: 170-180（高端）
Cv2.InRange(hsv, lower2, upper2, mask2);   // H:   0-10 （低端）
Cv2.BitwiseOr(mask1, mask2, mask);
```

`UseSecondaryRange` フラグと第2 HSV レンジを `MeasurementConfig` に追加し、
同一コードパスで任意の折り返し色にも対応できるようにした。

### 3-2. カーネルサイズの自動奇数化

楕円カーネルは奇数サイズでないと中心が定まらない。
偶数が入力された場合は `+1` して奇数に丸める処理を `ApplyMorphology` 内に組み込み、
呼び出し元がサイズを意識しなくてよい設計にした。

### 3-3. 複数オブジェクト対応

`FindContours` で得たすべての輪郭を面積フィルタ後に計測し、
**面積降順**でソートして返す。
`MaxObjects` パラメータで上位 N 件に絞ることも可能にした。

### 3-4. プリセット方式

`ColorPreset.cs` にディクショナリ形式でプリセットを定義し、
`MeasurementConfig` への適用を Action<T> で記述することで、
新しい対象物を追加する際に**既存コードを修正せず**エントリを追加するだけで済む（開放閉鎖原則）。

### 3-5. 中間画像の保存

`--save-intermediate` フラグを有効にすると 5 段階の中間画像が出力される。  
HSV・マスク・モルフォロジー・輪郭・結果の各ステップを目視確認できるため、
HSV パラメータのチューニング作業が大幅に効率化される。

---

## 4. 精度・汎用性・実装コストのトレードオフ

### 4-1. HSV 閾値 vs 機械学習（セグメンテーション）

| 観点 | HSV 閾値 | DL セグメンテーション |
|---|---|---|
| 精度 | 照明・個体差に弱い | 高い汎用性 |
| 実装コスト | 低い | モデル訓練・推論環境が必要 |
| パラメータ調整 | 直感的に可能 | 難しい |
| 依存ライブラリ | OpenCvSharp のみ | PyTorch / ONNX 等が追加で必要 |

今回の要件（単一色・既知背景・明確な色差）では HSV 閾値で十分な精度が期待でき、
実装コストも低いため HSV を採用した。

### 4-2. 最小外接矩形 vs 楕円フィッティング

オムライスは楕円形に近いため `FitEllipse` も候補だが、

- 輪郭点が 5 点未満だと計算不可
- MinAreaRect は常に矩形幅・長さとして解釈しやすい

という理由で `MinAreaRect` を採用した。
楕円の長軸・短軸として取得したい場合は `Cv2.FitEllipse` に差し替え可能。

### 4-3. スケール精度の限界

本実装はスケール（mm/px）が**既知かつ一定**であることを前提とする。  
透視投影歪み・レンズ歪みがある場合は、カメラキャリブレーション
（`Cv2.CalibrateCamera`）を組み合わせる必要がある。

---

## 5. AI ツールの活用（任意記載）

### 使用ツール

- **Claude (Anthropic)** — コード生成・設計レビュー

### 問いかけ内容

1. 「C# / OpenCvSharp で HSV 閾値による単色物体検出と最小外接矩形計測を実装してほしい」
2. 「赤（ミニトマト）の折り返し色域の扱い方は？」
3. 「Closing と Opening の順序についてベストプラクティスを教えてほしい」

### 妥当性確認

- **HSV 閾値値の確認**：OpenCV の HSV 仕様（H: 0–179, S/V: 0–255）を[公式ドキュメント](//docs.opencv.org/4.13.0/df/d9d/tutorial_py_colorspaces.html#:~:text=For%20HSV%2C%20hue%20range%20is%20[0%2C179]%2C%20saturation%20range%20is%20[0%2C255]%2C%20and%20value%20range%20is%20[0%2C255].%20Different%20software%20use%20different%20scales.%20So%20if%20you%20are%20comparing%20OpenCV%20values%20with%20them%2C%20you%20need%20to%20normalize%20these%20ranges.)と照合。
- **MinAreaRect の Size 解釈**：`Size.Width` と `Size.Height` の大小関係は保証されないことを
  OpenCvSharp の [API ドキュメント](//docs.opencv.org/4.13.0/d3/dc0/group__imgproc__shape.html#ga3d476a3417130ae5154aea421ca7ead9)および[ソース](../../../../shimat/opencvsharp/blob/main/src/OpenCvSharp/Modules/core/Struct/RotatedRect.cs)で確認し、`Math.Min/Max` で短辺・長辺を振り分けた。
- **Closing → Opening の順序**：[参考文献](//docs.opencv.org/4.13.0/d9/d61/tutorial_py_morphological_ops.html)で
  穴埋めには先に Closing が有効であることを確認した。
- **テストコード**：AI 出力のテストをそのまま使用せず、アサーション条件・境界値を手動でレビュー。
  特に `AspectRatio` が `WidthMm == 0` の時に `NaN` を返すべき仕様を[明示的にテストに追加](tests/ObjectMeasurement.Tests/ImageProcessorTests.cs#L142)した。
