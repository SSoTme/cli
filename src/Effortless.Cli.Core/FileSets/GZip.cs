using System.IO.Compression;
using System.Text;

namespace Effortless.Cli.FileSets;

public static class GZip
{
    public static byte[] Zip(this string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            str = string.Empty;
        }

        var bytes = new UTF8Encoding(false).GetBytes(str);
        return bytes.Zip();
    }

    public static byte[] Zip(this byte[] bytes)
    {
        using (var msi = new MemoryStream(bytes))
        using (var mso = new MemoryStream())
        {
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                CopyTo(msi, gs);
            }

            return mso.ToArray();
        }
    }

    public static byte[] Unzip(this byte[] bytes)
    {
        using (var msi = new MemoryStream(bytes))
        using (var mso = new MemoryStream())
        {
            using (var gs = new GZipStream(msi, CompressionMode.Decompress))
            {
                CopyTo(gs, mso);
            }

            return mso.ToArray();
        }
    }

    public static string UnzipToString(this byte[] zippedBytes)
    {
        if (ReferenceEquals(zippedBytes, null))
        {
            return string.Empty;
        }

        var unzippedBytes = zippedBytes.Unzip();
        return Encoding.UTF8.GetString(unzippedBytes);
    }

    public static void CopyTo(Stream src, Stream dest)
    {
        byte[] bytes = new byte[4096];
        int cnt;

        while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
        {
            dest.Write(bytes, 0, cnt);
        }
    }
}
