using Fclp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IxianMiner
{
    class Config
    {

        public static string host = "http://localhost:8081";
        public static string poolhost = null; // Primary pool hostname
        public static string poolhost2 = null; // Secondary pool hostname
        public static int threads = 0;  // 0 means automatically detect and apply maximum thread count
        public static string wallet = null;
        public static string workername = null;

        // Read-only values
        public static readonly string version = "0.6.5"; // Miner version

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
            Console.WriteLine("    --threads\t\t Specify number of threads to use for mining. By default it auto-detects the maximum number of threads.");
            Console.WriteLine("    --node\t\t Specify a node hostname and disable pool mode (default http://localhost:8081)");
            Console.WriteLine("    --pool\t\t Specify a pool hostname and enable pool mode");
            Console.WriteLine("    --pool2\t\t Specify a secondary pool hostname");
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
            cmd_parser.Setup<string>("pool2").Callback(value => poolhost2 = value).Required();
            cmd_parser.Setup<string>("node").Callback(value => host = value).Required();
            cmd_parser.Setup<int>("threads").Callback(value => threads = (int)value).Required();
            cmd_parser.Setup<string>("wallet").Callback(value => wallet = value).Required();
            cmd_parser.Setup<string>("worker").Callback(value => workername = value).Required();

            cmd_parser.Parse(args);


            // Handle potential issues
            if (threads < 1)
                threads = 0; // Default back to autodetect

            if(poolhost == null && wallet != null)
            {
                Console.WriteLine("Warning! Wallet address was specified, but pool mode is not enabled!");
            }

            // Handle pool-mode configuration
            if (poolhost != null)
            {
                // Set a default worker name if none provided
                if (workername == null)
                {
                    Console.WriteLine("No workername provided, using default IxianMiner as the worker name.");
                    workername = "IxianMiner";
                }

                if (wallet == null)
                {
                    Console.WriteLine("Error! Pool mode enabled, but no wallet address provided!");
                    Program.noStart = true;
                    return;
                }

                if (!validateChecksum(Base58Check.Base58CheckEncoding.DecodePlain(wallet)))
                {
                    Console.WriteLine("Error! Pool mode enabled, but invalid wallet address provided!");
                    Program.noStart = true;
                    return;
                }

                // Set the pool host
                host = poolhost;
                Console.WriteLine("Selected POOL: {0}", host);

            }
        }

        /// <summary>
        ///  Computes a (SHA512)^2 value of the given data. It is possible to calculate the hash for a subset of the input data by
        ///  using the `offset` and `count` parameters.
        /// </summary>
        /// <remarks>
        ///  The term (SHA512)^2 in this case means hashing the value twice - e.g. using SHA512 again on the computed hash value.
        /// </remarks>
        /// <param name="data">Source data for hashing.</param>
        /// <param name="offset">Byte offset into the data. Default = 0</param>
        /// <param name="count">Number of bytes to use in the calculation. Default, 0, means use all available bytes.</param>
        /// <returns>SHA256 squared hash of the input data.</returns>
        public static byte[] sha512sq(byte[] data, int offset = 0, int count = 0)
        {
#if USEBC || WINDOWS_UWP || NETCORE
			/*Sha512Digest sha = new Sha512Digest();
			sha.BlockUpdate(data, offset, count);
			byte[] rv = new byte[64];
			sha.DoFinal(rv, 0);
			sha.BlockUpdate(rv, 0, rv.Length);
			sha.DoFinal(rv, 0);
			return new uint256(rv);*/
#else
            using (var sha = new SHA512Managed())
            {
                if (count == 0)
                {
                    count = data.Length - offset;
                }
                var h = sha.ComputeHash(data, offset, count);
                return sha.ComputeHash(h, 0, h.Length);
            }
#endif
        }

        /// <summary>
        ///  Computes a trunc(N, (SHA512)^2) value of the given data. It is possible to calculate the hash for a subset of the input data by
        ///  using the `offset` and `count` parameters.
        /// </summary>
        /// <remarks>
        ///  The term (SHA512)^2 in this case means hashing the value twice - e.g. using SHA512 again on the computed hash value.
        ///  The trunc(N, X) function represents taking only the first `N` bytes of the byte-field `X`.
        /// </remarks>
        /// <param name="data">Source data for hashing.</param>
        /// <param name="offset">Byte offset into the data. Default = 0</param>
        /// <param name="count">Number of bytes to use in the calculation. Default, 0, means use all available bytes.</param>
        /// <param name="hash_length">Number of bytes to keep from the truncated hash.</param>
        /// <returns>SHA256 squared and truncated hash of the input data.</returns>
        public static byte[] sha512sqTrunc(byte[] data, int offset = 0, int count = 0, int hash_length = 44)
        {
            byte[] shaTrunc = new byte[hash_length];
            Array.Copy(sha512sq(data, offset, count), shaTrunc, hash_length);
            return shaTrunc;
        }

        /// <summary>
        ///  Validates that the given value is a valid Address by checking the embedded checksum.
        /// </summary>
        /// <remarks>
        ///  This function accepts only the final address bytes, not a public key + nonce pair. If you are generating an Address from 
        ///  public key + nonce, the Address constructor will automatically embed the correct checksum, so testing it here would be pointless.
        /// </remarks>
        /// <param name="address">Bytes of an Ixian Address.</param>
        /// <returns>True, if the value is a valid Address.</returns>
        public static bool validateChecksum(byte[] address)
        {
            try
            {
                // Check the address length
                if (address.Length < 4 || address.Length > 128)
                {
                    return false;
                }
                int version = address[0];
                int raw_address_len = address.Length - 3;
                byte[] in_chk = address.Skip(raw_address_len).Take(3).ToArray();

                byte[] checksum = sha512sqTrunc(address, 0, raw_address_len, 3);

                if (checksum.SequenceEqual(in_chk))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // If any exception occurs, the checksum is invalid
                return false;
            }

            // Checksums don't match
            return false;
        }
    }
}
