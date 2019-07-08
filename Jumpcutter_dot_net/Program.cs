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
           var ops = Parser.Default.ParseArguments<Options>(args).WithParsed(x=> options = x);

            if (ops.Tag == ParserResultType.Parsed)
            {

                try
                {
                    new JumpCutter(options);
                }
                catch (JCException e)
                {
                    Console.Error.WriteLine("Error: " + e.Message);
                }
                finally {
                    if (string.IsNullOrEmpty(options.temp_dir)) {
                        Directory.Delete(options.temp_dir,true);
                    }

                }
               // catch (Exception e) {
               //     Console.Error.WriteLine("Runtime Error:");
               //     Console.Error.WriteLine(e);
               // }
            }
            //Console.ReadKey();
        }




    }
}
