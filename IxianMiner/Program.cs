using System;
using System.IO;
using System.Threading;

namespace IxianMiner
{
    class Program
    {
        public static bool noStart = false;
        public static bool forceShutdown = false;

        static void checkRequiredFiles()
        {
            // Special case for argon
            if (!File.Exists("libargon2.dll") && !File.Exists("libargon2.so") && !File.Exists("libargon2.dylib"))
            {
                Console.WriteLine("Missing '{0}' in the program folder. Possibly the IxianMiner archive was corrupted or incorrectly installed. Please re-download the archive from https://www.ixian.io!", "libargon2");
                Console.WriteLine("Press ENTER to exit.");
                Console.ReadLine();
                Environment.Exit(-1);
            }
        }


        static void Main(string[] args)
        {
            Console.WriteLine(string.Format("IXIAN Miner {0}\n", Config.version));
            checkRequiredFiles();

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
