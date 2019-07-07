using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumpcutter_dot_net
{
    class Program
    {
        static void Main(string[] args)
        {


           Arguments options = new Arguments();
           var ops = Parser.Default.ParseArguments<Arguments>(args).WithParsed(x=> options = x);

            if (ops.Tag == ParserResultType.Parsed)
            {

                try
                {
                    new JumpCutter(options);
                }
                catch (JCException e)
                {
                    Console.Error.WriteLine("Error: "  + e.Message);
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
