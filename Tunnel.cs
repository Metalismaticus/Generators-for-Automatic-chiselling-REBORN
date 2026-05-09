using System;
using System.Collections.Generic;
using AutomaticChiselling;

public class TunnelGenerator : IShapeGenerator
{
    public string Name => "Tunnel";
    public string Description => "Tunnel with arch ceiling";
    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter { Id = "width",  Label = "Width",  Default = 12, Min = 4, Max = 64  },
        new ShapeParameter { Id = "height", Label = "Height", Default = 12, Min = 4, Max = 64  },
        new ShapeParameter { Id = "depth",  Label = "Length", Default = 32, Min = 1, Max = 256 },
        new ShapeParameter { Id = "shell",  Label = "Wall thickness", Default = 2, Min = 1, Max = 8 }
    };
    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        int w = p.GetInt("width", 12), h = p.GetInt("height", 12), d = p.GetInt("depth", 32);
        int shell = p.GetInt("shell", 2);
        var v = new bool[w, h, d];
        float halfW = w / 2f;
        int archStartY = h / 2;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                {
                    bool isWall = false;
                    if (x < shell || x >= w - shell) isWall = true;
                    if (y >= archStartY)
                    {
                        float dx = x - (halfW - 0.5f);
                        float dy = y - archStartY;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        float outerR = halfW;
                        float innerR = halfW - shell;
                        if (dist <= outerR && dist >= innerR) isWall = true;
                        if (dist > outerR) isWall = false;
                    }
                    if (y < shell) isWall = true;
                    if (isWall) v[x, y, z] = true;
                }
        return GeneratedShape.Mono(v);
    }
}
