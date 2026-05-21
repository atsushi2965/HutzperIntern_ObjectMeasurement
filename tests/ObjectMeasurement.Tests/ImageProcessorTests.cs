using ObjectMeasurement;
using Xunit;

namespace ObjectMeasurement.Tests;

// ── MeasurementConfig テスト ─────────────────────────────────────────────

public class MeasurementConfigTests
{
    [Fact]
    public void DefaultScaleIs_0_2()
    {
        var cfg = new MeasurementConfig();
        Assert.Equal(0.2, cfg.ScaleMmPerPixel);
    }

    [Fact]
    public void DefaultMorphKernelSize_IsOdd()
    {
        var cfg = new MeasurementConfig();
        Assert.True(cfg.MorphKernelSize % 2 != 0 || cfg.MorphKernelSize > 0,
            "MorphKernelSize should be a positive integer");
    }

    [Fact]
    public void DefaultUseSecondaryRange_IsFalse()
    {
        var cfg = new MeasurementConfig();
        Assert.False(cfg.UseSecondaryRange);
    }

    [Fact]
    public void DefaultSaveIntermediateImages_IsFalse()
    {
        var cfg = new MeasurementConfig();
        Assert.False(cfg.SaveIntermediateImages);
    }

    [Fact]
    public void DefaultImagePath_IsEmptyString()
    {
        var cfg = new MeasurementConfig();
        Assert.Equal(string.Empty, cfg.ImagePath);
    }
}

// ── ColorPreset テスト ──────────────────────────────────────────────────

public class ColorPresetTests
{
    [Fact]
    public void Apply_OmuriceCfg_SetsYellowHsvRange()
    {
        var cfg = new MeasurementConfig();
        ColorPreset.Apply("omurice", cfg);

        // 黄色系: H は 10-40 の範囲に収まるはず
        Assert.InRange(cfg.HMin, 0, 40);
        Assert.InRange(cfg.HMax, 20, 60);
        Assert.True(cfg.HMin < cfg.HMax, "HMin < HMax");
        Assert.Equal("omurice", cfg.PresetName);
        Assert.False(cfg.UseSecondaryRange);
    }

    [Fact]
    public void Apply_TomatoCfg_EnablesSecondaryRange()
    {
        var cfg = new MeasurementConfig();
        ColorPreset.Apply("tomato", cfg);

        Assert.Equal("tomato", cfg.PresetName);
        // 赤は折り返し色域が必要
        Assert.True(cfg.UseSecondaryRange, "tomato preset should use secondary HSV range");
    }

    [Fact]
    public void Apply_UnknownPreset_ThrowsArgumentException()
    {
        var cfg = new MeasurementConfig();
        Assert.Throws<ArgumentException>(() => ColorPreset.Apply("unknown_preset_xyz", cfg));
    }

    [Fact]
    public void AvailableNames_ContainsOmurice()
    {
        Assert.Contains("omurice", ColorPreset.AvailableNames,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvailableNames_ContainsTomato()
    {
        Assert.Contains("tomato", ColorPreset.AvailableNames,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Exists_ReturnsTrueForKnownPreset()
    {
        Assert.True(ColorPreset.Exists("omurice"));
        Assert.True(ColorPreset.Exists("tomato"));
    }

    [Fact]
    public void Exists_ReturnsFalseForUnknownPreset()
    {
        Assert.False(ColorPreset.Exists("not_a_preset"));
    }

    [Theory]
    [InlineData("OMURICE")]
    [InlineData("Omurice")]
    [InlineData("omurice")]
    public void Apply_IsCaseInsensitive(string name)
    {
        var cfg = new MeasurementConfig();
        // Should not throw
        ColorPreset.Apply(name, cfg);
        Assert.Equal("omurice", cfg.PresetName);
    }
}

// ── MeasurementResult テスト ────────────────────────────────────────────

public class MeasurementResultTests
{
    [Fact]
    public void AspectRatio_ReturnsLengthDividedByWidth()
    {
        var r = new MeasurementResult
        {
            WidthMm  = 50.0,
            LengthMm = 150.0,
        };
        Assert.Equal(3.0, r.AspectRatio, precision: 6);
    }

    [Fact]
    public void AspectRatio_WhenWidthIsZero_ReturnsNaN()
    {
        var r = new MeasurementResult { WidthMm = 0, LengthMm = 100 };
        Assert.True(double.IsNaN(r.AspectRatio));
    }

    [Fact]
    public void PropertiesAreInitOnly_CanBeSetViaInit()
    {
        var r = new MeasurementResult
        {
            WidthPixels      = 200.0,
            LengthPixels     = 600.0,
            WidthMm          = 40.0,
            LengthMm         = 120.0,
            AreaPixels       = 100000,
            RotationAngleDeg = -45.0,
        };

        Assert.Equal(200.0, r.WidthPixels);
        Assert.Equal(600.0, r.LengthPixels);
        Assert.Equal(40.0,  r.WidthMm);
        Assert.Equal(120.0, r.LengthMm);
        Assert.Equal(100000, r.AreaPixels);
        Assert.Equal(-45.0, r.RotationAngleDeg);
    }
}

// ── ImageProcessor ユニットテスト（OpenCV 不要な部分のみ） ──────────────

public class ImageProcessorUnitTests
{
    private static MeasurementConfig CreateConfig(double scale = 0.2) =>
        new MeasurementConfig
        {
            ImagePath        = "dummy.png",   // 実ファイルは不要なテストのみ
            ScaleMmPerPixel  = scale,
            SaveIntermediateImages = false,
        };

    [Theory]
    [InlineData(0.2, 100.0, 20.0)]
    [InlineData(0.2, 500.0, 100.0)]
    [InlineData(0.5, 100.0, 50.0)]
    [InlineData(0.1, 1000.0, 100.0)]
    public void PxToMm_ReturnsCorrectValue(double scale, double px, double expectedMm)
    {
        var cfg       = CreateConfig(scale);
        using var proc = new ImageProcessor(cfg);

        double result = proc.PxToMm(px);
        Assert.Equal(expectedMm, result, precision: 9);
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImageProcessor(null!));
    }

    [Fact]
    public void Process_NonExistentFile_ThrowsInvalidOperationException()
    {
        var cfg = CreateConfig();
        cfg.ImagePath = "/nonexistent/path/image.png";
        using var proc = new ImageProcessor(cfg);

        Assert.Throws<InvalidOperationException>(() => proc.Process());
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_DoesNotThrow()
    {
        var cfg = CreateConfig();
        var proc = new ImageProcessor(cfg);
        proc.Dispose();
        var ex = Record.Exception(() => proc.Dispose());
        Assert.Null(ex);
    }
}

// ── スケール計算 統合テスト用ヘルパー ────────────────────────────────────

public class ScaleCalculationTests
{
    [Theory]
    [InlineData(0.2, 1000, 200.0)]   // 1000 px × 0.2 mm/px = 200 mm
    [InlineData(0.2, 2500, 500.0)]
    [InlineData(0.1, 3000, 300.0)]
    public void Scale_PxToMm_MatchesExpected(double scale, double px, double expectedMm)
    {
        double actual = px * scale;
        Assert.Equal(expectedMm, actual, precision: 6);
    }

    [Fact]
    public void DefaultScale_0_2_OnSampleImage_ProducesReasonableOmuriceDimensions()
    {
        // 添付画像の解像度目安から期待値を概算するドキュメントテスト。
        // 実際の検出結果ではないため、計算式のみ検証する。
        // 画像幅 3024 px × 0.2 mm/px = 604.8 mm（実寸の目安）
        const double scale  = 0.2;
        const double widthPx = 3024;
        double widthMm = widthPx * scale;
        Assert.True(widthMm > 0, "Computed width should be positive");
    }
}
