using System;
using System.Collections.Generic;
using AutomaticChiselling;

public class ArchGenerator : IShapeGenerator
{
    public string Name => "Arch";
    public string Description => "Arch (rectangular, rounded, or circular)";
    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter { Id = "width",  Label = "Width",  Default = 16, Min = 3, Max = 128 },
        new ShapeParameter { Id = "height", Label = "Height", Default = 24, Min = 3, Max = 128 },
        new ShapeParameter { Id = "depth",  Label = "Depth",  Default = 4,  Min = 1, Max = 64  },
        new ShapeParameter { Id = "archType", Label = "Arch shape (0-2)", Default = 1, Min = 0, Max = 2 }
    };
    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        int w = p.GetInt("width", 16), h = p.GetInt("height", 24), d = p.GetInt("depth", 4);
        int archType = p.GetInt("archType", 1);
        var v = new bool[w, h, d];
        float halfW = w / 2f;
        int archStartY = archType == 0 ? h - 1 : h - (int)halfW;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                {
                    if ((x < d || x >= w - d) && y < archStartY)
                    {
                        v[x, y, z] = true;
                        continue;
                    }
                    if (y >= archStartY)
                    {
                        float dx = x - (halfW - 0.5f);
                        float dy = y - archStartY;
                        float archR = halfW;
                        if (archType == 0)
                        {
                            if (x < d || x >= w - d || y >= h - d)
                                v[x, y, z] = true;
                        }
                        else
                        {
                            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (dist <= archR && dist >= archR - d)
                                v[x, y, z] = true;
                        }
                    }
                }
        return GeneratedShape.Mono(v);
    }
}
