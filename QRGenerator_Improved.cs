// ============================================================================
// QR Code Generator — compact configurable generator for AutoChisel.
// Improved version with compact QR optimization and extended controls.
// ============================================================================

using System;
using System.Collections.Generic;
using AutomaticChiselling;

public class QRGenerator : IShapeGenerator
{
    public string Name => "QR Code";
    public string Description => "Compact configurable QR code voxel generator";

    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter
        {
            Id = "text",
            Label = "Text / URL",
            Type = ParameterType.TextInput,
            Default = "https://vintagestory.at",
            MaxLength = 1024
        },

        new ShapeParameter
        {
            Id = "cellSize",
            Label = "Voxels per module",
            Default = 1,
            Min = 1,
            Max = 8
        },

        new ShapeParameter
        {
            Id = "thickness",
            Label = "Thickness",
            Default = 1,
            Min = 1,
            Max = 32
        },

        new ShapeParameter
        {
            Id = "quietZone",
            Label = "Quiet zone",
            Default = 1,
            Min = 0,
            Max = 8
        },

        new ShapeParameter
        {
            Id = "maxVersion",
            Label = "Max QR version",
            Default = 4,
            Min = 1,
            Max = 40
        },

        new ShapeParameter
        {
            Id = "backing",
            Label = "Include backing",
            Type = ParameterType.Checkbox,
            Default = false
        },

        new ShapeParameter
        {
            Id = "boostEcl",
            Label = "Auto boost ECC",
            Type = ParameterType.Checkbox,
            Default = false
        },

        new ShapeParameter
        {
            Id = "invert",
            Label = "Invert colors",
            Type = ParameterType.Checkbox,
            Default = false
        },

        new ShapeParameter
        {
            Id = "canvas_width",
            Label = "Canvas width (0=auto)",
            Default = 0,
            Min = 0,
            Max = 512
        },

        new ShapeParameter
        {
            Id = "canvas_height",
            Label = "Canvas height (0=auto)",
            Default = 0,
            Min = 0,
            Max = 512
        },

        new ShapeParameter
        {
            Id = "align_h",
            Label = "Align horizontal",
            Type = ParameterType.Dropdown,
            Default = "Left",
            DropdownValues = new[] { "Left", "Center", "Right" }
        },

        new ShapeParameter
        {
            Id = "align_v",
            Label = "Align vertical",
            Type = ParameterType.Dropdown,
            Default = "Bottom",
            DropdownValues = new[] { "Bottom", "Center", "Top" }
        },

        new ShapeParameter
        {
            Id = "ecc",
            Label = "Error correction",
            Type = ParameterType.Dropdown,
            Default = "Low",
            DropdownValues = new[]
            {
                "Low",
                "Medium",
                "Quartile",
                "High"
            }
        }
    };

    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        string text = p.GetString("text", "hello");

        if (string.IsNullOrWhiteSpace(text))
            text = " ";

        int cell = Math.Max(1, p.GetInt("cellSize", 1));
        int thickness = Math.Max(1, p.GetInt("thickness", 1));
        int quiet = Math.Max(0, p.GetInt("quietZone", 1));
        int maxVersion = Math.Max(1, p.GetInt("maxVersion", 4));

        bool backing = p.GetBool("backing", false);
        bool boostEcl = p.GetBool("boostEcl", false);
        bool invert = p.GetBool("invert", false);
        int canvasWidth  = Math.Max(0, p.GetInt("canvas_width",  0));
        int canvasHeight = Math.Max(0, p.GetInt("canvas_height", 0));
        string alignH = p.GetString("align_h", "Left");
        string alignV = p.GetString("align_v", "Bottom");

        string eccStr = p.GetString("ecc", "Low");

        QrCode.Ecc ecc;

        switch (eccStr)
        {
            case "Low":
                ecc = QrCode.Ecc.Low;
                break;

            case "Quartile":
                ecc = QrCode.Ecc.Quartile;
                break;

            case "High":
                ecc = QrCode.Ecc.High;
                break;

            default:
                ecc = QrCode.Ecc.Medium;
                break;
        }

        var segs = QrSegment.MakeSegments(text);

        var qr = QrCode.EncodeSegments(
            segs,
            ecc,
            minVersion: 1,
            maxVersion: maxVersion,
            mask: -1,
            boostEcl: boostEcl
        );

        int n = qr.Size;

        int side = (n + quiet * 2) * cell;

        // Холст: если задан и больше QR — используем, иначе авто
        int sizeX = (canvasWidth  > 0 && canvasWidth  > side) ? canvasWidth  : side;
        int sizeY = (canvasHeight > 0 && canvasHeight > side) ? canvasHeight : side;
        int sizeZ = thickness;

        var shape = GeneratedShape.Empty(sizeX, sizeY, sizeZ);

        shape.Palette[1] = new byte[] { 16, 16, 16, 255 };
        shape.Palette[2] = new byte[] { 240, 240, 240, 255 };

        // Горизонтальное смещение (X)
        int offsetX;
        switch (alignH)
        {
            case "Center": offsetX = (sizeX - side) / 2; break;
            case "Right":  offsetX = sizeX - side;       break;
            default:       offsetX = 0;                  break; // Left
        }

        // Вертикальное смещение (Y)
        // Y=0 — низ модели, Y=sizeY-1 — верх
        int offsetY;
        switch (alignV)
        {
            case "Top":    offsetY = sizeY - side; break;
            case "Center": offsetY = (sizeY - side) / 2; break;
            default:       offsetY = 0;            break; // Bottom
        }

        for (int qy = 0; qy < n; qy++)
        {
            for (int qx = 0; qx < n; qx++)
            {
                bool dark = qr.GetModule(qx, qy);

                if (invert)
                    dark = !dark;

                if (!dark && !backing)
                    continue;

                byte colorIdx = dark
                    ? (byte)1
                    : (byte)2;

                int xBase = offsetX + (qx + quiet) * cell;
                int yBase = offsetY + ((n - 1 - qy) + quiet) * cell;

                for (int dx = 0; dx < cell; dx++)
                {
                    for (int dy = 0; dy < cell; dy++)
                    {
                        int vx = xBase + dx;
                        int vy = yBase + dy;

                        for (int dz = 0; dz < thickness; dz++)
                        {
                            shape.Voxels[vx, vy, dz] = colorIdx;
                        }
                    }
                }
            }
        }

        if (backing)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    int qx = (x / cell) - quiet;
                    int qy = (y / cell) - quiet;

                    if (qx < 0 || qx >= n || qy < 0 || qy >= n)
                    {
                        for (int dz = 0; dz < thickness; dz++)
                        {
                            shape.Voxels[x, y, dz] = 2;
                        }
                    }
                }
            }
        }

        return shape;
    }
}

// ============================================================================
// Nayuki QR Code Generator (MIT License) — minimal C# port, inline.
// Copyright (c) Project Nayuki. https://www.nayuki.io/page/qr-code-generator-library
// ============================================================================

public sealed class QrCode
{
    public enum Ecc { Low = 0, Medium = 1, Quartile = 2, High = 3 }

    // Format-info bits per QR spec (ISO/IEC 18004):
    //   Low=01 (1), Medium=00 (0), Quartile=11 (3), High=10 (2).
    // This is NOT the same order as the Ecc enum values. Using the enum value
    // directly in format bits is a common bug that makes scanners fail to read
    // the code because they decode the wrong ECL and hence the wrong block layout.
    private static readonly int[] EclFormatBits = { 1, 0, 3, 2 };

    public int Version { get; }
    public int Size { get; }
    public Ecc ErrorCorrectionLevel { get; }
    public int Mask { get; private set; }

    private readonly bool[,] modules;
    private readonly bool[,] isFunction;

    public bool GetModule(int x, int y)
    {
        return 0 <= x && x < Size && 0 <= y && y < Size && modules[y, x];
    }

    public static QrCode EncodeText(string text, Ecc ecl)
    {
        var segs = QrSegment.MakeSegments(text);
        return EncodeSegments(segs, ecl);
    }

    public static QrCode EncodeSegments(List<QrSegment> segs, Ecc ecl,
        int minVersion = 1, int maxVersion = 40, int mask = -1, bool boostEcl = true)
    {
        if (minVersion < 1 || maxVersion > 40 || minVersion > maxVersion || mask < -1 || mask > 7)
            throw new ArgumentOutOfRangeException();

        int version;
        int dataUsedBits = -1;
        for (version = minVersion; ; version++)
        {
            int dataCapacityBits = GetNumDataCodewords(version, ecl) * 8;
            dataUsedBits = QrSegment.GetTotalBits(segs, version);
            if (dataUsedBits != -1 && dataUsedBits <= dataCapacityBits) break;
            if (version >= maxVersion)
                throw new ArgumentException("Data too long for any version at this ECL");
        }

        foreach (Ecc newEcl in new[] { Ecc.Medium, Ecc.Quartile, Ecc.High })
        {
            if (boostEcl && dataUsedBits <= GetNumDataCodewords(version, newEcl) * 8)
                ecl = newEcl;
        }

        var bb = new BitBuffer();
        foreach (var seg in segs)
        {
            bb.AppendBits(seg.ModeType.ModeBits, 4);
            bb.AppendBits(seg.NumChars, seg.ModeType.NumCharCountBits(version));
            bb.AppendData(seg.Data);
        }

        int dataCapacityBits2 = GetNumDataCodewords(version, ecl) * 8;
        bb.AppendBits(0, Math.Min(4, dataCapacityBits2 - bb.BitLength));
        bb.AppendBits(0, (8 - bb.BitLength % 8) % 8);
        for (int padByte = 0xEC; bb.BitLength < dataCapacityBits2; padByte ^= 0xEC ^ 0x11)
            bb.AppendBits(padByte, 8);

        var dataCodewords = new byte[bb.BitLength / 8];
        for (int i = 0; i < bb.BitLength; i++)
            if (bb.GetBit(i)) dataCodewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));

        return new QrCode(version, ecl, dataCodewords, mask);
    }

    private QrCode(int ver, Ecc ecl, byte[] dataCodewords, int msk)
    {
        if (ver < 1 || ver > 40) throw new ArgumentOutOfRangeException();
        Version = ver;
        Size = ver * 4 + 17;
        ErrorCorrectionLevel = ecl;
        modules = new bool[Size, Size];
        isFunction = new bool[Size, Size];
        DrawFunctionPatterns();
        var allCodewords = AddEccAndInterleave(dataCodewords);
        DrawCodewords(allCodewords);

        if (msk == -1)
        {
            int minPenalty = int.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                ApplyMask(i); DrawFormatBits(i);
                int penalty = GetPenaltyScore();
                if (penalty < minPenalty) { msk = i; minPenalty = penalty; }
                ApplyMask(i);
            }
        }
        Mask = msk;
        ApplyMask(msk);
        DrawFormatBits(msk);
    }

    private void DrawFunctionPatterns()
    {
        for (int i = 0; i < Size; i++)
        {
            SetFunctionModule(6, i, i % 2 == 0);
            SetFunctionModule(i, 6, i % 2 == 0);
        }
        DrawFinderPattern(3, 3);
        DrawFinderPattern(Size - 4, 3);
        DrawFinderPattern(3, Size - 4);

        var alignPatPos = GetAlignmentPatternPositions();
        int numAlign = alignPatPos.Length;
        for (int i = 0; i < numAlign; i++)
            for (int j = 0; j < numAlign; j++)
                if (!(i == 0 && j == 0 || i == 0 && j == numAlign - 1 || i == numAlign - 1 && j == 0))
                    DrawAlignmentPattern(alignPatPos[i], alignPatPos[j]);

        DrawFormatBits(0);
        DrawVersion();
    }

    private void DrawFormatBits(int msk)
    {
        // Correct ECL→format-bit mapping (not the raw enum value!).
        int data = EclFormatBits[(int)ErrorCorrectionLevel] << 3 | msk;
        int rem = data;
        for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
        int bits = (data << 10 | rem) ^ 0x5412;

        for (int i = 0; i <= 5; i++) SetFunctionModule(8, i, GetBitB(bits, i));
        SetFunctionModule(8, 7, GetBitB(bits, 6));
        SetFunctionModule(8, 8, GetBitB(bits, 7));
        SetFunctionModule(7, 8, GetBitB(bits, 8));
        for (int i = 9; i < 15; i++) SetFunctionModule(14 - i, 8, GetBitB(bits, i));

        for (int i = 0; i < 8; i++) SetFunctionModule(Size - 1 - i, 8, GetBitB(bits, i));
        for (int i = 8; i < 15; i++) SetFunctionModule(8, Size - 15 + i, GetBitB(bits, i));
        SetFunctionModule(8, Size - 8, true);
    }

    private void DrawVersion()
    {
        if (Version < 7) return;
        int rem = Version;
        for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
        int bits = Version << 12 | rem;

        for (int i = 0; i < 18; i++)
        {
            bool bit = GetBitB(bits, i);
            int a = Size - 11 + i % 3, b = i / 3;
            SetFunctionModule(a, b, bit);
            SetFunctionModule(b, a, bit);
        }
    }

    private void DrawFinderPattern(int x, int y)
    {
        for (int dy = -4; dy <= 4; dy++)
            for (int dx = -4; dx <= 4; dx++)
            {
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                int xx = x + dx, yy = y + dy;
                if (0 <= xx && xx < Size && 0 <= yy && yy < Size)
                    SetFunctionModule(xx, yy, dist != 2 && dist != 4);
            }
    }

    private void DrawAlignmentPattern(int x, int y)
    {
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                SetFunctionModule(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
    }

    private void SetFunctionModule(int x, int y, bool isDark)
    {
        modules[y, x] = isDark;
        isFunction[y, x] = true;
    }

    private byte[] AddEccAndInterleave(byte[] data)
    {
        int numBlocks = NumErrorCorrectionBlocks[(int)ErrorCorrectionLevel, Version];
        int blockEccLen = EccCodewordsPerBlock[(int)ErrorCorrectionLevel, Version];
        int rawCodewords = GetNumRawDataModules(Version) / 8;
        int numShortBlocks = numBlocks - rawCodewords % numBlocks;
        int shortBlockLen = rawCodewords / numBlocks;

        var blocks = new byte[numBlocks][];
        var rsDiv = ReedSolomonComputeDivisor(blockEccLen);
        for (int i = 0, k = 0; i < numBlocks; i++)
        {
            var dat = new byte[shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1)];
            Array.Copy(data, k, dat, 0, dat.Length);
            k += dat.Length;
            var block = new byte[shortBlockLen + 1];
            Array.Copy(dat, 0, block, 0, dat.Length);
            var ecc = ReedSolomonComputeRemainder(dat, rsDiv);
            Array.Copy(ecc, 0, block, block.Length - blockEccLen, blockEccLen);
            blocks[i] = block;
        }
        var result = new byte[rawCodewords];
        for (int i = 0, k = 0; i < blocks[0].Length; i++)
            for (int j = 0; j < blocks.Length; j++)
                if (i != shortBlockLen - blockEccLen || j >= numShortBlocks)
                    result[k++] = blocks[j][i];
        return result;
    }

    private void DrawCodewords(byte[] data)
    {
        int i = 0;
        for (int right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (int vert = 0; vert < Size; vert++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    bool upward = ((right + 1) & 2) == 0;
                    int y = upward ? Size - 1 - vert : vert;
                    if (!isFunction[y, x] && i < data.Length * 8)
                    {
                        modules[y, x] = GetBitB(data[i >> 3], 7 - (i & 7));
                        i++;
                    }
                }
            }
        }
    }

    private void ApplyMask(int mask)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                bool invert;
                switch (mask)
                {
                    case 0: invert = (x + y) % 2 == 0; break;
                    case 1: invert = y % 2 == 0; break;
                    case 2: invert = x % 3 == 0; break;
                    case 3: invert = (x + y) % 3 == 0; break;
                    case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                    case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                    case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                    case 7: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    default: throw new ArgumentException();
                }
                if (!isFunction[y, x] && invert)
                    modules[y, x] ^= true;
            }
    }

    private int GetPenaltyScore()
    {
        int result = 0;
        for (int y = 0; y < Size; y++)
        {
            bool color = false; int runX = 0;
            var hist = new int[7];
            for (int x = 0; x < Size; x++)
            {
                if (modules[y, x] == color)
                {
                    runX++;
                    if (runX == 5) result += 3;
                    else if (runX > 5) result++;
                }
                else
                {
                    FinderPenaltyAddHistory(runX, hist);
                    if (!color) result += FinderPenaltyCountPatterns(hist) * 40;
                    color = modules[y, x];
                    runX = 1;
                }
            }
            result += FinderPenaltyTerminateAndCount(color, runX, hist) * 40;
        }
        for (int x = 0; x < Size; x++)
        {
            bool color = false; int runY = 0;
            var hist = new int[7];
            for (int y = 0; y < Size; y++)
            {
                if (modules[y, x] == color)
                {
                    runY++;
                    if (runY == 5) result += 3;
                    else if (runY > 5) result++;
                }
                else
                {
                    FinderPenaltyAddHistory(runY, hist);
                    if (!color) result += FinderPenaltyCountPatterns(hist) * 40;
                    color = modules[y, x];
                    runY = 1;
                }
            }
            result += FinderPenaltyTerminateAndCount(color, runY, hist) * 40;
        }
        for (int y = 0; y < Size - 1; y++)
            for (int x = 0; x < Size - 1; x++)
            {
                bool c = modules[y, x];
                if (c == modules[y, x + 1] && c == modules[y + 1, x] && c == modules[y + 1, x + 1])
                    result += 3;
            }
        int dark = 0;
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) if (modules[y, x]) dark++;
        int total = Size * Size;
        int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
        result += k * 10;
        return result;
    }

    private int FinderPenaltyCountPatterns(int[] hist)
    {
        int n = hist[1];
        bool core = n > 0 && hist[2] == n && hist[3] == n * 3 && hist[4] == n && hist[5] == n;
        return (core && hist[0] >= n * 4 && hist[6] >= n ? 1 : 0)
             + (core && hist[6] >= n * 4 && hist[0] >= n ? 1 : 0);
    }

    private int FinderPenaltyTerminateAndCount(bool curColor, int curRun, int[] hist)
    {
        if (curColor)
        {
            FinderPenaltyAddHistory(curRun, hist);
            curRun = 0;
        }
        curRun += Size;
        FinderPenaltyAddHistory(curRun, hist);
        return FinderPenaltyCountPatterns(hist);
    }

    private void FinderPenaltyAddHistory(int curRun, int[] hist)
    {
        if (hist[0] == 0) curRun += Size;
        Array.Copy(hist, 0, hist, 1, hist.Length - 1);
        hist[0] = curRun;
    }

    private int[] GetAlignmentPatternPositions()
    {
        if (Version == 1) return new int[0];
        int numAlign = Version / 7 + 2;
        int step = Version == 32 ? 26 : (Version * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2) * 2;
        var result = new int[numAlign];
        result[0] = 6;
        for (int i = result.Length - 1, pos = Size - 7; i >= 1; i--, pos -= step)
            result[i] = pos;
        return result;
    }

    private static int GetNumRawDataModules(int ver)
    {
        int result = (16 * ver + 128) * ver + 64;
        if (ver >= 2)
        {
            int numAlign = ver / 7 + 2;
            result -= (25 * numAlign - 10) * numAlign - 55;
            if (ver >= 7) result -= 36;
        }
        return result;
    }

    private static int GetNumDataCodewords(int ver, Ecc ecl)
    {
        return GetNumRawDataModules(ver) / 8
            - EccCodewordsPerBlock[(int)ecl, ver] * NumErrorCorrectionBlocks[(int)ecl, ver];
    }

    private static byte[] ReedSolomonComputeDivisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        int root = 1;
        for (int i = 0; i < degree; i++)
        {
            for (int j = 0; j < result.Length; j++)
            {
                result[j] = (byte)ReedSolomonMultiply(result[j] & 0xFF, root);
                if (j + 1 < result.Length) result[j] ^= result[j + 1];
            }
            root = ReedSolomonMultiply(root, 0x02);
        }
        return result;
    }

    private static byte[] ReedSolomonComputeRemainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (byte b in data)
        {
            int factor = (b ^ result[0]) & 0xFF;
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[result.Length - 1] = 0;
            for (int i = 0; i < result.Length; i++)
                result[i] ^= (byte)ReedSolomonMultiply(divisor[i] & 0xFF, factor);
        }
        return result;
    }

    private static int ReedSolomonMultiply(int x, int y)
    {
        int z = 0;
        for (int i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return z & 0xFF;
    }

    private static bool GetBitB(int x, int i) => ((x >> i) & 1) != 0;

    // Tables indexed by [ecl, version]. Row 0=Low, 1=Medium, 2=Quartile, 3=High.
    // Each row MUST have exactly 41 values (indices 0..40). Index 0 is a placeholder (-1).
    private static readonly sbyte[,] EccCodewordsPerBlock = {
        {-1, 7,10,15,20,26,18,20,24,30,18,20,24,26,30,22,24,28,30,28,28,28,28,30,30,26,28,26,26,26,26,28,28,28,28,28,28,28,28,28,28},
        {-1,10,16,26,18,24,16,18,22,22,26,30,22,22,24,24,28,28,26,26,26,26,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28,28},
        {-1,13,22,18,26,18,24,18,22,20,24,28,26,24,20,30,24,28,28,26,30,28,30,30,30,30,28,30,30,30,30,30,30,30,30,30,30,30,30,30,30},
        {-1,17,28,22,16,22,28,26,26,24,28,24,28,22,24,24,30,28,28,26,28,30,24,30,30,30,30,30,30,30,30,30,30,30,30,30,30,30,30,30,30}
    };

    private static readonly sbyte[,] NumErrorCorrectionBlocks = {
        {-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9,10,12,12,13,14,15,16,17,18,19,19,20,21,22,24,25,25},
        {-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9,10,10,11,13,14,16,17,17,18,20,21,23,25,26,28,29,31,33,35,37,38,40,43,45,47,49},
        {-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8,10,12,16,12,17,16,18,21,20,23,23,25,27,29,34,34,35,38,40,43,45,48,51,53,56,59,62,65,68},
        {-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8,11,11,16,16,18,16,19,21,25,25,25,34,30,32,35,37,40,42,45,48,51,54,57,60,63,66,70,74,77,81}
    };
}

public sealed class QrSegment
{
    public sealed class Mode
    {
        public static readonly Mode Numeric = new Mode(0x1, new[] { 10, 12, 14 });
        public static readonly Mode Alphanumeric = new Mode(0x2, new[] { 9, 11, 13 });
        public static readonly Mode Byte = new Mode(0x4, new[] { 8, 16, 16 });
        public int ModeBits { get; }
        private readonly int[] ccb;
        private Mode(int mb, int[] ccb) { ModeBits = mb; this.ccb = ccb; }
        public int NumCharCountBits(int ver)
        {
            return ccb[(ver + 7) / 17];
        }
    }

    public Mode ModeType { get; }
    public int NumChars { get; }
    public BitBuffer Data { get; }

    public QrSegment(Mode mode, int numCh, BitBuffer data)
    { ModeType = mode; NumChars = numCh; Data = data; }

    public static List<QrSegment> MakeSegments(string text)
    {
        var result = new List<QrSegment>();
        if (text.Length == 0) return result;
        if (IsNumeric(text)) result.Add(MakeNumeric(text));
        else if (IsAlphanumeric(text)) result.Add(MakeAlphanumeric(text));
        else result.Add(MakeBytes(System.Text.Encoding.UTF8.GetBytes(text)));
        return result;
    }

    public static QrSegment MakeBytes(byte[] data)
    {
        var bb = new BitBuffer();
        foreach (byte b in data) bb.AppendBits(b & 0xFF, 8);
        return new QrSegment(Mode.Byte, data.Length, bb);
    }

    public static QrSegment MakeNumeric(string digits)
    {
        var bb = new BitBuffer();
        for (int i = 0; i < digits.Length;)
        {
            int n = Math.Min(digits.Length - i, 3);
            bb.AppendBits(int.Parse(digits.Substring(i, n)), n * 3 + 1);
            i += n;
        }
        return new QrSegment(Mode.Numeric, digits.Length, bb);
    }

    public static QrSegment MakeAlphanumeric(string text)
    {
        var bb = new BitBuffer();
        int i;
        for (i = 0; i <= text.Length - 2; i += 2)
        {
            int t = AlphaCharset.IndexOf(text[i]) * 45 + AlphaCharset.IndexOf(text[i + 1]);
            bb.AppendBits(t, 11);
        }
        if (i < text.Length) bb.AppendBits(AlphaCharset.IndexOf(text[i]), 6);
        return new QrSegment(Mode.Alphanumeric, text.Length, bb);
    }

    public static int GetTotalBits(List<QrSegment> segs, int version)
    {
        long result = 0;
        foreach (var seg in segs)
        {
            int ccbits = seg.ModeType.NumCharCountBits(version);
            if (seg.NumChars >= (1 << ccbits)) return -1;
            result += 4L + ccbits + seg.Data.BitLength;
            if (result > int.MaxValue) return -1;
        }
        return (int)result;
    }

    private const string AlphaCharset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";
    private static bool IsNumeric(string t) { foreach (var c in t) if (c < '0' || c > '9') return false; return true; }
    private static bool IsAlphanumeric(string t) { foreach (var c in t) if (AlphaCharset.IndexOf(c) < 0) return false; return true; }
}

public sealed class BitBuffer
{
    private readonly List<bool> bits = new List<bool>();
    public int BitLength => bits.Count;
    public bool GetBit(int i) => bits[i];

    public void AppendBits(int val, int len)
    {
        if (len < 0 || len > 31 || (uint)val >> len != 0)
            throw new ArgumentException("Value out of range");
        for (int i = len - 1; i >= 0; i--) bits.Add(((val >> i) & 1) != 0);
    }

    public void AppendData(BitBuffer bb)
    {
        for (int i = 0; i < bb.BitLength; i++) bits.Add(bb.bits[i]);
    }
}