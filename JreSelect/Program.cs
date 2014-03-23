using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

// JreSelect is a commandline tool written by Anooshiravan Ahmadi (MCE) Schuberg Philis to force javaws.exe to run the specified JRE version.
// Please use this tool with caution, the reason that JRE7 disables JRE6 is a major security issues of Java JRE6.
// Only use with jnlp URLs that are fully trusted.

namespace JreSelect
{
    class Program
    {
        public static string javacpl = "";
        public static string javaws = "";
        public static string error = "";

        static void Main(string[] Args)
        {
            //Read command line arguments
            Arguments CommandLine = new Arguments(Args);
            string jre = "";
            string jnlp = "";
            string arch = "";
            string jDir = "";
            int myProductID = 0;
            bool isError = false;

            if (CommandLine["jre"] != null)
            {
                jre = CommandLine["jre"];
            }
            else
            {
                error += "- JRE parameter not found. Please define JRE version.\r\n";
                isError = true;
            }

            if (CommandLine["jnlp"] != null)
            {
                jnlp = CommandLine["jnlp"];
            }
            else
            {
                error += "- JNLP parameter not found. Please define JNLP path.\r\n";
                isError = true;
            }

            if (CommandLine["arch"] != null && (CommandLine["arch"].ToUpper() == "X86" || CommandLine["arch"].ToUpper() == "X64"))
            {
                arch = CommandLine["arch"].ToUpper();
            }
            else
            {
                error += "- ARCH parameter not found or is not correct. (use -arch x86 or -arch x64)\r\n";
                isError = true;
            }
            if (isError) showError();

            // Check deployment.properties and create if not exist
            string deployPath = Environment.GetEnvironmentVariable("USERPROFILE") + @"/appdata/locallow/sun/java/deployment/deployment.properties";
            if (!File.Exists(deployPath))
            {
                string j64dir = @"C:\Program Files\Java";
                string j32dir = @"C:\Program Files (x86)\Java";
                if (arch == "X86")
                {
                    jDir = j32dir;
                }
                else if (arch == "X64")
                {
                    jDir = j64dir;
                }

                DirSearch(jDir);
                Console.WriteLine(javacpl);
                int cplid = 0;
                try
                {
                    Process cpl = new Process();
                    cpl.StartInfo.FileName = javacpl;
                    cpl.StartInfo.RedirectStandardOutput = false;
                    cpl.StartInfo.UseShellExecute = true;
                    cpl.Start();
                    cplid = cpl.Id;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                int i = 0;
                while (!File.Exists(deployPath) && i < 50)
                {
                    System.Threading.Thread.Sleep(100);
                    i++;
                }
                if (cplid != 0)
                {
                    foreach (var process in Process.GetProcessesByName("javaw"))
                    {
                        int parentId;
                        try
                        {
                            parentId = Process.GetProcessById(process.Id).Parent().Id;
                        }
                        catch (Exception ex)
                        {
                            string s = ex.Message;
                            int start = s.IndexOf("Process with an Id of ");
                            int end = s.IndexOf(" is not running.");
                            parentId = Convert.ToInt32(Regex.Replace(s.Substring(start, end - start), "[^.0-9]", ""));
                        }
                        if (parentId == cplid) Process.GetProcessById(process.Id).Kill();
                    }
                }
            }
            if (!File.Exists(deployPath))
            {
                error += "- The file deployment.properties cannot be created.\r\n";
                isError = true;
            }

            // Put product IDs and product versions in the dictionary
            if (isError) showError();
            System.Collections.Generic.IEnumerable<String> deployFile = File.ReadAllLines(deployPath);
            Dictionary<int, string> products = new Dictionary<int, string>();
            foreach (string line in deployFile)
            {
                if (line.Contains("product="))
                {
                    int productID = Convert.ToInt32(line.Substring(line.IndexOf("product=") - 2, 1));
                    int indx = line.IndexOf("=") + 1;
                    int length = line.Length - indx;
                    string product = line.Substring(indx, length);
                    products.Add(productID, product);
                }
            }

            // Check if -jre parameter is correct
            if (!products.ContainsValue(jre))
            {
                error += "- Parameter -jre does not point to an installed JRE in your system.\r\n";
                error += "  System contains the followin JRE versions:\r\n";
                foreach (var pair in products)
                {
                    error += "    " + pair.Value + "\r\n";
                }
                isError = true;
            }
            else
            {
                myProductID = products.FirstOrDefault(x => x.Value == jre).Key;
            }

            // Disable all JRE versions except the selected one
            if (isError) showError();
            List<string> myDeployFile = new List<string>();
            foreach (string line in deployFile)
            {
                if (line.Contains("enabled="))
                {
                    int productID = Convert.ToInt32(line.Substring(line.IndexOf("enabled=") - 2, 1));
                    string strEnable = line.Substring(0, line.IndexOf("="));
                    if (productID == myProductID)
                    {
                        myDeployFile.Add(strEnable + "=true");
                    }
                    else
                    {
                        myDeployFile.Add(strEnable + "=false");
                    }

                }
                else
                {
                    myDeployFile.Add(line);
                }
            }
            // Find javaws
            foreach (string line in deployFile)
            {
                if (line.Contains("deployment.javaws.jre." + myProductID + ".path="))
                {
                    javaws = line.Substring(line.IndexOf("=") + 1, line.Length - line.IndexOf("=") - 1);
                    javaws = javaws.Replace(@"\:", @":");
                    javaws = javaws.Replace(@"\\", @"\");
                    javaws = javaws.Replace(@".exe", @"s.exe");
                }
            }
           
            // rewrite deployment.properties
            File.WriteAllLines (deployPath, myDeployFile.ToArray());
            
            //Run JNLP on JavaWS with the selected JRE
            Process javawsProc = new Process();
            javawsProc.StartInfo.FileName = javaws;
            javawsProc.StartInfo.Arguments = jnlp; ;
            Console.WriteLine(javaws);
            Console.WriteLine(jnlp);
                        
            javawsProc.Start();
            int pid = javawsProc.Id;

            // Wait for Javaw and return to previous deployment.properties
            while (ProcessExists(pid))
            {
                System.Threading.Thread.Sleep(200);
            }
            File.WriteAllLines(deployPath, deployFile.ToArray());

            // All done. Environment Exit
            Environment.Exit(0);
        }
        
        // Functions
        public static void showError()
        {
            showHelp();
            Console.WriteLine("\r\nError running the utility:");
            Console.WriteLine("--------------------");
            Console.WriteLine(error);
            Environment.Exit(1);
        }
        public static void showHelp()
        {
            string help = "\r\nJreSelect utility written by: Anooshiravan Ahmadi (MCE) Schuberg Philis\r\n\r\n";
            help += "JreSelect forces Java WebStart (javaws.exe) to use an specified JRE (javaw.exe)\r\n";
            help += "Please use this tool with caution, the reason that JRE7 disables JRE6 is a major security issues of JRE6.\r\n";
            help += "Only use with jnlp URLs that are fully trusted.\r\n";
            help += "Usage: JreSelect.exe [/jre=<Product Version>] [/jnlp=<URL to JNLP file>] [/arch=<OS architecture]>\r\n";
            help += "Example: JreSelect.exe /jre=1.6.0_45 /jnlp=http/://path/to/my.jnlp /arch=x86";
            Console.WriteLine(help);
        }
        private static bool ProcessExists(int iProcessID)
        {
            foreach (Process p in Process.GetProcesses())
            {
                if (p.Id == iProcessID)
                {
                    return true;
                }
            }
            return false;
        }
        private static void DirSearch(string sDir)
        {
            string found = "";
            try
            {
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d, "javacpl.exe"))
                    {
                        found = f;
                        if (found != "" && found != null) break;
                    }
                    if (found != "" && found != null) break;
                    else DirSearch(d);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            if (found != "" && found != null) javacpl = found;
        }
    }
    public static class ProcessExtensions
    {
        private static string FindIndexedProcessName(int pid)
        {
            var processName = Process.GetProcessById(pid).ProcessName;
            var processesByName = Process.GetProcessesByName(processName);
            string processIndexdName = null;

            for (var index = 0; index < processesByName.Length; index++)
            {
                processIndexdName = index == 0 ? processName : processName + "#" + index;
                var processId = new PerformanceCounter("Process", "ID Process", processIndexdName);
                if ((int)processId.NextValue() == pid)
                {
                    return processIndexdName;
                }
            }

            return processIndexdName;
        }

        private static Process FindPidFromIndexedProcessName(string indexedProcessName)
        {
            var parentId = new PerformanceCounter("Process", "Creating Process ID", indexedProcessName);
            return Process.GetProcessById((int)parentId.NextValue());
        }

        public static Process Parent(this Process process)
        {
            return FindPidFromIndexedProcessName(FindIndexedProcessName(process.Id));
        }
    }
}
