using Fclp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IxianMiner
{
    class Config
    {

        public static string host = "http://localhost:8081";
        public static string poolhost = null;
        public static int threads = 4;
        public static string wallet = null;
        public static string workername = null;

        // Read-only values
        public static readonly string version = "0.6.4"; // Miner version

        private Config()
        {

        }

        private static string outputHelp()
        {
            Program.noStart = true;

            Console.WriteLine("");
            Console.WriteLine(" IxianMiner.exe [-h] [-v] [--threads 4] [--pool http://...] [--node http://...] [--wallet YOUR_WALLET_ADDRESS] [--worker YOUR_RIG_NAME]");
            Console.WriteLine("");
            Console.WriteLine("    -h\t\t\t Displays this help");
            Console.WriteLine("    -v\t\t\t Displays version");
            Console.WriteLine("    --threads\t\t Specify number of threads to use for mining (default 4)");
            Console.WriteLine("    --node\t\t Specify a node hostname and disable pool mode (default http://localhost:8081)");
            Console.WriteLine("    --pool\t\t Specify a pool hostname and enable pool mode");
            Console.WriteLine("    --wallet\t\t Specify the mining wallet when in pool mode, required when pool mode is enabled");
            Console.WriteLine("    --worker\t\t Specify the worker name when in pool mode (default IxianMiner)");
            Console.WriteLine("");
            Console.WriteLine(" IxianMiner has two modes of operation: POOL mode and NODE mode.");
            Console.WriteLine("    POOL mode connects to a specified pool and mines with the provided wallet address");
            Console.WriteLine("    NODE mode connects to an IxianDLT node directly and mines to the IxianDLT node wallet address");
            Console.WriteLine("");


            return "";
        }

        private static string outputVersion()
        {
            Program.noStart = true;

            // Do nothing since version is the first thing displayed

            return "";
        }


        public static void readFromCommandLine(string[] args)
        {
            // first pass
            var cmd_parser = new FluentCommandLineParser();

            // help
            cmd_parser.SetupHelp("h", "help").Callback(text => outputHelp());
            // version
            cmd_parser.Setup<bool>('v', "version").Callback(text => outputVersion());

            cmd_parser.Parse(args);

            if (Program.noStart)
            {
                return;
            }

            // second pass
            cmd_parser = new FluentCommandLineParser();


            cmd_parser.Setup<string>("host").Callback(value => host = value).Required();
            cmd_parser.Setup<string>("pool").Callback(value => poolhost = value).Required();
            cmd_parser.Setup<string>("node").Callback(value => host = value).Required();
            cmd_parser.Setup<int>("threads").Callback(value => threads = (int)value).Required();
            cmd_parser.Setup<string>("wallet").Callback(value => wallet = value).Required();
            cmd_parser.Setup<string>("worker").Callback(value => workername = value).Required();

            cmd_parser.Parse(args);


            // Handle potential issues
            if (threads < 1)
                threads = 1;

            if(poolhost == null && wallet != null)
            {
                Console.WriteLine("Warning! Wallet address was specified, but pool mode is not enabled!");
            }

            if(poolhost != null && wallet == null)
            {
                Console.WriteLine("Warning! Pool mode enabled, but no wallet address provided!");
            }

            // Set a default worker name if none provided
            if(workername == null)
            {
                Console.WriteLine("No workername provided, using default IxianMiner as the worker name.");
                workername = "IxianMiner";
            }

        }

    }
}
