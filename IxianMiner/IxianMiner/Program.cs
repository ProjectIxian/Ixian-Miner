using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IxianMiner
{
    class Program
    {
        public static bool noStart = false;
        public static bool forceShutdown = false;

        static void Main(string[] args)
        {
            Console.WriteLine(string.Format("IXIAN Miner {0}\n", Config.version));
            Console.WriteLine("Press Escape or Ctrl-C to stop the miner\n");

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                forceShutdown = true;
            };

            // Read configuration from command line
            Config.readFromCommandLine(args);

            if (noStart)
            {
                return;
            }

            Miner miner = new Miner();

            miner.start();

            while (forceShutdown == false)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey();

                    if (key.Key == ConsoleKey.Escape)
                    {
                        forceShutdown = true;
                    }

                }
                Thread.Sleep(300);
            }

            miner.stop();
        }
    }
}
