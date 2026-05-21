namespace ObjectMeasurement;

/// <summary>
/// 計測処理のすべてのパラメータを保持するコンフィグクラス。
/// デフォルト値はオムライス（黄色系）向けに設定してある。
/// </summary>
public class MeasurementConfig
{
    // ─── 入出力 ────────────────────────────────────────────────────────
    /// <summary>入力画像ファイルパス</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>スケール [mm/pixel]（デフォルト 0.2 mm/px）</summary>
    public double ScaleMmPerPixel { get; set; } = 0.2;

    /// <summary>選択したプリセット名（表示用）</summary>
    public string PresetName { get; set; } = "omurice";

    // ─── HSV 色域（第1範囲） ────────────────────────────────────────────
    /// <summary>色相 下限 [0-179]</summary>
    public int HMin { get; set; } = 15;
    /// <summary>色相 上限 [0-179]</summary>
    public int HMax { get; set; } = 35;
    /// <summary>彩度 下限 [0-255]</summary>
    public int SMin { get; set; } = 60;
    /// <summary>彩度 上限 [0-255]</summary>
    public int SMax { get; set; } = 255;
    /// <summary>明度 下限 [0-255]</summary>
    public int VMin { get; set; } = 140;
    /// <summary>明度 上限 [0-255]</summary>
    public int VMax { get; set; } = 255;

    // ─── HSV 色域（第2範囲・赤など折り返しを持つ色向け） ─────────────────
    /// <summary>第2範囲を使用するか（赤色など H が 0 と 180 をまたぐ場合に true）</summary>
    public bool UseSecondaryRange { get; set; } = false;
    public int HMin2 { get; set; } = 0;
    public int HMax2 { get; set; } = 0;
    public int SMin2 { get; set; } = 0;
    public int SMax2 { get; set; } = 255;
    public int VMin2 { get; set; } = 0;
    public int VMax2 { get; set; } = 255;

    // ─── 形態学処理 ────────────────────────────────────────────────────
    /// <summary>モルフォロジー楕円カーネルサイズ（奇数推奨、大きいほど穴埋め・ノイズ除去が強い）</summary>
    public int MorphKernelSize { get; set; } = 25;

    // ─── 輪郭フィルタ ──────────────────────────────────────────────────
    /// <summary>検出対象とする最小面積 [px²]（小ノイズを除去）</summary>
    public double MinAreaPixels { get; set; } = 5000;

    /// <summary>最大検出オブジェクト数（0 = 無制限、面積上位 N 件を返す）</summary>
    public int MaxObjects { get; set; } = 0;

    // ─── 中間画像保存 ──────────────────────────────────────────────────
    /// <summary>中間画像を保存するか</summary>
    public bool SaveIntermediateImages { get; set; } = false;

    /// <summary>中間画像の出力ディレクトリ</summary>
    public string OutputDir { get; set; } = "./output";
}
