using System.Collections.Generic;
using AutomaticChiselling;

public class CubeGenerator : IShapeGenerator
{
    public string Name => "Cube";
    public string Description => "Box / hollow box";
    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter { Id = "width",  Label = "Width",  Default = 32, Min = 1, Max = 256 },
        new ShapeParameter { Id = "height", Label = "Height", Default = 32, Min = 1, Max = 256 },
        new ShapeParameter { Id = "depth",  Label = "Depth",  Default = 32, Min = 1, Max = 256 },
        new ShapeParameter { Id = "hollow", Label = "Hollow", Default = false, Type = ParameterType.Checkbox },
        new ShapeParameter { Id = "shell",  Label = "Shell thickness", Default = 2, Min = 1, Max = 32 }
    };
    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        int w = p.GetInt("width"), h = p.GetInt("height"), d = p.GetInt("depth");
        bool hollow = p.GetBool("hollow");
        int shell = p.GetInt("shell", 2);
        var v = new bool[w, h, d];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                {
                    if (hollow && x >= shell && x < w - shell &&
                        y >= shell && y < h - shell &&
                        z >= shell && z < d - shell)
                        continue;
                    v[x, y, z] = true;
                }
        return GeneratedShape.Mono(v);
    }
}
