﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Infinite_module_test
{
    static class code_utils
    {
        public static unsafe T SuperCast<T>(byte[] data) => *(T*)(*(ulong*)&data + 0x10);
        public static unsafe T SuperCast<T>(byte[] data, ulong startIndex) => *(T*)(*(ulong*)&data + (0x10 + startIndex));
        // DEBUG METHOD // DEBUG METHOD // DEBUG METHOD //
        public static unsafe T KindaSafe_SuperCast<T>(byte[] data) // double checks that the address is correct by comparing the actual size of the array with the size at he address, should not be needed.
        {
            // apparently arrays are structured differently between .net versions, great lol
            ulong data_ptr = *(ulong*)&data;
            if (*(ulong*)(data_ptr + 0x8) == (ulong)data.Length) // 0x08 is the byte count
                return *(T*)(data_ptr + 0x10); // 0x10 is the start of the actual data
            return default(T);
        }
        // DEBUG METHOD // DEBUG METHOD // DEBUG METHOD //
        public static unsafe T KindaSafe_SuperCast<T>(byte[] data, ulong startIndex)
        {
            ulong data_ptr = *(ulong*)&data;
            if (*(ulong*)(data_ptr + 0x8) != (ulong)data.Length)
                return default(T);

            // check to see if theres actually that many bytes in that array to read
            ulong struct_size = (ulong)Marshal.SizeOf(typeof(T));
            if (startIndex + struct_size > (ulong)data.Length)
                return default(T);

            return *(T*)(data_ptr + (0x10 + startIndex));
        }
        // DEBUG METHOD // DEBUG METHOD // DEBUG METHOD //
        //public static void pass_array_into_clipboard(byte[] suspect) => Clipboard.SetText(BitConverter.ToString(suspect).Replace("-", ""));


    }
}
