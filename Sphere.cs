using System.Collections.Generic;
using AutomaticChiselling;

public class SphereGenerator : IShapeGenerator
{
    public string Name => "Sphere";
    public string Description => "Full or hollow sphere";
    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter { Id = "radius", Label = "Radius", Default = 16, Min = 2, Max = 128 },
        new ShapeParameter { Id = "hollow", Label = "Hollow", Default = false, Type = ParameterType.Checkbox },
        new ShapeParameter { Id = "shell",  Label = "Shell thickness", Default = 2, Min = 1, Max = 16 }
    };
    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        int r = p.GetInt("radius", 16);
        bool hollow = p.GetBool("hollow");
        int shell = p.GetInt("shell", 2);
        int size = r * 2;
        var v = new bool[size, size, size];
        float cr = r - 0.5f;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                for (int z = 0; z < size; z++)
                {
                    float dx = x - cr, dy = y - cr, dz = z - cr;
                    float dist = dx * dx + dy * dy + dz * dz;
                    float rr = r * r;
                    if (dist <= rr)
                    {
                        if (hollow)
                        {
                            float innerR = (r - shell);
                            if (dist >= innerR * innerR)
                                v[x, y, z] = true;
                        }
                        else
                            v[x, y, z] = true;
                    }
                }
        return GeneratedShape.Mono(v);
    }
}
