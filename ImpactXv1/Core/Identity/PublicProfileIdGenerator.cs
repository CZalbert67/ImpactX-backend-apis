using System.Security.Cryptography;

namespace ImpactX.Core.Identity;

public static class PublicProfileIdGenerator
{
    public const string Prefix = "IX-";

    public static string Generate()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Prefix + Base32Encode(bytes);
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new System.Text.StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(alphabet[(buffer >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            output.Append(alphabet[(buffer << (5 - bits)) & 0x1F]);
        }

        return output.ToString();
    }
}
