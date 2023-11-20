﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;

namespace Installer
{
    public enum AutoStart
    {
        NONE = 0,
        FSUIPC,
        EXE
    }

    public static class InstallerFunctions
    {
        public static bool GetProcessRunning(string name)
        {
            Process proc = Process.GetProcessesByName(name).FirstOrDefault();
            return proc != null && proc.ProcessName == name;
        }

        #region Install Actions
        public static bool AutoStartFsuipc()
        {
            bool result = false;

            try
            {
                string regPath = (string)Registry.GetValue(Parameters.ipcRegPath, Parameters.ipcRegInstallDirValue, null);
                if (!string.IsNullOrEmpty(regPath))
                    regPath += "\\" + "FSUIPC7.ini";
                else
                    return false;

                if (File.Exists(regPath))
                {
                    string fileContent = File.ReadAllText(regPath, Encoding.Default);
                    if (!fileContent.Contains("[Programs]"))
                    {
                        fileContent += $"\r\n[Programs]\r\nRunIf1=READY,KILL,{Parameters.binPath}";
                        File.WriteAllText(regPath, fileContent, Encoding.Default);
                        result = true;
                    }
                    else
                    {
                        string pattern = @"^RunIf(\d+).*WorkingTitle2GSX\.exe";
                        if (Regex.IsMatch(fileContent, pattern, RegexOptions.Compiled | RegexOptions.Multiline))
                        {
                            fileContent = Regex.Replace(fileContent, pattern, $"RunIf$1=READY,KILL,{Parameters.binPath}", RegexOptions.Compiled | RegexOptions.Multiline);
                            File.WriteAllText(regPath, fileContent, Encoding.Default);
                            result = true;
                        }
                        else
                        {
                            int posLastRunBegin = fileContent.LastIndexOf("RunIf");
                            int posLastRunEnd = fileContent.IndexOf('\n', posLastRunBegin);
                            int lastIndex = Convert.ToInt32(fileContent.Substring(posLastRunBegin + 5, 1));
                            fileContent = fileContent.Insert(posLastRunEnd + 1, $"RunIf{lastIndex + 1}=READY,KILL,{Parameters.binPath}\r\n");
                            File.WriteAllText(regPath, fileContent, Encoding.Default);
                            result = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during AutoStartFsuipc", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return result;
        }

        public static bool AutoStartExe()
        {
            bool result = false;

            try
            {
                string path = Parameters.msExeStore;
                if (!File.Exists(path))
                    path = Parameters.msExeSteam;

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(File.ReadAllText(path));

                bool found = false;
                XmlNode simbase = xmlDoc.ChildNodes[1];
                foreach (XmlNode outerNode in simbase.ChildNodes)
                {
                    if (outerNode.Name == "Launch.Addon" && outerNode.InnerText.Contains("WorkingTitle2GSX.exe"))
                    {
                        found = true;
                        foreach (XmlNode innerNode in outerNode.ChildNodes)
                        {
                            if (innerNode.Name == "Disabled")
                                innerNode.InnerText = "False";
                            else if (innerNode.Name == "Path")
                                innerNode.InnerText = Parameters.binPath;
                            else if (innerNode.Name == "CommandLine")
                                innerNode.InnerText = "";
                            else if (innerNode.Name == "ManualLoad")
                                innerNode.InnerText = "";
                        }
                    }
                }

                if (!found)
                {
                    XmlNode outerNode = xmlDoc.CreateElement("Launch.Addon");

                    XmlNode innerNode = xmlDoc.CreateElement("Disabled");
                    innerNode.InnerText = "False";
                    outerNode.AppendChild(innerNode);

                    innerNode = xmlDoc.CreateElement("ManualLoad");
                    innerNode.InnerText = "False";
                    outerNode.AppendChild(innerNode);

                    innerNode = xmlDoc.CreateElement("Name");
                    innerNode.InnerText = "WorkingTitle2GSX";
                    outerNode.AppendChild(innerNode);

                    innerNode = xmlDoc.CreateElement("Path");
                    innerNode.InnerText = Parameters.binPath;
                    outerNode.AppendChild(innerNode);

                    xmlDoc.ChildNodes[1].AppendChild(outerNode);
                }

                xmlDoc.Save(path);
                result = true;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during AutoStartExe", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return result;
        }

        public static bool PlaceDesktopLink()
        {
            bool result = false;
            try
            {
                IShellLink link = (IShellLink)new ShellLink();

                link.SetDescription("Start WorkingTitle2GSX");
                link.SetPath(Parameters.binPath);

                IPersistFile file = (IPersistFile)link;
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                file.Save(Path.Combine(desktopPath, "WorkingTitle2GSX.lnk"), false);
                result = true;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during PlaceDesktopLink", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return result;
        }

        public static bool DeleteOldFiles()
        {
            try
            {
                if (!Directory.Exists(Parameters.binDir))
                    return true;

                Directory.Delete(Parameters.binDir, true);
                Directory.CreateDirectory(Parameters.binDir);

                return (new DirectoryInfo(Parameters.binDir)).GetFiles().Length == 0;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during RemoveOldFiles", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static bool ExtractZip(string extractDir = null, string zipFile = null)
        {
            try
            {
                if (zipFile == null)
                {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Installer.{Parameters.fileName}"))
                    {
                        ZipArchive archive = new ZipArchive(stream);
                        archive.ExtractToDirectory(Parameters.binDir);
                        stream.Close();
                    }

                    RunCommand($"powershell -WindowStyle Hidden -Command \"dir -Path {Parameters.binDir} -Recurse | Unblock-File\"");
                }
                else
                {
                    using (Stream stream = new FileStream(zipFile, FileMode.Open))
                    {
                        ZipArchive archive = new ZipArchive(stream);
                        archive.ExtractToDirectory(extractDir);
                        stream.Close();
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during ExtractZip", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static bool InstallWasm()
        {
            bool result = false;
            try
            {


            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during InstallWasm", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return result;
        }

        public static bool DownloadFile(string url, string file)
        {
            bool result = false;
            try
            {
                var webClient = new WebClient();
                webClient.DownloadFile(url, file);
                result = File.Exists(file);

            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during DownloadFile", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return result;
        }
        #endregion

        #region Check Requirements
        public static bool CheckFSUIPC()
        {
            bool result = false;
            try
            {
                string regVersion = (string)Registry.GetValue(Parameters.ipcRegPath, Parameters.ipcRegValue, null);
                if (!string.IsNullOrWhiteSpace(regVersion))
                {
                    regVersion = regVersion.Substring(1);
                    result = CheckVersion(regVersion, Parameters.ipcVersion, true, false);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during CheckFSUIPC", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return result;
        }
        public static bool CheckVersion(string versionInstalled, string versionRequired, bool majorEqual, bool ignoreBuild)
        {
            bool majorMatch = false;
            bool minorMatch = false;
            bool patchMatch = false;

            string[] strInst = versionInstalled.Split('.');
            string[] strReq = versionRequired.Split('.');
            int vInst;
            int vReq;
            bool prevWasEqual = false;

            //Major
            if (int.TryParse(strInst[0], out vInst) && int.TryParse(strReq[0], out vReq))
            {
                if (majorEqual)
                    majorMatch = vInst == vReq;
                else
                    majorMatch = vInst >= vReq;

                prevWasEqual = vInst == vReq;
            }

            //Minor
            if (int.TryParse(strInst[1], out vInst) && int.TryParse(strReq[1], out vReq))
            {
                if (prevWasEqual)
                    minorMatch = vInst >= vReq;
                else
                    minorMatch = true;

                prevWasEqual = vInst == vReq;
            }

            //Patch
            if (!ignoreBuild)
            {
                if (int.TryParse(strInst[2], out vInst) && int.TryParse(strReq[2], out vReq))
                {
                    if (prevWasEqual)
                        patchMatch = vInst >= vReq;
                    else
                        patchMatch = true;
                }
            }
            else
                patchMatch = true;

            return majorMatch && minorMatch && patchMatch;
        }

        public static bool CheckPackageVersion(string packagePath, string packageName, string version)
        {
            try
            {
                string file = packagePath + "\\" + packageName + "\\manifest.json";
                if (File.Exists(file))
                {
                    string[] lines = File.ReadAllLines(file);
                    foreach (string line in lines)
                    {
                        if (Parameters.wasmRegex.IsMatch(line))
                        {
                            var matches = Parameters.wasmRegex.Matches(line);
                            if (matches.Count == 1 && matches[0].Groups.Count >= 2)
                                return CheckVersion(matches[0].Groups[1].Value, version, false, false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during CheckPackageVersion", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return false;
        }

        public static string FindPackagePath(string confFile)
        {
            string[] lines = File.ReadAllLines(confFile);
            foreach (string line in lines)
            {
                if (line.StartsWith(Parameters.msStringPackage))
                {
                    return line.Replace("\"", "").Substring(Parameters.msStringPackage.Length) + "\\Community";
                }
            }

            return "";
        }

        public static bool CheckInstalledMSFS(out string packagePath)
        {
            try
            {
                if (File.Exists(Parameters.msConfigStore))
                {
                    packagePath = FindPackagePath(Parameters.msConfigStore);
                    return !string.IsNullOrWhiteSpace(packagePath) && Directory.Exists(packagePath);
                }
                else if (File.Exists(Parameters.msConfigSteam))
                {
                    packagePath = FindPackagePath(Parameters.msConfigSteam);
                    return !string.IsNullOrWhiteSpace(packagePath) && Directory.Exists(packagePath);
                }

                packagePath = "";
                return false;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during CheckInstalledMSFS", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            packagePath = "";
            return false;
        }

        public static string RunCommand(string command)
        {
            var pProcess = new Process();
            pProcess.StartInfo.FileName = "cmd.exe";
            pProcess.StartInfo.Arguments = "/C" + command;
            pProcess.StartInfo.UseShellExecute = false;
            pProcess.StartInfo.CreateNoWindow = true;
            pProcess.StartInfo.RedirectStandardOutput = true;
            pProcess.Start();
            string strOutput = pProcess.StandardOutput.ReadToEnd();
            pProcess.WaitForExit();

            return strOutput ?? "";
        }

        public static bool StringGreaterEqual(string input, int compare)
        {
            if (int.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out int numA) && numA >= compare)
                return true;
            else
                return false;
        }

        public static bool CheckDotNet()
        {
            try
            {
                bool installedDesktop = false;

                string output = RunCommand("dotnet --list-runtimes");

                var matches = Parameters.netDesktop.Matches(output);
                foreach (Match match in matches)
                {
                    if (!match.Success || match.Groups.Count != 5) continue;

                    if (StringGreaterEqual(match.Groups[2].Value, Parameters.netMajor) && StringGreaterEqual(match.Groups[3].Value, Parameters.netMinor) && StringGreaterEqual(match.Groups[4].Value, Parameters.netPatch))
                        installedDesktop = true;
                }

                return installedDesktop;
            }
            catch (Exception e)
            {
                MessageBox.Show($"Exception '{e.GetType()}' during CheckDotNet", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        #endregion
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    internal interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
