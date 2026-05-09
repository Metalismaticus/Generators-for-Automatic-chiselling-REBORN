using System.Collections.Generic;
using AutomaticChiselling;

public class DomeGenerator : IShapeGenerator
{
    public string Name => "Dome";
    public string Description => "Half sphere (dome)";
    public ShapeParameter[] Parameters => new[]
    {
        new ShapeParameter { Id = "radius", Label = "Radius", Default = 16, Min = 2, Max = 128 },
        new ShapeParameter { Id = "hollow", Label = "Hollow", Default = true, Type = ParameterType.Checkbox },
        new ShapeParameter { Id = "shell",  Label = "Shell thickness", Default = 2, Min = 1, Max = 16 }
    };
    public GeneratedShape Generate(Dictionary<string, object> p)
    {
        int r = p.GetInt("radius", 16);
        bool hollow = p.GetBool("hollow", true);
        int shell = p.GetInt("shell", 2);
        int size = r * 2;
        var v = new bool[size, r, size];
        float cr = r - 0.5f;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < r; y++)
                for (int z = 0; z < size; z++)
                {
                    float dx = x - cr, dy = y, dz = z - cr;
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
