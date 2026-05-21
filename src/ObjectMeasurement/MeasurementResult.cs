using OpenCvSharp;

namespace ObjectMeasurement;

/// <summary>
/// 1 オブジェクトの計測結果。
/// 幅 = 最小外接矩形の短辺、長さ = 長辺と定義する。
/// </summary>
public class MeasurementResult
{
    /// <summary>幅 [mm]</summary>
    public double WidthMm { get; init; }

    /// <summary>長さ [mm]</summary>
    public double LengthMm { get; init; }

    /// <summary>幅 [px]</summary>
    public double WidthPixels { get; init; }

    /// <summary>長さ [px]</summary>
    public double LengthPixels { get; init; }

    /// <summary>重心座標 [px]</summary>
    public Point2f Center { get; init; }

    /// <summary>最小外接矩形の傾き [度]（OpenCV 規約: -90 ～ 0）</summary>
    public double RotationAngleDeg { get; init; }

    /// <summary>輪郭の面積 [px²]</summary>
    public double AreaPixels { get; init; }

    /// <summary>アスペクト比（長さ / 幅）</summary>
    public double AspectRatio => WidthMm > 0 ? LengthMm / WidthMm : double.NaN;
}
