using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace PreparePayload
{
    class Program
    {
        /// <summary>
        /// Manipulates the given assembly so it can be loaded by the Execute-Assembly-Safe
        /// </summary>
        /// <param name="assemblyPath">Path to target assembly</param>
        /// <param name="destinationPath">Path to PathcETW projects favicon.ico</param>
        public static void Main(string[] args)
        {
            try
            {
                if (args.Count() != 2)
                    throw new Exception("You must give both arguments: Target Assembly and the path to Execute-Assembly-Safe favicon.ico!");

                string assemblyPath = args[0];
                string destinationPath = args[1];

                if (assemblyPath == null || assemblyPath == "")
                {
                    throw new Exception("You must give a path to the executable you wan't to use!");
                }
                if (destinationPath == null || destinationPath == "")
                {
                    throw new Exception("You must give a path where the manipulated assembly shall be written!");
                }

                if (!File.Exists(assemblyPath) || (File.GetAttributes(assemblyPath) & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    throw new Exception($"Given path was invalid! Path was: {assemblyPath}");
                }

                //Read binary
                Console.WriteLine($"[+] Reading target binary: {assemblyPath}");
                byte[] assemblyBin = File.ReadAllBytes(assemblyPath);

                if (assemblyBin == null || assemblyBin.Length <= 1)
                {
                    throw new Exception($"Unable to read file from path: {assemblyPath}");
                }

                //deflate
                byte[] compressed = Helper.deflateCompress(assemblyBin);

                //Base64 encode
                string encoded = Convert.ToBase64String(compressed);

                //Compress and write out
                Helper.gZIPCompress(encoded, destinationPath);
                Console.WriteLine($"[+] Wrote the manipulated assembly file to: {destinationPath}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] Error: {e.Message}");
            }
            finally
            {
                Console.Write("Press enter key to exit");
                Console.ReadLine();
            }
            
        }
    }

    /// <summary>
    /// Taken from NoAmci project: https://github.com/med0x2e/NoAmci
    /// </summary>
    static class Helper
    {
        public static byte[] deflateCompress(byte[] data)
        {
            byte[] compressedArray = null;
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress))
                    {
                        deflateStream.Write(data, 0, data.Length);
                    }
                    compressedArray = memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Deflate Compress Error: " + ex.Message);
                return null;
            }
            return compressedArray;
        }
        public static void gZIPCompress(string assembly, string destinationFile)
        {

            byte[] buffer = null;
            MemoryStream sourceStream = null;
            FileStream destinationStream = null;
            GZipStream compressedStream = null;
            try
            {

                byte[] assemblyBytes = Encoding.UTF8.GetBytes(assembly);
                sourceStream = new MemoryStream(assemblyBytes);

                buffer = new byte[sourceStream.Length];
                int checkCounter = sourceStream.Read(buffer, 0, buffer.Length);
                if (checkCounter != buffer.Length)
                {
                    throw new ApplicationException();
                }

                destinationStream = new FileStream(destinationFile, FileMode.OpenOrCreate, FileAccess.Write);
                compressedStream = new GZipStream(destinationStream, CompressionMode.Compress, true);
                compressedStream.Write(buffer, 0, buffer.Length);
            }
            catch (ApplicationException ex)
            {
                Console.WriteLine("Error occured during compression" + ex.Message);
            }
            finally
            {
                if (sourceStream != null)
                    sourceStream.Close();

                if (compressedStream != null)
                    compressedStream.Close();

                if (destinationStream != null)
                    destinationStream.Close();
            }
        }

        public static byte[] deflateDecompress(byte[] data)
        {
            byte[] decompressedArray = null;
            try
            {
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    using (MemoryStream compressStream = new MemoryStream(data))
                    {
                        using (DeflateStream deflateStream = new DeflateStream(compressStream, CompressionMode.Decompress))
                        {
                            deflateStream.CopyTo(decompressedStream);
                        }
                    }
                    decompressedArray = decompressedStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nDeflate Compress Error: " + ex.Message);
                return null;
            }

            return decompressedArray;
        }
        public static byte[] gzipDecompress(Stream sourceStream)
        {

            GZipStream decStream = null;
            byte[] tempBuffer = null;

            try
            {

                decStream = new GZipStream(sourceStream, CompressionMode.Decompress, true);

                tempBuffer = new byte[4];
                int position = (int)sourceStream.Length - 4;
                sourceStream.Position = position;

                sourceStream.Read(tempBuffer, 0, 4);

                sourceStream.Position = 0;

                int length = BitConverter.ToInt32(tempBuffer, 0);
                byte[] buffer = new byte[length + 100];
                int offset = 0;

                while (true)
                {
                    int bytesRead = decStream.Read(buffer, offset, 100);

                    if (bytesRead == 0)
                        break;

                    offset += bytesRead;
                }

                return buffer;
            }
            catch (ApplicationException ex)
            {
                Console.WriteLine("Error occured during decompression" + ex.Message);
                return null;
            }

            finally
            {
                if (sourceStream != null)
                    sourceStream.Close();

                if (decStream != null)
                    decStream.Close();
            }
        }
        public static void CopyTo(this Stream source, Stream destination, int bufferSize = 81920)
        {
            byte[] array = new byte[bufferSize];
            int count;
            while ((count = source.Read(array, 0, array.Length)) != 0)
            {
                destination.Write(array, 0, count);
            }
        }


    }
}
