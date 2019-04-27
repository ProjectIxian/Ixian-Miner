using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

internal static class NativeMethods
{
    // Import the libargon2 shared library
    [DllImport("libargon2", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int argon2id_hash_raw(UInt32 time_cost, UInt32 mem_cost, UInt32 parallelism,
                             IntPtr data, UIntPtr data_len,
                             IntPtr salt, UIntPtr salt_len,
                             IntPtr output, UIntPtr output_len);

}

