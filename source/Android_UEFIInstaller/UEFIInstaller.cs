using System;
using System.IO;
using System.Linq;

namespace Android_UEFIInstaller
{
    class UEFIInstaller : BasicInstaller
    {
        enum ErrorCodes
        {
            MOUNT_EFI_PARTITION,
            INSTALL_BOOT,
            UEFI_ENTRY
        }
        
         /* 
             * 
             * Create dirs
             * Install Android Files
             * Make data.img
             * Install Boot files
             * Create UEFI Entry
             * cleanup temp data
             * 
             * #####
             * Folders:
             *           :\Android
             *          Z:\EFI\Android
             * Files:
             *           :\Android\kernel    
             *           :\Android\initrd    
             *           :\Android\gearlock
             *           :\Android\system.img
             *           :\Android\data.img
             *           
             * 
             *           :\EFI\Android\grubx64.efi
             *           :\EFI\Android\grub.cfg
             */
             
        protected override bool InstallBootObjects(object extraData,string installDir)
        {
            string InstallDir = installDir;
            string EFI_DIR = config.UEFI_PARTITION_MOUNTPOINT + config.UEFI_BOOT;
            string BOOT_DIR = config.UEFI_PARTITION_MOUNTPOINT + config.BOOT_GRUB;

            Log.write("===Installing Boot Objects===");

            if (!MountFirmwarePartition())
                return false;

            if (!CreateBootDirectory(EFI_DIR))
                return false;

            CreateVersionInfo((string)extraData, installDir);

            if (!CopyBootFiles(InstallDir,EFI_DIR))
                return false;

            if (!CopyBootGrubFiles(BOOT_DIR))
                return false;

            if (!CreateUEFIBootOption(config.UEFI_PARTITION_MOUNTPOINT))
                return false;          

           if (!UnMountFirmwarePartition())
                return false;

            return true;
        }

        private void CreateVersionInfo(string ISOFilePath, string ExtractDirector)
        {
            string fileName = ISOFilePath.Split('\\').Last().Replace(".iso", "");

            string[] parts = fileName.Split('-');

            int BUILD = 0;    //20230614
            string ANDROIDVERS = ""; //v15.8.6
            string BUILDTYPE = ""; //gapps or foss

            foreach (string part in parts)
            {
                if (part.StartsWith("v")||part.Contains("."))
                {
                    ANDROIDVERS = part.Replace(".","");     
                }else
                    if (part.Contains("gapp") || part.Contains("foss"))
                {
                    BUILDTYPE = part;      
                }
                else
                 if (part.Length>=5)
                 {
                    int.TryParse(part, out BUILD);
                 }
            }

            if(ANDROIDVERS!="" && BUILDTYPE != "" && BUILD > 0)
            {
                string output = string.Format("{0};{1};{2}", BUILD, ANDROIDVERS, BUILDTYPE);

                Log.updateStatus("Status: Creating tag file...");
                File.WriteAllText(ExtractDirector + @"\version.dat", output);
            }

            string srcDir = Environment.CurrentDirectory + @"\EFI\boot\";
            string CFG = File.ReadAllText(srcDir + config.UEFI_GRUB_CONFIG);
            CFG = CFG.Replace("BLISS_VER", ANDROIDVERS);

            File.WriteAllText(ExtractDirector + @"\" + config.UEFI_GRUB_CONFIG, CFG);
        }

        protected override bool UnInstallBootObjects(Object extraData)
        {

            Log.write("===Removing Boot Objects===");
            MountFirmwarePartition();
            if (UEFIWrapper.UEFI_Init())
            {
                Log.write("-Remove Android UEFI Entry");
                int ret = UEFIWrapper.UEFI_DeleteBootOptionByDescription(config.BOOT_ENTRY_TEXT);
                Log.write("-UEFI: " + ret);
            }
            else
            {
                Log.write("-UEFI Init ... fail");
            }
            base.cleanup(config.UEFI_PARTITION_MOUNTPOINT + config.UEFI_BOOT);
            base.cleanup(config.UEFI_PARTITION_MOUNTPOINT + @"boot\");

            UnMountFirmwarePartition();

            return true;
        }

        private bool MountFirmwarePartition()
        {
            Log.updateStatus("Mounting EFI Partition...");
            Log.write("-Mounting EFI Partition...");
            
            string MOUNT_EXE = @"C:\Windows\System32\mountvol.exe";
            string MOUNT_CMD = string.Format(" Z: /S");

            
            if (!ExecuteCLICommand(MOUNT_EXE, MOUNT_CMD))
            {
                return false;
            }

            return true;
        }

        private bool UnMountFirmwarePartition()
        {
            Log.updateStatus("UnMounting EFI Partition...");
            Log.write("-UnMounting EFI Partition...");
            string UNMOUNT_EXE = @"C:\Windows\System32\mountvol.exe";
            string UNMOUNT_CMD = string.Format(" Z: /D");

            if (!ExecuteCLICommand(UNMOUNT_EXE,UNMOUNT_CMD))
            {
                return false;
            }

            return true;
        }

        private bool CreateBootDirectory(string directory)
        {
            Log.write("-Setup Boot Directory...");
            try
            {
                //directory = @"C:\AndroidOS\EFI\bliss"; testing

                if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Log.write("-Boot Folder Created: " + directory);
            }
            }catch(Exception ex)
            {
                Log.write(ex.Message);
                return false;
            }
            
            return true;
        }

        private bool CopyBootFiles(string InstallDir,string EFI_DIR)
        {
            // EFI_DIR = @"C:\AndroidOS\EFI\bliss"; testing
            Log.updateStatus("Status: Copying boot files (1/2)...");
            Log.write("-Copy EFI Boot files");
            try
            {
                string srcDir = Environment.CurrentDirectory + @"\EFI\boot\";

                File.Copy(srcDir + config.UEFI_BOOT_BIN64, EFI_DIR + config.UEFI_BOOT_BIN64, false);

                if (Environment.Is64BitOperatingSystem)
                {  
                    File.Copy(srcDir + config.UEFI_GRUB_BIN64, EFI_DIR + config.UEFI_GRUB_BIN64, false);
                }
                else
                {
                    File.Copy(srcDir + config.UEFI_GRUB_BIN32, EFI_DIR + config.UEFI_GRUB_BIN32, false);
                }

                Log.write("-Copy configuration file");
                File.Copy(InstallDir + @"\" + config.UEFI_GRUB_CONFIG, EFI_DIR + config.UEFI_GRUB_CONFIG, true);     
                File.Delete(InstallDir + @"\" + config.UEFI_GRUB_CONFIG);
                    
                return true;
            }
            catch (Exception ex)
            {
                Log.write(ex.Message);
                return false;
            }
            
        }

        private bool CopyBootGrubFiles(string BOOT_DIR)
        {
           // BOOT_DIR = @"C:\AndroidOS\boot\";
            Log.updateStatus("Status: Copying boot files (2/2)...");
            Log.write("-Copy Grub Boot files");

            try
            {
               string tempDir = Environment.CurrentDirectory + @"\boot\";

               foreach (string dirPath in Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories))
               {
                   Directory.CreateDirectory(dirPath.Replace(tempDir, BOOT_DIR));
               }

               foreach (string newPath in Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories))
               {
                   File.Copy(newPath, newPath.Replace(tempDir, BOOT_DIR), true);
               }

               return true;
            }
            catch (Exception ex)
            {
                Log.write(ex.Message);
                return false;
            }
        }

        private bool CreateUEFIBootOption(string Drive)
        {
            string _Drive = string.Format(@"\\.\{0}",Drive);

            Log.write("-Add UEFI Entry");
            
            if (!UEFIWrapper.UEFI_Init())
            {
                Log.write("UEFI Init Fail");
                return false;
            }

            if (Environment.Is64BitOperatingSystem)
            {

                if (!UEFIWrapper.UEFI_MakeMediaBootOption(config.BOOT_ENTRY_TEXT, _Drive, config.UEFI_BOOT + config.UEFI_GRUB_BIN64))
                {
                    Log.write("UEFI 64-bit Entry Fail");
                    return false;
                }
            }
            else
            {
                if (!UEFIWrapper.UEFI_MakeMediaBootOption(config.BOOT_ENTRY_TEXT, _Drive, config.UEFI_BOOT + config.UEFI_GRUB_BIN32))
                {
                    Log.write("UEFI 32-bit Entry Fail");
                    return false;
                }
            }
            return true;
        }
    }
}
