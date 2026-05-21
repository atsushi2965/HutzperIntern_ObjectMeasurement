using ObjectMeasurement;

/// <summary>
/// エントリポイント。コマンドライン引数または対話入力でパラメータを受け取り、
/// 画像処理・計測を実行してコンソールへ出力する。
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("=== ObjectMeasurement v1.0 ===");
        Console.WriteLine();

        try
        {
            MeasurementConfig config;

            if (args.Length == 0)
                config = RunInteractiveMode();
            else
                config = ParseArguments(args);

            // 画像ファイルの存在確認
            if (!File.Exists(config.ImagePath))
            {
                Console.Error.WriteLine($"[ERROR] Image file not found: {config.ImagePath}");
                return 1;
            }

            PrintConfig(config);

            using var processor = new ImageProcessor(config);
            var results = processor.Process();

            PrintResults(results);
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"[ERROR] Invalid argument: {ex.Message}");
            PrintUsage();
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            return 3;
        }
    }

    // ── コマンドライン引数パーサ ────────────────────────────────────────

    static MeasurementConfig ParseArguments(string[] args)
    {
        var cfg = new MeasurementConfig();
        bool presetApplied = false;

        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i].ToLowerInvariant();

            switch (key)
            {
                case "-h":
                case "--help":
                    PrintUsage();
                    Environment.Exit(0);
                    break;

                case "-i":
                case "--image":
                    cfg.ImagePath = RequireNext(args, ref i, key);
                    break;

                case "-s":
                case "--scale":
                    cfg.ScaleMmPerPixel = ParseDouble(RequireNext(args, ref i, key), key);
                    break;

                case "-p":
                case "--preset":
                    string preset = RequireNext(args, ref i, key);
                    ColorPreset.Apply(preset, cfg);
                    presetApplied = true;
                    break;

                case "--hmin":  cfg.HMin = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--hmax":  cfg.HMax = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--smin":  cfg.SMin = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--smax":  cfg.SMax = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--vmin":  cfg.VMin = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--vmax":  cfg.VMax = ParseInt(RequireNext(args, ref i, key), key); break;

                case "--hmin2": cfg.HMin2 = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--hmax2": cfg.HMax2 = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--smin2": cfg.SMin2 = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--smax2": cfg.SMax2 = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--vmin2": cfg.VMin2 = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--vmax2": cfg.VMax2 = ParseInt(RequireNext(args, ref i, key), key); break;
                case "--use-secondary":
                    cfg.UseSecondaryRange = true;
                    break;

                case "-k":
                case "--kernel":
                    cfg.MorphKernelSize = ParseInt(RequireNext(args, ref i, key), key);
                    break;

                case "-a":
                case "--minarea":
                    cfg.MinAreaPixels = ParseDouble(RequireNext(args, ref i, key), key);
                    break;

                case "-n":
                case "--maxobjects":
                    cfg.MaxObjects = ParseInt(RequireNext(args, ref i, key), key);
                    break;

                case "--save-intermediate":
                    cfg.SaveIntermediateImages = true;
                    break;

                case "-o":
                case "--output":
                    cfg.OutputDir = RequireNext(args, ref i, key);
                    cfg.SaveIntermediateImages = true;
                    break;

                default:
                    // 位置引数として画像パスを受け付ける（1 番目のみ）
                    if (!args[i].StartsWith('-') && string.IsNullOrEmpty(cfg.ImagePath))
                        cfg.ImagePath = args[i];
                    else
                        throw new ArgumentException($"Unknown option: {args[i]}");
                    break;
            }
        }

        // プリセット未指定の場合はデフォルト（omurice）を適用
        if (!presetApplied)
            ColorPreset.Apply("omurice", cfg);

        if (string.IsNullOrEmpty(cfg.ImagePath))
            throw new ArgumentException("Image path is required. Use -i <path> or provide as positional argument.");

        return cfg;
    }

    // ── 対話入力モード ──────────────────────────────────────────────────

    static MeasurementConfig RunInteractiveMode()
    {
        Console.WriteLine("--- Interactive Mode ---");
        Console.WriteLine("（引数なしで起動したため、対話入力モードで設定します）");
        Console.WriteLine();

        var cfg = new MeasurementConfig();

        // 画像パス
        cfg.ImagePath = Prompt("入力画像のパスを入力してください: ");

        // スケール
        string scaleStr = Prompt($"スケール [mm/pixel]（デフォルト {cfg.ScaleMmPerPixel}）: ");
        if (!string.IsNullOrWhiteSpace(scaleStr))
            cfg.ScaleMmPerPixel = ParseDouble(scaleStr, "scale");

        // プリセット選択
        string presetList = string.Join(" / ", ColorPreset.AvailableNames);
        string presetStr  = Prompt($"プリセット [{presetList} / custom]（デフォルト omurice）: ");
        presetStr = string.IsNullOrWhiteSpace(presetStr) ? "omurice" : presetStr.Trim();

        if (presetStr.Equals("custom", StringComparison.OrdinalIgnoreCase))
        {
            cfg.PresetName = "custom";
            Console.WriteLine("HSV 範囲を入力してください（第1範囲）:");
            cfg.HMin = ParseInt(Prompt("  H下限 [0-179]: "), "HMin");
            cfg.HMax = ParseInt(Prompt("  H上限 [0-179]: "), "HMax");
            cfg.SMin = ParseInt(Prompt("  S下限 [0-255]: "), "SMin");
            cfg.SMax = ParseInt(Prompt("  S上限 [0-255]: "), "SMax");
            cfg.VMin = ParseInt(Prompt("  V下限 [0-255]: "), "VMin");
            cfg.VMax = ParseInt(Prompt("  V上限 [0-255]: "), "VMax");

            string sec = Prompt("第2色域を使用しますか？（赤など折り返しがある場合）[y/N]: ");
            if (sec.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                cfg.UseSecondaryRange = true;
                Console.WriteLine("HSV 範囲を入力してください（第2範囲）:");
                cfg.HMin2 = ParseInt(Prompt("  H下限2 [0-179]: "), "HMin2");
                cfg.HMax2 = ParseInt(Prompt("  H上限2 [0-179]: "), "HMax2");
                cfg.SMin2 = ParseInt(Prompt("  S下限2 [0-255]: "), "SMin2");
                cfg.SMax2 = ParseInt(Prompt("  S上限2 [0-255]: "), "SMax2");
                cfg.VMin2 = ParseInt(Prompt("  V下限2 [0-255]: "), "VMin2");
                cfg.VMax2 = ParseInt(Prompt("  V上限2 [0-255]: "), "VMax2");
            }
        }
        else
        {
            ColorPreset.Apply(presetStr, cfg);
        }

        // カーネルサイズ
        string kernStr = Prompt($"モルフォロジーカーネルサイズ（デフォルト {cfg.MorphKernelSize}）: ");
        if (!string.IsNullOrWhiteSpace(kernStr))
            cfg.MorphKernelSize = ParseInt(kernStr, "kernel");

        // 最小面積
        string areaStr = Prompt($"最小検出面積 [px²]（デフォルト {cfg.MinAreaPixels}）: ");
        if (!string.IsNullOrWhiteSpace(areaStr))
            cfg.MinAreaPixels = ParseDouble(areaStr, "minarea");

        // 中間画像保存
        string saveStr = Prompt("中間画像を保存しますか？ [y/N]: ");
        if (saveStr.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
        {
            cfg.SaveIntermediateImages = true;
            string dirStr = Prompt($"出力ディレクトリ（デフォルト {cfg.OutputDir}）: ");
            if (!string.IsNullOrWhiteSpace(dirStr))
                cfg.OutputDir = dirStr.Trim();
        }

        Console.WriteLine();
        return cfg;
    }

    // ── 出力ヘルパー ────────────────────────────────────────────────────

    static void PrintConfig(MeasurementConfig cfg)
    {
        Console.WriteLine("--- 実行設定 ---");
        Console.WriteLine($"  画像       : {cfg.ImagePath}");
        Console.WriteLine($"  スケール   : {cfg.ScaleMmPerPixel} mm/px");
        Console.WriteLine($"  プリセット : {cfg.PresetName}");
        Console.WriteLine($"  HSV 第1範囲: H[{cfg.HMin}-{cfg.HMax}] S[{cfg.SMin}-{cfg.SMax}] V[{cfg.VMin}-{cfg.VMax}]");
        if (cfg.UseSecondaryRange)
            Console.WriteLine($"  HSV 第2範囲: H[{cfg.HMin2}-{cfg.HMax2}] S[{cfg.SMin2}-{cfg.SMax2}] V[{cfg.VMin2}-{cfg.VMax2}]");
        Console.WriteLine($"  カーネル   : {cfg.MorphKernelSize}");
        Console.WriteLine($"  最小面積   : {cfg.MinAreaPixels} px²");
        if (cfg.SaveIntermediateImages)
            Console.WriteLine($"  中間画像   : {cfg.OutputDir}");
        Console.WriteLine();
    }

    static void PrintResults(List<MeasurementResult> results)
    {
        Console.WriteLine("--- 計測結果 ---");
        if (results.Count == 0)
        {
            Console.WriteLine("検出オブジェクトなし（HSV 範囲やカーネルサイズを調整してください）");
            return;
        }

        Console.WriteLine($"検出数: {results.Count} 件");
        Console.WriteLine(new string('─', 60));
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            Console.WriteLine($"オブジェクト #{i + 1}");
            Console.WriteLine($"  幅 (Width)  : {r.WidthMm,8:F2} mm  ({r.WidthPixels,8:F1} px)");
            Console.WriteLine($"  長さ(Length): {r.LengthMm,8:F2} mm  ({r.LengthPixels,8:F1} px)");
            Console.WriteLine($"  面積        : {r.AreaPixels,10:F0} px²");
            Console.WriteLine($"  重心        : ({r.Center.X:F1}, {r.Center.Y:F1}) px");
            Console.WriteLine($"  傾き        : {r.RotationAngleDeg,6:F1}°");
            Console.WriteLine($"  アスペクト比: {r.AspectRatio:F2}");
            if (i < results.Count - 1)
                Console.WriteLine(new string('─', 60));
        }
        Console.WriteLine(new string('─', 60));
    }

    static void PrintUsage()
    {
        Console.WriteLine(@"
使い方:
  ObjectMeasurement [オプション] [画像パス]

基本オプション:
  -i, --image <path>        入力画像パス（PNG 等）
  -s, --scale <value>       スケール [mm/pixel]（デフォルト: 0.2）
  -p, --preset <name>       プリセット名（omurice / tomato / plate / custom）
  -h, --help                このヘルプを表示

HSV 第1色域（プリセット未使用 または custom のとき）:
  --hmin <0-179>  色相 下限
  --hmax <0-179>  色相 上限
  --smin <0-255>  彩度 下限
  --smax <0-255>  彩度 上限
  --vmin <0-255>  明度 下限
  --vmax <0-255>  明度 上限

HSV 第2色域（赤など折り返し用）:
  --use-secondary           第2色域を有効化
  --hmin2 / --hmax2 / ...   第2色域の HSV 値

調整オプション:
  -k, --kernel <size>       モルフォロジーカーネルサイズ（デフォルト: 25）
  -a, --minarea <px²>       最小検出面積（デフォルト: 5000）
  -n, --maxobjects <n>      最大検出数（デフォルト: 0=無制限）

出力オプション:
  --save-intermediate       中間画像を保存する
  -o, --output <dir>        中間画像の出力先（指定すると --save-intermediate も有効）

使用例:
  ObjectMeasurement -i photo.png -p omurice -s 0.2
  ObjectMeasurement -i photo.png -p tomato --save-intermediate -o ./debug
  ObjectMeasurement -i photo.png -p custom --hmin 20 --hmax 40 --smin 50 --smax 255 --vmin 100 --vmax 255
");
    }

    // ── ユーティリティ ──────────────────────────────────────────────────

    static string Prompt(string message)
    {
        Console.Write(message);
        return Console.ReadLine() ?? string.Empty;
    }

    static string RequireNext(string[] args, ref int i, string key)
    {
        if (++i >= args.Length)
            throw new ArgumentException($"{key} requires a value");
        return args[i];
    }

    static int ParseInt(string s, string key)
    {
        if (!int.TryParse(s, out int v))
            throw new ArgumentException($"'{key}' must be an integer, got: {s}");
        return v;
    }

    static double ParseDouble(string s, string key)
    {
        if (!double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double v))
            throw new ArgumentException($"'{key}' must be a number, got: {s}");
        return v;
    }
}
