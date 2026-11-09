using System;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;

namespace SWTORCombatParser.Utilities;

internal static class StableHash
{
    // Local cache and run IDs must work without OpenSSL, and must retain the
    // exact SHA-256 format used by existing caches and the server.
    internal static string Sha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var digest = new Sha256Digest();
        digest.BlockUpdate(bytes, 0, bytes.Length);
        var result = new byte[digest.GetDigestSize()];
        digest.DoFinal(result, 0);
        return Convert.ToHexString(result);
    }
}
