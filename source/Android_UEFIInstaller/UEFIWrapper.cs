using System;
using System.Runtime.InteropServices;

namespace Android_UEFIInstaller
{
    static class UEFIWrapper
    {

        [DllImport(@"Win32UEFI.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool UEFI_Init();
        [DllImport(@"Win32UEFI.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr UEFI_GetBootList();
        [DllImport(@"Win32UEFI.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr[][] UEFI_GetBootDevices();
        [DllImport(@"Win32UEFI.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool UEFI_isUEFIAvailable();
        [DllImport(@"Win32UEFI.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool UEFI_MakeMediaBootOption([MarshalAs(UnmanagedType.LPWStr)] string Description, 
                                                           [MarshalAs(UnmanagedType.LPWStr)] string DiskLetter, 
                                                           [MarshalAs(UnmanagedType.LPWStr)] string Path);

        [DllImport(@"Win32UEFI.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern int UEFI_DeleteBootOptionByDescription([MarshalAs(UnmanagedType.LPWStr)]string Description);
        /*
        [DllImport(@"Win32UEFI.dll")]
        void UEFI_MakeMediaBootOption(WCHAR* Description, WCHAR* DiskLetter, WCHAR* Path);
        
        EFI_BOOT_ORDER* UEFI_GetBootList();
        BDS_LOAD_OPTION** UEFI_GetBootDevices();
        int UEFI_GetBootCount();
        
        
        */
    }
}
