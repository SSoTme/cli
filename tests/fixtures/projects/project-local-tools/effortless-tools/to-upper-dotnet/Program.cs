// Dotnet-shape local tool. Deliberately dependency-free (HttpListener instead
// of CLIClassLibrary) so the fixture restores offline; the shape is the same
// one every published cloud tool has: read PORT, answer POST / with a
// TranspilePayload response, GET / for health.
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
var listener = new HttpListener();
listener.Prefixes.Add($"http://127.0.0.1:{port}/");
listener.Start();
Console.WriteLine($"to-upper-dotnet listening on http://127.0.0.1:{port}/");

while (true)
{
    var context = listener.GetContext();
    try
    {
        if (context.Request.HttpMethod == "GET")
        {
            Write(context.Response, 200, """{"status":"healthy","tool":"to-upper-dotnet"}""");
            continue;
        }

        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var payload = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
        var zipped = payload["transpileRequest"]?["zippedInputFileSet"]?.GetValue<string>()
            ?? throw new InvalidOperationException("no zippedInputFileSet");
        var inputXml = Gunzip(Convert.FromBase64String(zipped));
        var first = XDocument.Parse(inputXml).Descendants("FileSetFile").First();
        var text = first.Element("FileContents")?.Value
            ?? Gunzip(Convert.FromBase64String(
                (first.Element("ZippedTextFileContents") ?? first.Element("ZippedFileContents"))!.Value));
        var outputName = payload["cliOutput"]?.GetValue<string>();
        var outputXml = new XDocument(
            new XElement("FileSet",
                new XElement("FileSetFiles",
                    new XElement("FileSetFile",
                        new XElement("RelativePath", string.IsNullOrEmpty(outputName) ? "Output.txt" : outputName),
                        new XElement("FileContents", text.ToUpperInvariant()),
                        new XElement("AlwaysOverwrite", "true"))))).ToString();
        var response = new JsonObject
        {
            ["TranspileRequest"] = new JsonObject { ["ZippedOutputFileSet"] = Convert.ToBase64String(Gzip(outputXml)) },
            ["Transpiler"] = new JsonObject { ["Name"] = "to-upper-dotnet", ["LowerHyphenName"] = "to-upper-dotnet" },
            ["Logs"] = new JsonArray(new JsonObject { ["Level"] = "message", ["Text"] = $"to-upper-dotnet saw {payload["cliParams"]?.AsArray().Count ?? 0} param(s)" }),
            ["Exception"] = null,
        };
        Write(context.Response, 200, response.ToJsonString());
    }
    catch (Exception exception)
    {
        Write(context.Response, 200, new JsonObject
        {
            ["Transpiler"] = new JsonObject { ["Name"] = "to-upper-dotnet" },
            ["Logs"] = new JsonArray(),
            ["Exception"] = new JsonObject { ["Message"] = exception.Message },
        }.ToJsonString());
    }
}

static void Write(HttpListenerResponse response, int status, string json)
{
    var bytes = Encoding.UTF8.GetBytes(json);
    response.StatusCode = status;
    response.ContentType = "application/json";
    response.ContentLength64 = bytes.Length;
    response.OutputStream.Write(bytes);
    response.Close();
}

static string Gunzip(byte[] bytes)
{
    using var input = new MemoryStream(bytes);
    using var gzip = new GZipStream(input, CompressionMode.Decompress);
    using var output = new MemoryStream();
    gzip.CopyTo(output);
    return Encoding.UTF8.GetString(output.ToArray());
}

static byte[] Gzip(string text)
{
    using var output = new MemoryStream();
    using (var gzip = new GZipStream(output, CompressionMode.Compress))
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        gzip.Write(bytes);
    }

    return output.ToArray();
}
