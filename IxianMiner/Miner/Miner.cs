using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using IXICore;

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
        public int currentBlockVersion = 5;
        public ulong currentBlockDifficulty = 0; // Current block difficulty
        byte[] activeBlockChallenge = null;

        private static Random random = new Random(); // Used to seed initial curNonce
        [ThreadStatic] private static byte[] curNonce = null; // Used for random nonce
        [ThreadStatic] private static byte[] dummyExpandedNonce = null;
        [ThreadStatic] private static int lastNonceLength = 0;

        private DateTime startTime;

        int connectionFails = 0;
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
            {
                Console.WriteLine("Waiting for empty PoW block...");
            }

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
                    Thread.Sleep(5000);
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
                    Console.WriteLine("Thread Exception: {0}", e.Message);
                    checkFailover();
                    Thread.Sleep(5000);
                }
            }
        }

        private void miningThreadLoop(object data)
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
                        Thread.Sleep(500);
                    }
                    else
                    {
                        if (currentBlockVersion < 5)
                            calculatePow_v2(currentHashCeil);
                        else
                            calculatePow_v3(currentHashCeil);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Miner Thread Exception: {0}", e.Message);
                    Thread.Sleep(5000);
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

                    if (resultdata["num"] == null)
                    {
                        Thread.Sleep(1000);
                        hasBlock = false;
                        return;
                    }

                    ulong num = resultdata["num"];
                    int ver = resultdata["ver"];
                    ulong diff = resultdata["dif"];
                    byte[] block_checksum = resultdata["chk"];
                    byte[] solver_address = resultdata["adr"];

                    currentBlockNum = num;
                    currentBlockVersion = ver;
                    currentBlockDifficulty = diff;

                    Console.WriteLine("Received block: #{0} diff {1}", num, diff);

                    currentHashCeil = MiningUtils.getHashCeilFromDifficulty(currentBlockDifficulty);

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

                if (resultdata["num"] != null)
                {
                    ulong num = resultdata["num"];
                    if (num != currentBlockNum)
                    {
                        hasBlock = false;
                        return;
                    }
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

        // Checks and switches to the failover pool if necessary. Will also switch to primary pool in case the failover doesn't work.
        private void checkFailover()
        {
            // Skip failover if no secondary pool is provided
            if (Config.poolhost2 == null)
                return;

            connectionFails++;

            if(connectionFails >= 3)
            {
                connectionFails = 0;
                Console.Write("Switching active pool to: ");
                if(Config.host.Equals(Config.poolhost))
                {
                    Config.host = Config.poolhost2;
                }
                else
                {
                    Config.host = Config.poolhost;
                }
                Console.WriteLine(Config.host);
            }
        }

        // Expand a provided nonce up to expand_length bytes by appending a suffix of fixed-value bytes
        private static byte[] expandNonce(byte[] nonce, int expand_length)
        {
            if (dummyExpandedNonce == null)
            {
                dummyExpandedNonce = new byte[expand_length];
                for (int i = 0; i < dummyExpandedNonce.Length; i++)
                {
                    dummyExpandedNonce[i] = 0x23;
                }
            }

            // set dummy with nonce
            for (int i = 0; i < nonce.Length; i++)
            {
                dummyExpandedNonce[i] = nonce[i];
            }

            // clear any bytes from last nonce
            for (int i = nonce.Length; i < lastNonceLength; i++)
            {
                dummyExpandedNonce[i] = 0x23;
            }

            lastNonceLength = nonce.Length;

            return dummyExpandedNonce;
        }

        private void calculatePow_v2(byte[] hash_ceil)
        {           
            // PoW = Argon2id( BlockChecksum + SolverAddress, Nonce)
            byte[] nonce = randomNonce(64);
            byte[] hash = Argon2id.getHash(activeBlockChallenge, nonce, 1, 1024, 2);

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

        private void calculatePow_v3(byte[] hash_ceil)
        {
            // PoW = Argon2id( BlockChecksum + SolverAddress, Nonce)
            byte[] nonce_bytes = randomNonce(64);
            byte[] fullnonce = expandNonce(nonce_bytes, 234236);

            byte[] hash = Argon2id.getHash(activeBlockChallenge, fullnonce, 2, 2048, 2);
            
            if (hash.Length < 1)
            {
                Console.WriteLine("Stopping miner due to invalid hash.");
                stop();
                return;
            }

            hashesPerSecond++;

            // We have a valid hash, update the corresponding block
            if (Miner.validateHashInternal_v2(hash, hash_ceil) == true)
            {
                Console.WriteLine("SHARE FOUND FOR BLOCK {0}", currentBlockNum);

                // Broadcast the nonce to the network
                sendSolution(nonce_bytes);
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
