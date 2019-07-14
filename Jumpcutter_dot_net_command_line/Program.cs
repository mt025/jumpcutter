using CommandLine;
using System;
using System.IO;
using System.Linq;
using Jumpcutter_dot_net;

namespace jumpcutter_dot_net_commandline
{
    class Program
    {
        static int Main(string[] args)
        {
            var withError = false;
            Console.WriteLine();

            Options options = new Options();
            var ops = Parser.Default.ParseArguments<Options>(args).WithParsed(x => options = x);

            if (ops.Tag == ParserResultType.Parsed)
            {
                var jc = new JumpCutter(options);
                try
                {

                    try
                    {
                        jc.Process();
                    }
                    catch(FileLoadException e)
                    {
                        Console.Error.WriteLine(e);
                    }
                    catch (Exception e)
                    {
                        jc.lockfile?.Delete();
                        throw new JCException("\tUncaught Exception", e);
                    }


                }
                catch (JCException e)
                {
                    Console.Error.WriteLine("\tError: " + e.Message);
                    var ie = e.InnerException;
                    var count = 0;
                    while (ie != null)
                    {
                        count++;
                        var tabs = String.Concat(Enumerable.Repeat("\t", count));
                        Console.Error.WriteLine(tabs + "Inner Error: " + ie.Message);
                        ie = ie.InnerException;
                    }
                    withError = true;
                    jc.lockfile?.Delete();

                }
                finally
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(options.temp_dir))
                        {
                            var tempdir = new DirectoryInfo(options.temp_dir);
                            if (tempdir.Exists)
                                tempdir.Delete(true);
                        }
                    }
                    catch (Exception)
                    {
                        Console.Error.WriteLine("Failed to cleanup temp dir " + options.temp_dir);
                    }

                }

            }
            //Console.ReadKey();
            return withError ? -1 : 0;
        }




    }
}
