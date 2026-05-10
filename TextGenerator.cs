using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutomaticChiselling;
using SkiaSharp;

/// <summary>
/// Генератор текста в вокселях.
/// Шрифты (.ttf / .otf) клади в autochisel/fonts/ — папка создаётся автоматически.
///
/// Особенности:
///   - Выбор шрифта из папки autochisel/fonts/
///   - Размер шрифта, глубина по Z
///   - Обводка с отдельным цветом (palette slot 2), текст — palette slot 1
///   - Двухцветность: Material Mapping позволяет назначить разные блоки
///     на текст и обводку
/// </summary>
public class TextGenerator : IShapeGenerator
{
    public string Name        => "Text";
    public string Description =>
        "Renders text as voxels. Drop .ttf/.otf fonts into autochisel/fonts/, " +
        "then pick a font here. Outline uses a separate palette slot for two-color mapping.";

    public string AssetFolderName => "fonts";

    public GeneratorAction Action => GeneratorAction.Save;

    public bool SupportsColorPicker => true;

    public ShapeParameter[] Parameters
    {
        get
        {
            string[] paths = GeneratorAssets.ListFiles("fonts");
            var supported = new[] { ".ttf", ".otf" };
            var names = paths
                .Where(f => supported.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(Path.GetFileName)
                .OrderBy(n => n)
                .ToArray();

            string[] fontDropdown;
            string fontDefault;
            if (names.Length == 0)
            {
                fontDropdown = new[] { "(no fonts — drop .ttf into autochisel/fonts/)" };
                fontDefault  = fontDropdown[0];
            }
            else
            {
                fontDropdown = names;
                fontDefault  = names[0];
            }

            return new[]
            {
                // Текст
                new ShapeParameter {
                    Id = "text", Label = "Text",
                    Type = ParameterType.TextInput,
                    Default = "Hello", MaxLength = 64, Placeholder = "Enter text"
                },

                // Шрифт
                new ShapeParameter {
                    Id = "font", Label = "Font",
                    Type = ParameterType.Dropdown,
                    Default = fontDefault, DropdownValues = fontDropdown
                },

                // Размер шрифта
                new ShapeParameter {
                    Id = "font_size", Label = "Font size",
                    Default = 32, Min = 8, Max = 128
                },

                // Глубина по Z
                new ShapeParameter {
                    Id = "depth", Label = "Depth",
                    Default = 4, Min = 1, Max = 32
                },

                // Обводка — 0 = выключена
                new ShapeParameter {
                    Id = "outline", Label = "Outline thickness",
                    Default = 0, Min = 0, Max = 16
                },

                // Цвет текста
                new ShapeParameter {
                    Id = "text_r", Label = "Text R",
                    Default = 255, Min = 0, Max = 255
                },
                new ShapeParameter {
                    Id = "text_g", Label = "Text G",
                    Default = 255, Min = 0, Max = 255
                },
                new ShapeParameter {
                    Id = "text_b", Label = "Text B",
                    Default = 255, Min = 0, Max = 255
                },

                // Цвет обводки
                new ShapeParameter {
                    Id = "out_r", Label = "Outline R",
                    Default = 30, Min = 0, Max = 255
                },
                new ShapeParameter {
                    Id = "out_g", Label = "Outline G",
                    Default = 30, Min = 0, Max = 255
                },
                new ShapeParameter {
                    Id = "out_b", Label = "Outline B",
                    Default = 30, Min = 0, Max = 255
                },

                // Ширина холста (0 = авто, подгоняется под текст)
                new ShapeParameter {
                    Id = "canvas_width", Label = "Canvas width (0=auto)",
                    Default = 0, Min = 0, Max = 512
                },

                // Горизонтальное выравнивание
                new ShapeParameter {
                    Id = "align", Label = "Alignment",
                    Type = ParameterType.Dropdown,
                    Default = "Left",
                    DropdownValues = new[] { "Left", "Center", "Right" }
                },
            };
        }
    }

    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        string text     = p.GetString("text",    "Hello");
        string fontFile = p.GetString("font",    "");
        int fontSize    = Math.Max(8,  Math.Min(128, p.GetInt("font_size", 32)));
        int depth       = Math.Max(1,  Math.Min(32,  p.GetInt("depth",     4)));
        int outline     = Math.Max(0,  Math.Min(16,  p.GetInt("outline",   0)));

        int tR = Math.Max(0, Math.Min(255, p.GetInt("text_r", 255)));
        int tG = Math.Max(0, Math.Min(255, p.GetInt("text_g", 255)));
        int tB = Math.Max(0, Math.Min(255, p.GetInt("text_b", 255)));
        int oR = Math.Max(0, Math.Min(255, p.GetInt("out_r",  30)));
        int oG = Math.Max(0, Math.Min(255, p.GetInt("out_g",  30)));
        int oB = Math.Max(0, Math.Min(255, p.GetInt("out_b",  30)));
        int canvasWidth = Math.Max(0, p.GetInt("canvas_width", 0));
        string align    = p.GetString("align", "Left");

        if (string.IsNullOrEmpty(text)) text = " ";

        // 1. Загрузка шрифта через SkiaSharp
        SKTypeface typeface = null;
        if (!string.IsNullOrEmpty(fontFile))
        {
            string fontPath = Path.Combine(GeneratorAssets.FolderPath("fonts"), fontFile);
            if (File.Exists(fontPath))
            {
                try { typeface = SKTypeface.FromFile(fontPath); }
                catch { typeface = null; }
            }
        }
        if (typeface == null)
            typeface = SKTypeface.Default;

        // 2. Замер текста для определения размера bitmap
        int pad = outline + 2;
        SKRect textBounds = SKRect.Empty;
        using (var measurePaint = MakePaint(typeface, fontSize, SKColors.White, 0))
            measurePaint.MeasureText(text, ref textBounds);

        // textBounds.Left бывает отрицательным (bearing) — учитываем в реальной ширине блока
        int textPixelW = (int)Math.Ceiling(textBounds.Width - textBounds.Left) + pad * 2;
        int bmpH       = Math.Max(1, (int)Math.Ceiling(textBounds.Height - textBounds.Top) + pad * 2);

        // Если canvas_width задан и больше текста — используем его, иначе авто
        int bmpW = (canvasWidth > 0 && canvasWidth > textPixelW) ? canvasWidth : textPixelW;

        // Базовая позиция рисования при выравнивании влево
        float baseDrawX = pad - textBounds.Left;
        float drawY     = pad - textBounds.Top;
        int offsetX;
        switch (align)
        {
            case "Center": offsetX = (bmpW - textPixelW) / 2; break;
            case "Right":  offsetX = bmpW - textPixelW;       break;
            default:       offsetX = 0;                        break; // Left
        }

        float drawX = baseDrawX + offsetX;

        // mask: 0=пусто, 1=текст, 2=обводка
        var mask = new byte[bmpW, bmpH];

        // 3. Рисуем обводку — читаем маску как slot 2
        if (outline > 0)
        {
            using (var bmp = new SKBitmap(bmpW, bmpH, SKColorType.Rgba8888, SKAlphaType.Premul))
            using (var canvas = new SKCanvas(bmp))
            {
                canvas.Clear(SKColors.Transparent);
                using (var strokePaint = MakePaint(typeface, fontSize, SKColors.White, outline))
                    canvas.DrawText(text, drawX, drawY, strokePaint);

                for (int x = 0; x < bmpW; x++)
                    for (int y = 0; y < bmpH; y++)
                        if (bmp.GetPixel(x, y).Alpha > 64)
                            mask[x, y] = 2;
            }
        }

        // 4. Рисуем заливку текста — slot 1, перезаписывает обводку
        using (var bmp = new SKBitmap(bmpW, bmpH, SKColorType.Rgba8888, SKAlphaType.Premul))
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Transparent);
            using (var fillPaint = MakePaint(typeface, fontSize, SKColors.White, 0))
                canvas.DrawText(text, drawX, drawY, fillPaint);

            for (int x = 0; x < bmpW; x++)
                for (int y = 0; y < bmpH; y++)
                    if (bmp.GetPixel(x, y).Alpha > 64)
                        mask[x, y] = 1;
        }

        typeface.Dispose();

        // 5. Сборка палитры
        var palette = new byte[256][];
        for (int i = 0; i < 256; i++)
            palette[i] = new byte[] { 180, 180, 180, 255 };

        palette[1] = new byte[] { (byte)tR, (byte)tG, (byte)tB, 255 }; // текст
        palette[2] = new byte[] { (byte)oR, (byte)oG, (byte)oB, 255 }; // обводка

        for (int i = 3; i < 255; i++) palette[i] = null;

        // 6. Заполняем вокселя
        var shape = new GeneratedShape
        {
            Voxels  = new byte[bmpW, bmpH, depth],
            Palette = palette
        };

        for (int x = 0; x < bmpW; x++)
            for (int y = 0; y < bmpH; y++)
            {
                byte idx = mask[x, y];
                if (idx == 0) continue;

                int outY = bmpH - 1 - y; // инвертируем Y: bitmap вниз, модель вверх

                for (int z = 0; z < depth; z++)
                    shape.Voxels[x, outY, z] = idx;
            }

        return shape;
    }

    // strokeWidth == 0 → Fill; > 0 → Stroke (обводка)
    private static SKPaint MakePaint(SKTypeface tf, int size, SKColor color, int strokeWidth)
    {
        var paint = new SKPaint
        {
            Typeface    = tf,
            TextSize    = size,
            IsAntialias = false,
            Color       = color,
        };

        if (strokeWidth > 0)
        {
            paint.Style       = SKPaintStyle.Stroke;
            paint.StrokeWidth = strokeWidth * 2;
            paint.StrokeCap   = SKStrokeCap.Round;
            paint.StrokeJoin  = SKStrokeJoin.Round;
        }
        else
        {
            paint.Style = SKPaintStyle.Fill;
        }

        return paint;
    }
}
