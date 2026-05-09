using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutomaticChiselling;
using SkiaSharp;

/// <summary>
/// Pixel-art generator: drop a PNG/JPG/BMP/etc. into autochisel/images/, pick it
/// from the dropdown, get a chisel slab with the picture's pixels quantised to
/// a chosen palette size (same median-cut algorithm the OBJ importer uses).
///
/// Output:
///   width × height × 16  (the slab is always one chisel-block deep)
///
/// Composition along the depth axis (Z):
///   - image  — N voxels carrying the picture's pixels (transparent → empty)
///   - backing — M voxels behind the image, FOLLOWING THE IMAGE SHAPE: a
///                backing voxel only exists where the image had an opaque
///                pixel, so a logo on a transparent PNG keeps its silhouette.
///                Backing gets its own palette slot, so the player assigns a
///                separate material to it in the Materials dialog.
///   - the rest of the 16-voxel depth is air, positioned per Alignment:
///       Front   — image+backing pinned to z=0
///       Center  — centred inside the 16-voxel envelope
///       Back    — pinned to z=15
///
/// imageDepth + backing is clamped to ≤ 16 (one chisel-block deep). Defaults
/// are 1 + 0 so the result is the classic flat poster.
///
/// Asset folder: autochisel/images/  (auto-created on load)
/// </summary>
public class ImagePixelArtGenerator : IShapeGenerator
{
    public string Name => "Image (pixel art)";
    public string Description =>
        "Drop a PNG/JPG/BMP/GIF/WebP into autochisel/images/, " +
        "pick it here, set thickness/backing, save.";

    public string AssetFolderName => "images";

    // Default action — Save only. Open Models tab to actually load.
    public GeneratorAction Action => GeneratorAction.Save;

    // Show "Pick: inventory / Pick: all blocks / Reset" buttons in the Generators
    // tab. Auto-matches the image's quantised palette to game blocks and updates
    // the live preview so you see the model in the chosen materials before saving.
    public bool SupportsColorPicker => true;

    public ShapeParameter[] Parameters
    {
        get
        {
            string[] paths = GeneratorAssets.ListFiles("images");
            var supported = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
            var names = paths
                .Where(p => supported.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Select(Path.GetFileName)
                .OrderBy(n => n)
                .ToArray();

            string[] dropdownVals;
            string defaultVal;
            if (names.Length == 0)
            {
                dropdownVals = new[] { "(no images — drop one into autochisel/images/)" };
                defaultVal = dropdownVals[0];
            }
            else
            {
                dropdownVals = names;
                defaultVal = names[0];
            }

            return new[]
            {
                new ShapeParameter {
                    Id = "file", Label = "Image",
                    Type = ParameterType.Dropdown,
                    Default = defaultVal,
                    DropdownValues = dropdownVals
                },
                new ShapeParameter {
                    Id = "max_size", Label = "Max size (px)",
                    Default = 64, Min = 8, Max = 256
                },
                new ShapeParameter {
                    Id = "palette", Label = "Max colors",
                    // 253 max — slot 254 is reserved for the backing material
                    // so the backing slot is always free regardless of image colors.
                    Default = 32, Min = 2, Max = 253
                },
                new ShapeParameter {
                    Id = "alpha_cutoff", Label = "Alpha cutoff",
                    Default = 32, Min = 0, Max = 255
                },
                new ShapeParameter {
                    Id = "image_depth", Label = "Image thickness",
                    Default = 1, Min = 1, Max = 16
                },
                new ShapeParameter {
                    Id = "backing", Label = "Backing thickness",
                    // 0 = no backing. Total (image_depth + backing) is auto-
                    // clamped at runtime so it never exceeds 16.
                    Default = 0, Min = 0, Max = 15
                },
                new ShapeParameter {
                    Id = "alignment", Label = "Alignment",
                    Type = ParameterType.Dropdown,
                    Default = "Front",
                    DropdownValues = new[] { "Front", "Center", "Back" }
                }
            };
        }
    }

    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        string fileName = p.GetString("file", "");
        int maxSize     = Math.Max(8,  Math.Min(256, p.GetInt("max_size", 64)));
        int maxColors   = Math.Max(2,  Math.Min(253, p.GetInt("palette", 32)));
        int alphaCut    = Math.Max(0,  Math.Min(255, p.GetInt("alpha_cutoff", 32)));
        int imageDepth  = Math.Max(1,  Math.Min(16,  p.GetInt("image_depth", 1)));
        int backing     = Math.Max(0,  Math.Min(15,  p.GetInt("backing", 0)));
        string align    = p.GetString("alignment", "Front");

        // imageDepth + backing must fit the 16-voxel chisel-block depth.
        // Clamp the BACKING down (image keeps its requested thickness).
        if (imageDepth + backing > 16) backing = Math.Max(0, 16 - imageDepth);

        string folder = GeneratorAssets.FolderPath("images");
        string path = Path.Combine(folder, fileName ?? "");
        if (string.IsNullOrEmpty(fileName) || !File.Exists(path))
            return GeneratedShape.Empty(1, 1, 1);

        // --- 1. Load + resize ---------------------------------------------------
        SKBitmap bmp = null;
        try { bmp = SKBitmap.Decode(path); }
        catch { return GeneratedShape.Empty(1, 1, 1); }
        if (bmp == null) return GeneratedShape.Empty(1, 1, 1);

        try
        {
            int srcW = bmp.Width, srcH = bmp.Height;
            if (srcW <= 0 || srcH <= 0) return GeneratedShape.Empty(1, 1, 1);

            // Preserve aspect — longest axis becomes maxSize voxels
            int dstW, dstH;
            if (srcW >= srcH)
            {
                dstW = maxSize;
                dstH = Math.Max(1, (int)Math.Round(srcH * (double)maxSize / srcW));
            }
            else
            {
                dstH = maxSize;
                dstW = Math.Max(1, (int)Math.Round(srcW * (double)maxSize / srcH));
            }

            // Sample every output pixel from the source via nearest neighbour.
            int[] argb = new int[dstW * dstH];
            byte[] alpha = new byte[dstW * dstH];
            for (int y = 0; y < dstH; y++)
            {
                int sy = (int)((y + 0.5) * srcH / dstH);
                if (sy < 0) sy = 0; else if (sy >= srcH) sy = srcH - 1;
                for (int x = 0; x < dstW; x++)
                {
                    int sx = (int)((x + 0.5) * srcW / dstW);
                    if (sx < 0) sx = 0; else if (sx >= srcW) sx = srcW - 1;
                    var c = bmp.GetPixel(sx, sy);
                    argb[y * dstW + x] = (c.Red << 16) | (c.Green << 8) | c.Blue;
                    alpha[y * dstW + x] = c.Alpha;
                }
            }

            // --- 2. Build palette via median-cut over visible pixels ----------
            var visibleColors = new List<int>(dstW * dstH);
            for (int i = 0; i < argb.Length; i++)
                if (alpha[i] >= alphaCut) visibleColors.Add(argb[i]);
            if (visibleColors.Count == 0) return GeneratedShape.Empty(1, 1, 1);

            byte[][] palette = MedianCutPalette(visibleColors, maxColors);

            // Reserve a backing slot — find the first unused slot AFTER the
            // image colors. 180/180/180 is MedianCutPalette's default fill =
            // empty. Set a clearly-distinct dark grey on it.
            byte backingIdx = 0;
            if (backing > 0)
            {
                for (byte slot = 1; slot < 255; slot++)
                {
                    var pe = palette[slot];
                    bool empty = pe == null
                        || (pe[0] == 180 && pe[1] == 180 && pe[2] == 180);
                    if (empty)
                    {
                        palette[slot] = new byte[] { 90, 90, 90, 255 };
                        backingIdx = slot;
                        break;
                    }
                }
            }

            // Cache for nearest-palette lookups.
            var nearestCache = new Dictionary<int, byte>();
            byte NearestIdx(int rgb)
            {
                if (nearestCache.TryGetValue(rgb, out var cached)) return cached;
                int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
                byte best = 1;
                int bestD = int.MaxValue;
                for (int i = 1; i <= maxColors; i++)
                {
                    var pe = palette[i];
                    if (pe == null) continue;
                    int dr = pe[0] - r, dg = pe[1] - g, db = pe[2] - b;
                    int d = dr * dr + dg * dg + db * db;
                    if (d < bestD) { bestD = d; best = (byte)i; }
                }
                nearestCache[rgb] = best;
                return best;
            }

            // --- 3. Place image + backing in a 16-deep slab -------------------
            // Layout along Z (alignment-dependent):
            //   image:   z = imgZ0   .. imgZ0  + imageDepth - 1
            //   backing: z = backZ0  .. backZ0 + backing    - 1
            int totalDepth = imageDepth + backing;
            int slabDepth = 16;
            int z0;
            switch (align)
            {
                case "Center": z0 = (slabDepth - totalDepth) / 2; break;
                case "Back":   z0 = slabDepth - totalDepth; break;
                default:       z0 = 0; break; // Front
            }
            if (z0 < 0) z0 = 0;
            int imgZ0  = z0;
            int backZ0 = z0 + imageDepth;

            var shape = new GeneratedShape
            {
                Voxels = new byte[dstW, dstH, slabDepth],
                Palette = palette
            };

            // Walk every pixel — for opaque ones, fill the image column AND
            // the backing column behind it (backing follows the picture's
            // silhouette, so transparent pixels stay air all the way through).
            for (int y = 0; y < dstH; y++)
                for (int x = 0; x < dstW; x++)
                {
                    int idx = y * dstW + x;
                    if (alpha[idx] < alphaCut) continue;     // transparent → no voxel here at all

                    byte pIdx = NearestIdx(argb[idx]);
                    int outY = dstH - 1 - y; // flip image Y (rows go top→down → bottom→up)

                    // Image volume
                    for (int dz = 0; dz < imageDepth; dz++)
                        shape.Voxels[x, outY, imgZ0 + dz] = pIdx;

                    // Backing volume (only behind opaque pixels)
                    if (backing > 0 && backingIdx > 0)
                    {
                        for (int dz = 0; dz < backing; dz++)
                            shape.Voxels[x, outY, backZ0 + dz] = backingIdx;
                    }
                }

            return shape;
        }
        finally { bmp.Dispose(); }
    }

    // ----------------------------------------------------------------------
    // Median-cut palette generation. Same Heckbert algorithm the OBJ importer
    // uses; copied here so the generator stays a fully self-contained script.
    // ----------------------------------------------------------------------
    private static byte[][] MedianCutPalette(List<int> allColors, int maxColors)
    {
        var palette = new byte[256][];
        for (int i = 0; i < 256; i++)
            palette[i] = new byte[] { 180, 180, 180, 255 };
        if (allColors.Count == 0) return palette;

        var buckets = new List<List<int>> { allColors };
        while (buckets.Count < maxColors)
        {
            int bestIdx = -1, bestExtent = 0, bestAxis = 0;
            for (int i = 0; i < buckets.Count; i++)
            {
                var bk = buckets[i];
                if (bk.Count <= 1) continue;
                int rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
                foreach (var col in bk)
                {
                    int r = (col >> 16) & 0xFF, g = (col >> 8) & 0xFF, b = col & 0xFF;
                    if (r < rMin) rMin = r; if (r > rMax) rMax = r;
                    if (g < gMin) gMin = g; if (g > gMax) gMax = g;
                    if (b < bMin) bMin = b; if (b > bMax) bMax = b;
                }
                int rE = rMax - rMin, gE = gMax - gMin, bE = bMax - bMin;
                int e = rE; int candAxis = 0;
                if (gE > e) { e = gE; candAxis = 1; }
                if (bE > e) { e = bE; candAxis = 2; }
                if (e > bestExtent)
                {
                    bestExtent = e; bestIdx = i; bestAxis = candAxis;
                }
            }
            if (bestIdx < 0) break;

            var bucket = buckets[bestIdx];
            int splitAxis = bestAxis;
            bucket.Sort((x, y) =>
            {
                int va = (x >> (16 - splitAxis * 8)) & 0xFF;
                int vb = (y >> (16 - splitAxis * 8)) & 0xFF;
                return va.CompareTo(vb);
            });
            int mid = bucket.Count / 2;
            var left  = bucket.GetRange(0, mid);
            var right = bucket.GetRange(mid, bucket.Count - mid);
            buckets[bestIdx] = left;
            buckets.Add(right);
        }

        int slot = 1;
        foreach (var bk in buckets)
        {
            if (slot >= 255) break;
            if (bk.Count == 0) continue;
            long rSum = 0, gSum = 0, bSum = 0;
            foreach (var col in bk)
            {
                rSum += (col >> 16) & 0xFF;
                gSum += (col >>  8) & 0xFF;
                bSum +=  col        & 0xFF;
            }
            palette[slot] = new byte[]
            {
                (byte)(rSum / bk.Count),
                (byte)(gSum / bk.Count),
                (byte)(bSum / bk.Count),
                255
            };
            slot++;
        }
        for (int i = slot; i < 255; i++) palette[i] = null;
        return palette;
    }
}
