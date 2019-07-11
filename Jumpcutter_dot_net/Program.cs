using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumpcutter_dot_net
{
    class Program
    {
        static void Main(string[] args)
        {


            Options options = new Options();
            var ops = Parser.Default.ParseArguments<Options>(args).WithParsed(x => options = x);

            if (ops.Tag == ParserResultType.Parsed)
            {

                try
                {
                    try
                    {
                        var jc = new JumpCutter(options);
                        jc.Process();
                    }
                    catch (Exception e)
                    {
                        throw new JCException("\tUncaught Exception", e);
                    }
                    

                }
                catch (JCException e)
                {
                    Console.Error.WriteLine("\tError: " + e.Message);
                    var ie = e.InnerException;
                    var count = 0;
                    while (ie != null) {
                        count++;
                        var tabs = String.Concat(Enumerable.Repeat("\t", count));
                        Console.Error.WriteLine(tabs+"Inner Error: " + ie.Message);
                        ie = ie.InnerException;
                    }
                    
                }
                finally
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(options.temp_dir))
                        {
                            var tempdir = new DirectoryInfo(options.temp_dir);
                            if(tempdir.Exists)
                                tempdir.Delete(true);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Failed to cleanup temp dir " + options.temp_dir);
                    }

                }

            }
            //Console.ReadKey();
        }




    }
}
