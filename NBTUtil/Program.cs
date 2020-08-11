using NBTUtil.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NBTUtil
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleRunner runner = new ConsoleRunner();
            runner.Run(args);

            Console.Out.WriteLine("End of program!");
        }
    }
}
