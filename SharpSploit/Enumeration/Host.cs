﻿// Author: Ryan Cobb (@cobbr_io)
// Project: SharpSploit (https://github.com/cobbr/SharpSploit)
// License: BSD 3-Clause

using System;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SharpSploit.Generic;
using SharpSploit.Execution;

namespace SharpSploit.Enumeration
{
    /// <summary>
    /// Host is a library for local host enumeration.
    /// </summary>
    public class Host
    {
        /// <summary>
        /// Gets a list of running processes on the system.
        /// </summary>
        /// <returns>List of ProcessResults.</returns>
        public static SharpSploitResultList<ProcessResult> GetProcessList()
        {
            var processorArchitecture = GetArchitecture();
            Process[] processes = Process.GetProcesses();
            SharpSploitResultList<ProcessResult> results = new SharpSploitResultList<ProcessResult>();
            foreach (Process process in processes)
            {
                int processId = process.Id;
                int parentProcessId = GetParentProcess(process);
                string processName = process.ProcessName;
                string processPath = string.Empty;
                int sessionId = process.SessionId;
                string processOwner = GetProcessOwner(process);
                Win32.Kernel32.Platform processArch = Win32.Kernel32.Platform.Unknown;

                if (parentProcessId != 0)
                {
                    processPath = process.MainModule.FileName;
                }

                if (processorArchitecture == Win32.Kernel32.Platform.x64)
                {
                    processArch = IsWow64(process) ? Win32.Kernel32.Platform.x86 : Win32.Kernel32.Platform.x64;
                }
                else if (processorArchitecture == Win32.Kernel32.Platform.x86)
                {
                    processArch = Win32.Kernel32.Platform.x86;
                }
                else if (processorArchitecture == Win32.Kernel32.Platform.IA64)
                {
                    processArch = Win32.Kernel32.Platform.x86;
                }

                results.Add(new ProcessResult(processId, parentProcessId, processName, processPath, sessionId, processOwner, processArch));
            }
            return results;
        }

        /// <summary>
        /// Gets the architecture of the OS.
        /// </summary>
        /// <author>Daniel Duggan (@_RastaMouse)</author>
        public static Win32.Kernel32.Platform GetArchitecture()
        {
            const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;
            const ushort PROCESSOR_ARCHITECTURE_IA64 = 6;
            const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;

            var sysInfo = new Win32.Kernel32.SYSTEM_INFO();
            Win32.Kernel32.GetNativeSystemInfo(ref sysInfo);

            switch (sysInfo.wProcessorArchitecture)
            {
                case PROCESSOR_ARCHITECTURE_AMD64:
                    return Win32.Kernel32.Platform.x64;
                case PROCESSOR_ARCHITECTURE_INTEL:
                    return Win32.Kernel32.Platform.x86;
                case PROCESSOR_ARCHITECTURE_IA64:
                    return Win32.Kernel32.Platform.IA64;
                default:
                    return Win32.Kernel32.Platform.Unknown;
            }
        }

        /// <summary>
        /// Gets the parent process id of a Process
        /// </summary>
        /// <author>Daniel Duggan (@_RastaMouse)</author>
        /// <param name="Process"></param>
        /// <returns>Parent Process Id. Returns 0 if unsuccessful</returns>
        public static int GetParentProcess(Process Process)
        {
            try
            {
                return GetParentProcess(Process.Handle);
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the parent process id of a process handle
        /// </summary>
        /// <author>Daniel Duggan (@_RastaMouse)</author>
        /// <param name="Handle">Handle to the process to get the parent process id of</param>
        /// <returns>Parent Process Id</returns>
        private static int GetParentProcess(IntPtr Handle)
        {
            var basicProcessInformation = new Win32.NtDll.PROCESS_BASIC_INFORMATION();
            Win32.NtDll.NtQueryInformationProcess(Handle, Win32.NtDll.PROCESSINFOCLASS.ProcessBasicInformation, ref basicProcessInformation, Marshal.SizeOf(basicProcessInformation), out int returnLength);
            return basicProcessInformation.InheritedFromUniqueProcessId;
        }

        /// <summary>
        /// Gets the username of the owner of a process
        /// </summary>
        /// <author>Daniel Duggan (@_RastaMouse)</author>
        /// <param name="Process">Process to get owner of</param>
        /// <returns>Username of process owner. Returns empty string if unsuccessful.</returns>
        public static string GetProcessOwner(Process Process)
        {
            try
            {
                Win32.Kernel32.OpenProcessToken(Process.Handle, 8, out IntPtr handle);
                using (var winIdentity = new WindowsIdentity(handle))
                {
                    return winIdentity.Name;
                }
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Checks if a process is a Wow64 process
        /// </summary>
        /// <author>Daniel Duggan (@_RastaMouse)</author>
        /// <param name="Process">Process to check Wow64</param>
        /// <returns>True if process is Wow64, false otherwise. Returns false if unsuccessful.</returns>
        public static bool IsWow64(Process Process)
        {
            try
            {
                Win32.Kernel32.IsWow64Process(Process.Handle, out bool isWow64);
                return isWow64;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Generates a minidump that represents the memory of a running process. Useful for offline Mimikatz
        /// if dumping the LSASS process. (Requires Admin)
        /// </summary>
        /// <param name="processId">Process ID of the process to generate a minidump for.</param>
        /// <param name="outputPath">Path to write output file in. Defaults to the current directory.</param>
        /// <param name="outputFileName">Filename to ouput the minidump to.</param>
        /// <remarks>
        /// Authored by Justin Bui (@youslydawg).
        /// </remarks>
        public static void CreateProcessDump(int processId, string outputPath = "", string outputFileName = "")
        {
            CreateProcessDump(Process.GetProcessById(processId), outputPath, outputFileName);
        }

        /// <summary>
        /// Generates a minidump that represents the memory of a running process. Useful for offline Mimikatz
        /// if dumping the LSASS process. (Requires Admin)
        /// </summary>
        /// <param name="processName">Name of the process to generate a minidump for.</param>
        /// <param name="outputPath">Path to write output file in. Defaults to the current directory.</param>
        /// <param name="outputFileName">Filename to ouput the minidump to.</param>
        /// <remarks>
        /// Authored by Justin Bui (@youslydawg).
        /// </remarks>
        public static void CreateProcessDump(string processName = "lsass", string outputPath = "", string outputFileName = "")
        {
            if (processName.EndsWith(".exe"))
            {
                processName = processName.Substring(0, processName.Length - 4);
            }
            Process[] process_list = Process.GetProcessesByName(processName);
            if (process_list.Length > 0)
            {
                CreateProcessDump(process_list[0], outputPath, outputFileName);
            }
        }

        /// <summary>
        /// Generates a minidump that represents the memory of a running process. Useful for offline Mimikatz
        /// if dumping the LSASS process. (Requires Admin)
        /// </summary>
        /// <param name="process">Process to generate a minidump for.</param>
        /// <param name="outputPath">Path to write output file in. Defaults to the current directory.</param>
        /// <param name="outputFileName">Filename to ouput the minidump to.</param>
        /// <remarks>
        /// Authored by Justin Bui (@youslydawg).
        /// </remarks>
        public static void CreateProcessDump(Process process, string outputPath = "", string outputFileName = "")
        {
            if (outputPath == "" || outputPath == null)
            {
                outputPath = GetCurrentDirectory();
            }
            if (outputFileName == "" || outputFileName == null)
            {
                outputFileName = process.ProcessName + "_" + process.Id + ".dmp";
            }
            
            string fullPath = Path.Combine(outputPath, outputFileName);
            FileStream fileStream = File.Create(fullPath);
            bool success = false;
            try
            {
                success = Execution.Win32.Dbghelp.MiniDumpWriteDump(process.Handle, (uint)process.Id, fileStream.SafeFileHandle, Execution.Win32.Dbghelp.MINIDUMP_TYPE.MiniDumpWithFullMemory, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }

            fileStream.Close();
            if (!success)
            {
                File.Delete(fullPath);
            }
        }

        /// <summary>
        /// Gets the hostname of the system.
        /// </summary>
        /// <returns>Hostname of the system.</returns>
        public static string GetHostname()
		{
			return Environment.MachineName;
		}

        /// <summary>
        /// Gets the Domain name and username of the current logged on user.
        /// </summary>
        /// <returns>Current username.</returns>
        public static string GetUsername()
		{
			return Environment.UserDomainName + "\\" + Environment.UserName;
		}

        /// <summary>
        /// Gets the full path of the current working directory.
        /// </summary>
        /// <returns>Current working directory.</returns>
        public static string GetCurrentDirectory()
		{
			return Directory.GetCurrentDirectory();
		}

        /// <summary>
        /// Gets a directory listing of the current working directory.
        /// </summary>
        /// <returns>List of FileSystemEntryResults.</returns>
		public static SharpSploitResultList<FileSystemEntryResult> GetDirectoryListing()
		{
            return GetDirectoryListing(GetCurrentDirectory());
		}

        /// <summary>
        /// Gets a directory listing of a directory.
        /// </summary>
        /// <param name="Path">The path of the directory to get a listing of.</param>
        /// <returns>List of FileSystemEntryResults.</returns>
		public static SharpSploitResultList<FileSystemEntryResult> GetDirectoryListing(string Path)
        {
            SharpSploitResultList<FileSystemEntryResult> results = new SharpSploitResultList<FileSystemEntryResult>();
            foreach (string dir in Directory.GetDirectories(Path))
            {
                results.Add(new FileSystemEntryResult(dir));
            }
            foreach (string file in Directory.GetFiles(Path))
            {
                results.Add(new FileSystemEntryResult(file));
            }
            return results;
        }

        /// <summary>
        /// Changes the current working directory.
        /// </summary>
        /// <param name="DirectoryName">Relative or absolute path to new working directory.</param>
        public static void ChangeCurrentDirectory(string DirectoryName)
        {
            Directory.SetCurrentDirectory(DirectoryName);
        }

        /// <summary>
        /// ProcessResult represents a running process, used with the GetProcessList() function.
        /// </summary>
        public sealed class ProcessResult : SharpSploitResult
        {
            public int Pid { get; } = 0;
            public int Ppid { get; } = 0;
            public string Name { get; } = "";
            public string Path { get; } = "";
            public int SessionID { get; } = 0;
            public string Owner { get; } = "";
            public Win32.Kernel32.Platform Architecture { get; } = Win32.Kernel32.Platform.Unknown;
            protected internal override IList<SharpSploitResultProperty> ResultProperties
            {
                get
                {
                    return new List<SharpSploitResultProperty> {
                        new SharpSploitResultProperty { Name = "Pid", Value = this.Pid },
                        new SharpSploitResultProperty { Name = "Ppid", Value = this.Ppid },
                        new SharpSploitResultProperty { Name = "Name", Value = this.Name },
                        new SharpSploitResultProperty { Name = "SessionID", Value = this.SessionID },
                        new SharpSploitResultProperty { Name = "Owner", Value = this.Owner },
                        new SharpSploitResultProperty { Name = "Architecture", Value = this.Architecture },
                        new SharpSploitResultProperty { Name = "Path", Value = this.Path }
                    };
                }
            }

            public ProcessResult(int Pid = 0, int Ppid = 0, string Name = "", string Path = "", int Sessionid = 0, string Owner = "", Win32.Kernel32.Platform Architecture = Win32.Kernel32.Platform.Unknown)
            {
                this.Pid = Pid;
                this.Ppid = Ppid;
                this.Name = Name;
                this.Path = Path;
                this.SessionID = Sessionid;
                this.Owner = Owner;
                this.Architecture = Architecture;
            }
        }

        /// <summary>
        /// FileSystemEntryResult represents a file on disk, used with the GetDirectoryListing() function.
        /// </summary>
        public sealed class FileSystemEntryResult : SharpSploitResult
        {
            public string Name { get; } = "";
            protected internal override IList<SharpSploitResultProperty> ResultProperties
            {
                get
                {
                    return new List<SharpSploitResultProperty>
                    {
                        new SharpSploitResultProperty
                        {
                            Name = "Name",
                            Value = this.Name
                        }
                    };
                }
            }

            public FileSystemEntryResult(string Name = "")
            {
                this.Name = Name;
            }
        }
    }
}
