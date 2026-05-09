using System;
using System.Collections.Generic;
using AutomaticChiselling;

public class ConeGenerator : IShapeGenerator
{
    public string Name => "Cone";
    public string Description => "Cone / hollow cone";
    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter { Id = "radius", Label = "Base radius", Default = 12, Min = 2, Max = 128 },
        new ShapeParameter { Id = "height", Label = "Height",      Default = 24, Min = 2, Max = 256 },
        new ShapeParameter { Id = "hollow", Label = "Hollow",      Default = false, Type = ParameterType.Checkbox },
        new ShapeParameter { Id = "shell",  Label = "Shell thickness", Default = 2, Min = 1, Max = 16 }
    };
    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        int r = p.GetInt("radius", 12), h = p.GetInt("height", 24);
        bool hollow = p.GetBool("hollow");
        int shell = p.GetInt("shell", 2);
        int diam = r * 2;
        var v = new bool[diam, h, diam];
        float cr = r - 0.5f;
        for (int y = 0; y < h; y++)
        {
            float layerR = r * (1f - (float)y / h);
            float innerR = Math.Max(0, layerR - shell);
            for (int x = 0; x < diam; x++)
                for (int z = 0; z < diam; z++)
                {
                    float dx = x - cr, dz = z - cr;
                    float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (dist <= layerR)
                    {
                        if (hollow && dist < innerR) continue;
                        v[x, y, z] = true;
                    }
                }
        }
        return GeneratedShape.Mono(v);
    }
}
