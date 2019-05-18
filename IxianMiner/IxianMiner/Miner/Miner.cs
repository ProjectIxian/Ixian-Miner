using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IxianMiner
{
    class Miner
    {

        public bool poolMode = false; // True means this miner connects to a pool. False means it connects to an IxianDLT node.

        private long hashesPerSecond = 0; // Total number of hashes per second
        private DateTime lastStatTime; // Last statistics output time
        private bool shouldStop = false; // flag to signal shutdown of threads

        public byte[] currentHashCeil { get; private set; }
        public ulong currentBlockNum = 0; // Mining block number
        public int currentBlockVersion = 0;
        public ulong currentBlockDifficulty = 0; // Current block difficulty
        byte[] activeBlockChallenge = null;

        private static Random random = new Random(); // Used to seed initial curNonce
        [ThreadStatic] private static byte[] curNonce = null; // Used for random nonce

        private DateTime startTime;


        bool waitingForNewBlock = false;
        bool hasBlock = false;

        public ulong foundShares = 0;
        public long lastHashrate = 0; // Last hashrate used for reporting

        public Miner()
        {
            lastStatTime = DateTime.UtcNow;
            startTime = DateTime.Now;
        }

        public bool start()
        {

            if (Config.poolhost == null)
            {
                poolMode = false;
                Console.WriteLine("Starting miner in NODE mode");
            }
            else
            {
                poolMode = true;
                Console.WriteLine("Starting miner in POOL mode with wallet {0}", Config.wallet);
            }

            // Start primary thread
            Thread main_thread = new Thread(threadLoop);
            main_thread.Start();

            // Start the web service thread
            Thread webservice_thread = new Thread(webThreadLoop);
            webservice_thread.Start();

            // Handle thread count autodetect
            if(Config.threads == 0)
            {
                Config.threads = Miner.calculateMiningThreadsCount();
            }

            Console.WriteLine("Mining threads: {0}", Config.threads);

            // Start secondary miner threads
            for (int i = 0; i < Config.threads; i++)
            {
                Thread miner_thread = new Thread(miningThreadLoop);
                miner_thread.Start();
            }

            return true;
        }

        // Signals all the mining threads to stop
        public bool stop()
        {
            Console.WriteLine("Shutting down miner...");
            shouldStop = true;
            return true;
        }

        // Returns the allowed number of mining threads based on amount of logical processors detected
        public static int calculateMiningThreadsCount()
        {
            int vcpus = Environment.ProcessorCount;

            // Single logical processor detected, force one mining thread maximum
            if (vcpus <= 1)
            {
                Console.WriteLine("Single logical processor detected, forcing one mining thread maximum.");
                return 1;
            }

            // Provided mining thread count is allowed
            return vcpus;
        }

        // Output the miner status
        private void printMinerStatus()
        {
            if (hasBlock)
            {
                DateTime endTime = DateTime.Now;
                TimeSpan totalTimeTaken = endTime.Subtract(startTime);

                string time_prefix = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine("[{0}] Speed: {1} H/s\tBlock#:{2}\tDiff:{3}\tShares:{4}\tUptime: {5}", time_prefix, hashesPerSecond, currentBlockNum, currentBlockDifficulty, foundShares, totalTimeTaken.ToString(@"d\.hh\:mm\:ss"));
            }
            else
                Console.WriteLine("Waiting for empty PoW block...");

            lastStatTime = DateTime.UtcNow;
            lastHashrate = hashesPerSecond;
            hashesPerSecond = 0;
        }


        private void threadLoop(object data)
        {
            while (!shouldStop)
            {
                try
                {
                    if (waitingForNewBlock)
                    {
                        Thread.Sleep(500);
                    }

                    Thread.Sleep(300);
                    
                    // Output mining stats
                    TimeSpan timeSinceLastStat = DateTime.UtcNow - lastStatTime;
                    if (timeSinceLastStat.TotalSeconds > 1)
                    {
                        printMinerStatus();
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Exception: {0}", e.Message);
                    break;
                }
            }
        }

        private void webThreadLoop(object data)
        {
            while (!shouldStop)
            {
                try
                {
                    if (waitingForNewBlock)
                    {
                        Thread.Sleep(500);
                    }
                    if (hasBlock == false)
                    {
                        requestNewBlock();
                    }
                    else
                    {
                        checkBlockStatus();
                        Thread.Sleep(2000);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: {0}", e.Message);
                    break;
                }
            }
        }

        private void miningThreadLoop(object data)
        {
            while (!shouldStop)
            {
                if (waitingForNewBlock)
                {
                    Thread.Sleep(500);
                }
                if (hasBlock == false)
                {
                    Thread.Sleep(500);
                }
                else
                {
                    calculatePow_v2(currentHashCeil);
                }
            }
        }


        // Request a new block for mining
        private void requestNewBlock()
        {
            using (var webClient = new System.Net.WebClient())
            {
                string suffix = "";
                if (poolMode)
                    suffix = string.Format("&wallet={0}&worker={1}&hr={2}", Config.wallet, Config.workername, lastHashrate);
                var json = webClient.DownloadString(string.Format("{0}/getminingblock?algo=0{1}", Config.host, suffix));
                try
                {
                    dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                    dynamic resultdata = data["result"];

                    ulong num = resultdata["num"];
                    int ver = resultdata["ver"];
                    ulong diff = resultdata["dif"];
                    byte[] block_checksum = resultdata["chk"];
                    byte[] solver_address = resultdata["adr"];

                    currentBlockNum = num;
                    currentBlockVersion = ver;
                    currentBlockDifficulty = diff;

                    Console.WriteLine("Received block: #{0} diff {1}", num, diff);

                    currentHashCeil = getHashCeilFromDifficulty(currentBlockDifficulty);

                    activeBlockChallenge = new byte[block_checksum.Length + solver_address.Length];
                    System.Buffer.BlockCopy(block_checksum, 0, activeBlockChallenge, 0, block_checksum.Length);
                    System.Buffer.BlockCopy(solver_address, 0, activeBlockChallenge, block_checksum.Length, solver_address.Length);
                    hasBlock = true;
                }
                catch(Exception)
                {
                    Thread.Sleep(1000);
                    hasBlock = false;
                }

            }
        }

        // Check the status for the current mining block
        private void checkBlockStatus()
        {
            using (var webClient = new System.Net.WebClient())
            {
                string suffix = "";
                if (poolMode)
                    suffix = string.Format("&wallet={0}&worker={1}&hr={2}", Config.wallet, Config.workername, lastHashrate);
                string final_url = string.Format("{0}/getblock?num={1}{2}", Config.host, currentBlockNum, suffix);
                var json = webClient.DownloadString(final_url);
                dynamic data = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                dynamic resultdata = data["result"];

                ulong num = resultdata["num"];
                if(num != currentBlockNum)
                {
                    hasBlock = false;
                    return;
                }

                string powfield = resultdata["PoW field"];
                if(powfield.Length > 5)
                    hasBlock = false;
            }
        }

        // Sends the found solution
        private void sendSolution(byte[] nonce)
        {
            using (var webClient = new System.Net.WebClient())
            {
                string suffix = "";
                if (poolMode)
                    suffix = string.Format("&wallet={0}&worker={1}", Config.wallet, Config.workername);

                string final_url = string.Format("{0}/submitminingsolution?nonce={1}&blocknum={2}{3}", Config.host, hashToString(nonce), currentBlockNum, suffix);
                var json = webClient.DownloadString(final_url);
            }
        }


        private void calculatePow_v2(byte[] hash_ceil)
        {           
            // PoW = Argon2id( BlockChecksum + SolverAddress, Nonce)
            byte[] nonce = randomNonce(64);
            byte[] hash = findHash_v1(activeBlockChallenge, nonce);

            if (hash.Length < 1)
            {
                Console.WriteLine("Stopping miner due to invalid hash, potential hardware failure.");
                stop();
                return;
            }

            hashesPerSecond++;

            // We have a valid hash, update the corresponding block
            if (Miner.validateHashInternal_v2(hash, hash_ceil) == true)
            {
                Console.WriteLine("SHARE FOUND FOR BLOCK {0}", currentBlockNum);
                // Broadcast the nonce to the network
                sendSolution(nonce);
                hasBlock = false;
                foundShares++;
            }
        }


        private static bool validateHashInternal_v2(byte[] hash, byte[] hash_ceil)
        {
            if (hash == null || hash.Length < 32)
            {
                return false;
            }
            for (int i = 0; i < hash.Length; i++)
            {
                byte cb = i < hash_ceil.Length ? hash_ceil[i] : (byte)0xff;
                if (cb > hash[i]) return true;
                if (cb < hash[i]) return false;
            }
            // if we reach this point, the hash is exactly equal to the ceiling we consider this a 'passing hash'
            return true;
        }

        private static byte[] findHash_v1(byte[] data, byte[] salt)
        {
            try
            {
                byte[] hash = new byte[32];
                IntPtr data_ptr = Marshal.AllocHGlobal(data.Length);
                IntPtr salt_ptr = Marshal.AllocHGlobal(salt.Length);
                Marshal.Copy(data, 0, data_ptr, data.Length);
                Marshal.Copy(salt, 0, salt_ptr, salt.Length);
                UIntPtr data_len = (UIntPtr)data.Length;
                UIntPtr salt_len = (UIntPtr)salt.Length;
                IntPtr result_ptr = Marshal.AllocHGlobal(32);
                int result = NativeMethods.argon2id_hash_raw((UInt32)1, (UInt32)1024, (UInt32)2, data_ptr, data_len, salt_ptr, salt_len, result_ptr, (UIntPtr)32);
                Marshal.Copy(result_ptr, hash, 0, 32);
                Marshal.FreeHGlobal(data_ptr);
                Marshal.FreeHGlobal(result_ptr);
                Marshal.FreeHGlobal(salt_ptr);
                return hash;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error during mining: {0}", e.Message);
                return null;
            }
        }


        public static byte[] getHashCeilFromDifficulty(ulong difficulty)
        {
            /*
             * difficulty is an 8-byte number from 0 to 2^64-1, which represents how hard it is to find a hash for a certain block
             * the dificulty is converted into a 'ceiling value', which specifies the maximum value a hash can have to be considered valid under that difficulty
             * to do this, follow the attached algorithm:
             *  1. calculate a bit-inverse value of the difficulty
             *  2. create a comparison byte array with the ceiling value of length 10 bytes
             *  3. set the first two bytes to zero
             *  4. insert the inverse difficulty as the next 8 bytes (mind the byte order!)
             *  5. the remaining 22 bytes are assumed to be 'FF'
             */
            byte[] hash_ceil = new byte[10];
            hash_ceil[0] = 0x00;
            hash_ceil[1] = 0x00;
            for (int i = 0; i < 8; i++)
            {
                int shift = 8 * (7 - i);
                ulong mask = ((ulong)0xff) << shift;
                byte cb = (byte)((difficulty & mask) >> shift);
                hash_ceil[i + 2] = (byte)~cb;
            }
            return hash_ceil;
        }

        private byte[] randomNonce(int length)
        {
            if (curNonce == null)
            {
                curNonce = new byte[length];
                lock (random)
                {
                    random.NextBytes(curNonce);
                }
            }
            bool inc_next = true;
            length = curNonce.Length;
            for (int pos = length - 1; inc_next == true && pos > 0; pos--)
            {
                if (curNonce[pos] < 0xFF)
                {
                    inc_next = false;
                    curNonce[pos]++;
                }
                else
                {
                    curNonce[pos] = 0;
                }
            }
            return curNonce;
        }

        public static string hashToString(byte[] data)
        {
            if (data == null)
            {
                return "null";
            }
            StringBuilder hash = new StringBuilder();
            foreach (byte theByte in data)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }

        public static byte[] stringToHash(string data)
        {
            int NumberChars = data.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(data.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
