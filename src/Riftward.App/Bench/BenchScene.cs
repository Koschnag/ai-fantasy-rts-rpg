namespace Riftward.App.Bench;

/// <summary>
/// Technisches Testmuster der leeren Szene (T-020): ein Dreieck um den
/// Ursprung in Weltkoordinaten. Kein Spielinhalt und kein Shipping-Asset;
/// dasselbe feste Vertexlayout wie das T-010-Skelett (pos 3xf32 + color0
/// 4xu8, Stride 16 Bytes).
/// </summary>
public static class BenchScene
{
    public static byte[] Vertices { get; } = Build();

    public const int TriangleCount = 1;

    private static byte[] Build()
    {
        var data = new byte[3 * 16];

        Span<float> positions =
        [
             0.0f,  1.2f, 0f,
            -1.2f, -0.8f, 0f,
             1.2f, -0.8f, 0f,
        ];

        ReadOnlySpan<byte> colors = [230, 80, 70, 255, 90, 200, 120, 255, 70, 130, 240, 255];

        for (var vertexIndex = 0; vertexIndex < 3; vertexIndex++)
        {
            var offset = vertexIndex * 16;

            for (var component = 0; component < 3; component++)
            {
                BitConverter.GetBytes(positions[(vertexIndex * 3) + component]).CopyTo(data.AsSpan(offset + (component * 4)));
            }

            colors.Slice(vertexIndex * 4, 4).CopyTo(data.AsSpan(offset + 12));
        }

        return data;
    }
}
