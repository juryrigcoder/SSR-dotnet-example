using Newtonsoft.Json;
using Extism.Sdk;
using System.Text;
using System.Web;

namespace MyServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://localhost:3030");
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/hello/world", async context =>
                            {
                                await context.Response.WriteAsync(Ssr("<my-header>Hello World</my-header>"));
                            });
                        });
                        Console.WriteLine("Starting server on http://localhost:3030");
                        Console.WriteLine("Enhanced page at http://localhost:3030/hello/world");
                    });
                })
                .Build();

            await host.RunAsync();
        }
        private static Dictionary<string, string> ReadElements(string directory)
        {
            var elements = new Dictionary<string, string>();
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                var fileExt = Path.GetExtension(filePath).ToLower();
                if (fileExt == ".mjs" || fileExt == ".js" || fileExt == ".html")
                {
                    var content = File.ReadAllText(filePath);
                    var sanitizedContent = HttpUtility.HtmlEncode(content);
                    var key = Path.GetFileNameWithoutExtension(filePath);
                    elements[key] = sanitizedContent;
                }
            }
            return elements;
        }
        public static string Ssr(string document)
        {
            EnhanceSsrWasmResponse? response = null;
            try
            {
                string wasmPath = "../wasm/enhance-ssr.wasm";
                string wasmName = "enhance-ssr";
                var manifest = new Manifest(new PathWasmSource(wasmPath, wasmName));

                using var plugin = new Plugin(manifest, new HostFunction[] { }, withWasi: true);
                var elements = ReadElements("../elements");
                var decodedElements = new Dictionary<string, object>();
                foreach (var element in elements)
                {
                    decodedElements[element.Key] = HttpUtility.HtmlDecode(element.Value);
                }

                var decodedDocument = HttpUtility.HtmlDecode(document);
                var input = new Dictionary<string, object>
                {
                    ["markup"] = decodedDocument,
                    ["elements"] = decodedElements,
                    ["initialState"] = new InitialState { message = "Hello from initialState" }
                };

                string inputJson = JsonConvert.SerializeObject(input, Formatting.Indented);
                ReadOnlySpan<byte> resultSpan = plugin.Call("ssr", Encoding.UTF8.GetBytes(inputJson));
                byte[] resultArray = new byte[resultSpan.Length];
                resultSpan.CopyTo(resultArray);
                string output = Encoding.UTF8.GetString(resultArray);
                response = JsonConvert.DeserializeObject<EnhanceSsrWasmResponse>(output);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in Ssr method: {e.Message}");
                return "<html><body><h1>Error processing HTML content</h1></body></html>";
            }
            return response?.Document ?? "<html><body><h1>No document returned from plugin</h1></body></html>";
        }
    }
    public class InitialState
    {
        public string message { get; set; }
    }
    public class EnhanceSsrWasmResponse
    {
        public string Document { get; set; }
    }
}
