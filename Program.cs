using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace HTTPOverTcp
{
    class Program
    {
        static void Main(string[] args)
        {
            string token = @"ZXhwaXJhdGlvbj0xOTkwLTAxLTAxVDAxOjAxOjAxLjAwMDAwMDFaO3ZlcnNpb249djE=!aaaaaaaaaaaaaaaaaaaaaaa!c2l0ZT1fX25vbmVfXzt3b3JrZXJzPV9fbm9uZV9f";
            ReadFromTcp("localhost", 80, "", "~5thsite", token).Wait();
        }

        static async Task ReadFromTcp(string serverIp, int serverPort, string requestPath, string host, string token)
        {
            TcpClient tcpClient = null;
            NetworkStream networkStream = null;

            StringBuilder requestHeaders = new StringBuilder();


            requestHeaders.AppendLine(string.Format(@"GET /{0} HTTP/1.1", requestPath));
            requestHeaders.AppendLine("Connection: Keep-Alive");
            requestHeaders.AppendLine("Accept: text/html, application/xhtml+xml, */*");
            requestHeaders.AppendLine("Accept-Language: en-US,en;q=0.5");
            requestHeaders.AppendLine(string.Format("Host: {0}", host));
            requestHeaders.AppendLine("Max-Forwards: 10");
            requestHeaders.AppendLine("User-Agent: AlwaysOn");
            requestHeaders.AppendLine(string.Format("MWH-SecurityToken: {0}", token));
            requestHeaders.AppendLine();

            try
            {
                tcpClient = new TcpClient();

                Console.WriteLine("Connecting to {0}:{1}", serverIp, serverPort);
                await tcpClient.ConnectAsync(serverIp, serverPort);

                networkStream = tcpClient.GetStream();

                Console.WriteLine("Making HTTP request with host header: {0}", host);

                //string request = string.Format(VnetProxyPingRequestTemplate, host, token);

                string request = requestHeaders.ToString();
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(request);

                await networkStream.WriteAsync(data, 0, data.Length);

                string response = await ReceiveData(networkStream);

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception : {0}", e.ToString());
            }
            finally
            {
                if (tcpClient != null)
                {
                    tcpClient.Dispose();
                }

                if (networkStream != null)
                {
                    networkStream.Dispose();
                }
            }
        }

        static async Task<string> ReceiveData(Stream readStream)
        {
            string result;

            const int ONEMB = 1024 * 1024;
            byte[] buffer = new byte[ONEMB];
            int offset = 0;
            int read = 0;

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Reading the response message: ");

            using (MemoryStream memoryStream = new MemoryStream())
            {
                while ((read = await readStream.ReadAsync(buffer, 0, ONEMB)) != 0)
                {
                    await memoryStream.WriteAsync(buffer, offset, read);
                    offset = offset + read;

                    if (read < ONEMB || read == 0)
                    {
                        break;
                    }
                }


                var data = memoryStream.ToArray();

                var index = BinaryMatch(data, Encoding.ASCII.GetBytes("\r\n\r\n")) + 4;
                var headers = Encoding.ASCII.GetString(data, 0, index);
                memoryStream.Position = index;

                Console.WriteLine("Logging response headers.");

                Dictionary<string, string> parsedHeader = ParseHeader(headers);
                foreach(string key in parsedHeader.Keys)
                {
                    Console.WriteLine("{0}: {1}", key, parsedHeader[key]);
                }

                // If gzip encoded
                if (parsedHeader.ContainsKey("Content-Encoding") && parsedHeader["Content-Encoding"].Equals("gzip", StringComparison.OrdinalIgnoreCase))
                {
                    using (GZipStream decompressionStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                    {
                        using (var decompressedMemory = new MemoryStream())
                        {
                            decompressionStream.CopyTo(decompressedMemory);
                            decompressedMemory.Position = 0;
                            result = Encoding.UTF8.GetString(decompressedMemory.ToArray());
                        }
                    }
                }
                else
                {
                    result = Encoding.UTF8.GetString(data, index, data.Length - index);
                }
            }


            Console.WriteLine("Response Body: \n{0}", result);

            return result;
        }

        private static int BinaryMatch(byte[] input, byte[] pattern)
        {
            int sLen = input.Length - pattern.Length + 1;
            for (int i = 0; i < sLen; ++i)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; ++j)
                {
                    if (input[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            return -1;
        }

        private static Dictionary<string, string> ParseHeader(string headers)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            
            // Split the headers line by line
            string[] headersList = headers.Split(new string[] { "\r\n" }, StringSplitOptions.None);

            if (headersList.Length <= 0)
            {
                return result;
            }

            // Get the status code from the HTTP header
            string[] statusMessage = headersList[0].Split(new char[] { ' ' } );

            string statusCode = "";
            if (statusMessage.Length >= 2)
            {
                statusCode = statusMessage[1];
            }

            // Add the status code to the dictionary
            result.Add("HttpStatus", statusCode);

            // Parse the remaining headers
            for (int index = 1; index < headersList.Length; index++)
            {
                string header = headersList[index];
                
                // Split the header key and vaule on ": "
                string[] headerKeyValue = header.Split(new string[] { ": " }, StringSplitOptions.None);

                if (headerKeyValue.Length < 2)
                {
                    continue;
                }

                // In case the header value also contains ": ", concatenate all the values to a single string
                string value = "";
                for (int i = 1; i < headerKeyValue.Length; i++)
                {
                    value = value + headerKeyValue[i];
                }

                result.Add(headerKeyValue[0], value);
            }

            return result;
        }
    }
}
