using System.Collections.Generic;
using AutomaticChiselling;

public class ColumnGenerator : IShapeGenerator
{
    public string Name => "Column";
    public string Description => "Round column with base and capital";
    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter { Id = "radius", Label = "Radius", Default = 4,  Min = 2, Max = 32  },
        new ShapeParameter { Id = "height", Label = "Height", Default = 32, Min = 4, Max = 256 }
    };
    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        int r = p.GetInt("radius", 4), h = p.GetInt("height", 32);
        int diam = (r + 2) * 2;
        var v = new bool[diam, h, diam];
        float cr = diam / 2f - 0.5f;
        for (int y = 0; y < h; y++)
        {
            float layerR = (y < 2 || y >= h - 2) ? r + 1.5f : r;
            for (int x = 0; x < diam; x++)
                for (int z = 0; z < diam; z++)
                {
                    float dx = x - cr, dz = z - cr;
                    if (dx * dx + dz * dz <= layerR * layerR)
                        v[x, y, z] = true;
                }
        }
        return GeneratedShape.Mono(v);
    }
}
