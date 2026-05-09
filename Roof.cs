using System.Collections.Generic;
using AutomaticChiselling;

public class RoofGenerator : IShapeGenerator
{
    public string Name => "Roof";
    public string Description => "Triangular roof";
    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter { Id = "width",  Label = "Width",  Default = 20, Min = 4, Max = 128 },
        new ShapeParameter { Id = "height", Label = "Height", Default = 10, Min = 2, Max = 64  },
        new ShapeParameter { Id = "depth",  Label = "Depth",  Default = 30, Min = 1, Max = 256 }
    };
    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        int w = p.GetInt("width", 20), h = p.GetInt("height", 10), d = p.GetInt("depth", 30);
        var v = new bool[w, h, d];
        for (int y = 0; y < h; y++)
        {
            int inset = (int)((float)y / h * (w / 2f));
            for (int x = inset; x < w - inset; x++)
                for (int z = 0; z < d; z++)
                    v[x, y, z] = true;
        }
        return GeneratedShape.Mono(v);
    }
}
