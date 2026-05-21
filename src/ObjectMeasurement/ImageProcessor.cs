using OpenCvSharp;

namespace ObjectMeasurement;

/// <summary>
/// OpenCvSharp を用いた画像処理クラス。
/// 単色物体を抽出し、幅・長さを計測して返す。
///
/// 処理フロー:
///   1. 画像読み込み（BGR）
///   2. BGR → HSV 変換
///   3. HSV 閾値によるバイナリマスク生成（2 色域対応）
///   4. モルフォロジー処理（Closing → Opening）
///   5. 輪郭検出・面積フィルタ
///   6. 最小外接矩形で幅・長さを算出
///   7. px → mm 変換
/// </summary>
public class ImageProcessor : IDisposable
{
    private readonly MeasurementConfig _cfg;
    private bool _disposed;

    public ImageProcessor(MeasurementConfig config)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
    }

    // ── 公開メソッド ────────────────────────────────────────────────────

    /// <summary>
    /// 設定に従い画像を処理し、計測結果リストを返す。
    /// 面積の大きい順にソートして返す。
    /// </summary>
    public List<MeasurementResult> Process()
    {
        if (_cfg.SaveIntermediateImages)
            Directory.CreateDirectory(_cfg.OutputDir);

        using var src = Cv2.ImRead(_cfg.ImagePath, ImreadModes.Color);
        if (src.Empty())
            throw new InvalidOperationException($"Failed to load image: {_cfg.ImagePath}");

        // Step 1: BGR → HSV
        using var hsv = new Mat();
        Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
        SaveIntermediate(hsv, "01_hsv.png");

        // Step 2: マスク生成
        using var mask = BuildMask(hsv);
        SaveIntermediate(mask, "02_mask_raw.png");

        // Step 3: モルフォロジー処理
        using var morphed = ApplyMorphology(mask, _cfg.MorphKernelSize);
        SaveIntermediate(morphed, "03_mask_morphed.png");

        // Step 4: 輪郭検出
        Cv2.FindContours(
            morphed,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        // Step 5: 面積フィルタ & 計測
        var results = new List<MeasurementResult>();
        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area < _cfg.MinAreaPixels) continue;

            var result = Measure(contour, area);
            results.Add(result);
        }

        // 面積の大きい順にソート
        results.Sort((a, b) => b.AreaPixels.CompareTo(a.AreaPixels));

        // 最大検出数の制限
        if (_cfg.MaxObjects > 0 && results.Count > _cfg.MaxObjects)
            results = results.Take(_cfg.MaxObjects).ToList();

        // 中間画像: 輪郭 & 結果の可視化
        if (_cfg.SaveIntermediateImages)
            SaveVisualization(src, contours, results);

        return results;
    }

    // ── 内部メソッド ────────────────────────────────────────────────────

    /// <summary>
    /// HSV マスクを生成する。
    /// 第2色域が有効な場合（赤色など H が 0 と 180 をまたぐケース）は OR で合成する。
    /// </summary>
    private Mat BuildMask(Mat hsv)
    {
        var lower1 = new Scalar(_cfg.HMin, _cfg.SMin, _cfg.VMin);
        var upper1 = new Scalar(_cfg.HMax, _cfg.SMax, _cfg.VMax);

        var mask = new Mat();
        Cv2.InRange(hsv, lower1, upper1, mask);

        if (_cfg.UseSecondaryRange)
        {
            using var mask2 = new Mat();
            var lower2 = new Scalar(_cfg.HMin2, _cfg.SMin2, _cfg.VMin2);
            var upper2 = new Scalar(_cfg.HMax2, _cfg.SMax2, _cfg.VMax2);
            Cv2.InRange(hsv, lower2, upper2, mask2);
            Cv2.BitwiseOr(mask, mask2, mask);
        }

        return mask;
    }

    /// <summary>
    /// Closing（穴埋め）→ Opening（ノイズ除去）の順でモルフォロジー処理を適用する。
    /// Closing を先にすることで、対象物内部の小さな穴（テクスチャ由来）を塞ぎやすい。
    /// </summary>
    private static Mat ApplyMorphology(Mat mask, int kernelSize)
    {
        if (kernelSize % 2 == 0) kernelSize++;   // 奇数に丸める
        var size = new Size(kernelSize, kernelSize);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, size);

        var morphed = new Mat();
        Cv2.MorphologyEx(mask, morphed, MorphTypes.Close, kernel);
        Cv2.MorphologyEx(morphed, morphed, MorphTypes.Open, kernel);
        return morphed;
    }

    /// <summary>
    /// 1 輪郭に対して最小外接矩形を求め、幅（短辺）・長さ（長辺）を mm で返す。
    /// </summary>
    private MeasurementResult Measure(Point[] contour, double areaPx)
    {
        RotatedRect rect = Cv2.MinAreaRect(contour);

        double shortSidePx = Math.Min(rect.Size.Width, rect.Size.Height);
        double longSidePx  = Math.Max(rect.Size.Width, rect.Size.Height);

        return new MeasurementResult
        {
            WidthPixels      = shortSidePx,
            LengthPixels     = longSidePx,
            WidthMm          = PxToMm(shortSidePx),
            LengthMm         = PxToMm(longSidePx),
            Center           = rect.Center,
            RotationAngleDeg = rect.Angle,
            AreaPixels       = areaPx,
        };
    }

    /// <summary>ピクセル値を mm に変換する。</summary>
    public double PxToMm(double px) => px * _cfg.ScaleMmPerPixel;

    /// <summary>中間画像を OutputDir へ保存する。</summary>
    private void SaveIntermediate(Mat mat, string filename)
    {
        if (!_cfg.SaveIntermediateImages) return;
        string path = Path.Combine(_cfg.OutputDir, filename);
        Cv2.ImWrite(path, mat);
        Console.WriteLine($"  [intermediate] Saved: {path}");
    }

    /// <summary>輪郭と計測結果を原画像上に描画して保存する。</summary>
    private void SaveVisualization(Mat src, Point[][] contours, List<MeasurementResult> results)
    {
        // ── 輪郭画像 ──
        using var contourImg = src.Clone();
        for (int i = 0; i < contours.Length; i++)
        {
            if (Cv2.ContourArea(contours[i]) >= _cfg.MinAreaPixels)
                Cv2.DrawContours(contourImg, contours, i, Scalar.LimeGreen, 3);
        }
        SaveIntermediate(contourImg, "04_contours.png");

        // ── 結果画像 ──
        using var resultImg = src.Clone();
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var center = new Point((int)r.Center.X, (int)r.Center.Y);
            Cv2.Circle(resultImg, center, 6, Scalar.Red, -1);
            string label = $"#{i + 1} W:{r.WidthMm:F1} L:{r.LengthMm:F1} mm";
            Cv2.PutText(resultImg, label,
                new Point(center.X + 10, center.Y - 10),
                HersheyFonts.HersheySimplex, 1.0, Scalar.Yellow, 2);
        }
        SaveIntermediate(resultImg, "05_result.png");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
