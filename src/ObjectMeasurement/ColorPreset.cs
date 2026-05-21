namespace ObjectMeasurement;

/// <summary>
/// 対象物別の HSV 色域プリセット。
/// 新しい対象物を追加する場合は Presets ディクショナリにエントリを追加する。
/// </summary>
public static class ColorPreset
{
    /// <summary>
    /// プリセット名 → MeasurementConfig への適用アクションのマップ。
    /// </summary>
    private static readonly Dictionary<string, Action<MeasurementConfig>> Presets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ── オムライス（卵の黄色系） ──────────────────────────────────
            // 卵焼きの黄橙色。照明条件によって V の下限を下げると検出しやすい。
            ["omurice"] = cfg =>
            {
                cfg.PresetName        = "omurice";
                cfg.HMin              = 15;   // 黄みがかった橙
                cfg.HMax              = 35;
                cfg.SMin              = 60;   // 低彩度でも捉えるため低めに設定
                cfg.SMax              = 255;
                cfg.VMin              = 150;  // 暗い影領域は除外
                cfg.VMax              = 255;
                cfg.UseSecondaryRange = false;
                cfg.MorphKernelSize   = 25;   // 表面の凹凸を埋める大きめカーネル
                cfg.MinAreaPixels     = 5000;
            },

            // ── ミニトマト（赤系） ────────────────────────────────────────
            // 赤は HSV で H が 0 付近と 170～180 の両端にまたがるため 2 範囲を使用。
            ["tomato"] = cfg =>
            {
                cfg.PresetName        = "tomato";
                // 第1範囲：高 H 側の赤（170-180）
                cfg.HMin              = 170;
                cfg.HMax              = 180;
                cfg.SMin              = 140;
                cfg.SMax              = 255;
                cfg.VMin              = 60;
                cfg.VMax              = 255;
                // 第2範囲：低 H 側の赤（0-10）
                cfg.UseSecondaryRange = true;
                cfg.HMin2             = 0;
                cfg.HMax2             = 10;
                cfg.SMin2             = 140;
                cfg.SMax2             = 255;
                cfg.VMin2             = 60;
                cfg.VMax2             = 255;
                cfg.MorphKernelSize   = 7;
                cfg.MinAreaPixels     = 3000;
            },

            // ── 白い皿 ────────────────────────────────────────────────────
            // V を高く、S を低く絞ることで白系を検出。
            ["plate"] = cfg =>
            {
                cfg.PresetName        = "plate";
                cfg.HMin              = 0;
                cfg.HMax              = 179;
                cfg.SMin              = 0;
                cfg.SMax              = 40;
                cfg.VMin              = 200;
                cfg.VMax              = 255;
                cfg.UseSecondaryRange = false;
                cfg.MorphKernelSize   = 31;
                cfg.MinAreaPixels     = 10000;
            },
        };

    /// <summary>利用可能なプリセット名の一覧</summary>
    public static IEnumerable<string> AvailableNames => Presets.Keys;

    /// <summary>
    /// プリセットを config へ適用する。
    /// </summary>
    /// <exception cref="ArgumentException">不明なプリセット名の場合</exception>
    public static void Apply(string presetName, MeasurementConfig config)
    {
        if (!Presets.TryGetValue(presetName, out var apply))
            throw new ArgumentException(
                $"Unknown preset '{presetName}'. Available: {string.Join(", ", AvailableNames)}");
        apply(config);
    }

    /// <summary>プリセットが存在するか確認</summary>
    public static bool Exists(string name) => Presets.ContainsKey(name);
}
