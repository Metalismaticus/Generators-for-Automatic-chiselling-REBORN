using System.Collections.Generic;
using AutomaticChiselling;

public class WallGenerator : IShapeGenerator
{
    public string Name => "Wall";
    public string Description => "Flat wall";
    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter { Id = "width",  Label = "Width",  Default = 32, Min = 1, Max = 256 },
        new ShapeParameter { Id = "height", Label = "Height", Default = 16, Min = 1, Max = 256 },
        new ShapeParameter { Id = "depth",  Label = "Depth",  Default = 1,  Min = 1, Max = 64  }
    };
    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        int w = p.GetInt("width"), h = p.GetInt("height"), d = p.GetInt("depth");
        var v = new bool[w, h, d];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    v[x, y, z] = true;
        return GeneratedShape.Mono(v);
    }
}
