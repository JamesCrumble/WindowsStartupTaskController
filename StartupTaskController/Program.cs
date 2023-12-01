using System.Text;
using Microsoft.Win32;
using System.Diagnostics;
using System.ComponentModel;
using Shell32;
using System.Security.Principal;


{
    onStartup();

    ShellController.RegistrateEvent(
        "appslist", () =>
        {
            StartupAppsInfo startupAppsInfo = new StartupAppsInfo(writableRegistryKey: true);
            foreach (StartupAppInfo startupAppInfo in startupAppsInfo.Iterable(onlyEnabled: true))
            {
                string paramsInfo = startupAppInfo.Params.Count == 0 ? "" : '\"' + string.Join("\", \"", startupAppInfo.Params) + '\"';
                Console.WriteLine($"{startupAppInfo.ExecutableCmd}, [{paramsInfo}]");
                if (startupAppInfo.Command == string.Empty)
                {
                    Console.WriteLine($"DEBUG: Cannot define command for \"{startupAppInfo.AppName}\" app name. This app will be skipped...");
                    continue;
                }
            }
        }
    );
    ShellController.RegistrateEvent(
        "appsrun", () => {

            StartupAppsInfo startupAppsInfo = new StartupAppsInfo(writableRegistryKey: true);
            Dictionary<string, Process> startups = new Dictionary<string, Process>();

            foreach (StartupAppInfo startupAppInfo in startupAppsInfo.Iterable(onlyEnabled: true))
            {
                string paramsInfo = startupAppInfo.Params.Count == 0 ? '\"' + string.Join("\", \"", startupAppInfo.Params) + '\"' : "";
                Console.WriteLine($"{startupAppInfo.ExecutableCmd}, [{paramsInfo}]");
                if (startupAppInfo.Command == string.Empty)
                {
                    Console.WriteLine($"DEBUG: Cannot define command for \"{startupAppInfo.AppName}\" app name. This app will be skipped...");
                    continue;
                }

                Process proc = new Process();
                proc.StartInfo.FileName = @startupAppInfo.ExecutableCmd;
                proc.StartInfo.Arguments = string.Join(' ', startupAppInfo.Params);
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.LoadUserProfile = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.RedirectStandardOutput = true;

                startups[startupAppInfo.AppName] = proc;
            }

            executeStartupProcs(startups);
        }
    );

    ShellController.RunForever();
}

void onStartup()
{
    try
    {
        WindowsIdentity user = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(user);
        bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        if (isAdmin is false)
        {
            throw new UnauthorizedAccessException("isAdmin is false...");
        }
    }
    catch (Exception ex)
    {
        throw new Exception($"CRITICAL: App require administrator privilage. Fall with exc {ex}");
    }
}

void executeStartupProcs(Dictionary<string, Process> startups)
{
    while (startups.Count != 0)
    {
        foreach (string appName in startups.Keys)
        {
            bool val = false;
            Process proc = startups[appName];
            try
            {
                Console.WriteLine($"Start process \"{appName}\" app.");
                val = proc.Start(); // TODO: Can be dead loop option. Impl execution on background...
            }
            catch (Win32Exception exc)
            {
                val = false;
                Console.WriteLine($"ERROR: Cannot start of continue processing \"{appName}\" app with err: {exc}");
            }
            finally
            {
                Console.WriteLine($"Proc with by {appName} app start with {val} status. {proc.StartInfo.FileName}");
                // if (val is true)
                // Console.WriteLine(proc.StandardOutput.ReadToEnd());
                startups.Remove(appName);
            }

            if (val is false)
            {
                break;
            }

            // Console.WriteLine(proc.StandardOutput.ReadToEnd());
            // try
            // {
            //     DateTime startTime = proc.StartTime;
            // }
            // catch (InvalidOperationException)
            // {
            //     if (!proc.Start())
            //     {
            //         // Console.WriteLine(proc.StandardOutput.ReadLine());
            //         startups.Remove(i);
            //     }
            // }
        }
    }
}

public class ShellController
{
    private static Dictionary<string, Action> events = new Dictionary<string, Action>();

    public static bool RegistrateEvent(string eventName, Action executable) {

        if (events.ContainsKey(eventName)) {
            Console.WriteLine($"WARNING: Event with \"{eventName}\" name already exists in events and will be skipped !!!");
            return false;
        }

        events[eventName] = executable;
        return true;
    }

    public static string GenerateDoc() {
        StringBuilder sb = new StringBuilder();
        sb.Append("###### Windows StartupTaskController ######\n");
        sb.Append("###### Help info about all commands  ######\n");
        sb.Append("\nDefault commands:\n");
        sb.Append(" - exit\n\n");
        sb.Append("Functional commands:\n");

        int i = 0;
        foreach (string eventName in events.Keys) {
            sb.Append($" {i + 1}. {eventName}\n");
            i++;
        }

        return sb.ToString();
    }

    public static void RunForever() 
    { 
        Console.WriteLine(GenerateDoc());
        while (true) /* All events loop */
        {
            Console.Write("Enter id or event name to execute command with parameters.\n$ ");
            string? readed = Console.ReadLine();
            if (readed is null || readed == string.Empty) {
                Console.WriteLine($"INFO: Unknown \"{readed}\" id or command");
                continue;
            }
            if (events.ContainsKey(readed.ToLower())) {
                events[readed.ToLower()]();
                Console.WriteLine();
                continue;
            } else if (readed.ToLower() == "exit") {
                Console.WriteLine("INFO: Exiting...");
                Thread.Sleep(1000);
                break;   
            }

            Console.WriteLine($"INFO: Unknown \"{readed}\" id or command");
        }
    }
}

public class StartupAppsInfo
{
    public static string[] RegistryPaths = new string[2]{
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
    };
    public static RegistryKey[] RegistryKeys = new RegistryKey[2] {
        Registry.CurrentUser,
        Registry.LocalMachine,
    };

    public bool WritableRegistryKey;

    public StartupAppsInfo(bool writableRegistryKey = false)
    {
        WritableRegistryKey = writableRegistryKey;
    }

    public IEnumerable<StartupAppInfo> Iterable(bool onlyEnabled = false)
    {
        foreach (string registryPath in RegistryPaths)
        {
            foreach (RegistryKey registryKey in RegistryKeys)
            {
                RegistryKey? subRegistryKey = registryKey.OpenSubKey(registryPath, WritableRegistryKey);
                if (subRegistryKey is null)
                {
                    Console.WriteLine($"DEBUG: Cannot open sub key for \"{registryPath}\" path by {registryKey.Name} reg key");
                    continue;
                }
                foreach (string appName in subRegistryKey.GetValueNames())
                {
                    StartupAppInfo startupAppInfo = new StartupAppInfo(subRegistryKey, appName);
                    if (onlyEnabled && !startupAppInfo.isEnabled)
                    {
                        continue;
                    }
                    yield return startupAppInfo;
                }
            }
        }
    }
}

public struct StartupAppInfo
{
    public RegistryKey RegistrySubKey;
    public string AppName;

    public StartupAppInfo(RegistryKey regKey, string appName)
    {
        RegistrySubKey = regKey;
        AppName = appName;
    }
    public string? Command
    {
        get
        {
            List<string> errs = new List<string>();
            string[] commandsRegistyPaths = new string[1] {
                @"Software\Microsoft\Windows\CurrentVersion\Run",
            };

            foreach (string commandsRegistryPath in commandsRegistyPaths)
            {
                foreach (RegistryKey registryKey in StartupAppsInfo.RegistryKeys)
                {
                    RegistryKey subRegistryKey = registryKey.OpenSubKey(commandsRegistryPath);
                    if (subRegistryKey is null)
                    {
                        errs.Add($"DEBUG: Cannot open sub reg key by \"{commandsRegistryPath}\" path of \"{registryKey.Name}\" reg key");
                        continue;
                    }

                    object? command = subRegistryKey.GetValue(AppName);
                    if (command is null)
                    {
                        errs.Add($"DEBUG: Cannot get command from \"{subRegistryKey.Name}\" sub reg key");
                        continue;
                    }


                    return ((string)command).Replace("\"", "");
                }
            }

            /* I HAVE NO FUCKING IDEA HOW IN C# USING UNION TYPE SO CREATE FROM STRING EVERY TIME... */
            string? _findFile(string startDirPath, string appName)
            {
                DirectoryInfo startupFolderInfo = new DirectoryInfo(startDirPath);

                foreach (FileInfo file in startupFolderInfo.GetFiles())
                {
                    if (!file.Name.ToLower().Contains(appName.ToLower()) && !appName.ToLower().Contains(file.Name.ToLower()))
                    {
                        continue;
                    }

                    string ret = file.FullName;
                    if (!ret.EndsWith(".lnk"))
                    {
                        Console.WriteLine(ret);
                        return ret;
                    }

                    Type? shellAppType = Type.GetTypeFromProgID("Shell.Application");
                    if (shellAppType is null) {
                        Console.WriteLine("DEBUG: Cannot get shell app type...");
                        return ret;
                    }
                    object? shell = Activator.CreateInstance(shellAppType);
                    if (shell is null) {
                        Console.WriteLine("DEBUG: Cannot create instance of shell...");
                        return ret;
                    }
                    Folder? folder = (Folder)shellAppType.InvokeMember(
                        "NameSpace", 
                        System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { @startupFolderInfo.FullName }
                    );
                    if (folder is null) {
                        Console.WriteLine("DEBUG: Cannot invoike method to get shell folder obj...");
                        return ret; 
                    }

                    foreach (FolderItem item in folder.Items())
                    {
                        if (file.Name == item.Name)
                        {
                            continue;
                        }
                        if (!item.IsLink)
                        {
                            continue;
                        }
                        
                        
                        ShellLinkObject lnk = item.GetLink;
                        FolderItem target = lnk.Target;
                        ret = target.Path;
                        break;
                    }

                    return ret.Replace("\"", "");
                }

                foreach (DirectoryInfo folder in startupFolderInfo.EnumerateDirectories())
                {
                    if (
                        !folder.Name.ToLower().Contains(appName.ToLower())
                        && !appName.ToLower().Contains(folder.Name.ToLower())
                    )
                    {
                        continue;
                    }
                    return _findFile(folder.FullName, appName);
                }
                return null;
            }

            string foundedCommandPath = _findFile("C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs", AppName);
            if (foundedCommandPath is null && errs.Count() > 0) {
                foreach (string err in errs) {
                    Console.WriteLine(err);
                }
            }

            return foundedCommandPath is null ? null : foundedCommandPath;
        }
    }
    public byte[] AppInfobytes
    {
        get
        {
            return (byte[])RegistrySubKey.GetValue(AppName);
        }

    }
    public string AppInfoBytesRepr
    {
        get
        {
            byte[] info = AppInfobytes;
            StringBuilder infoStr = new StringBuilder(info.Length * 2);
            for (int i = 0; i < info.Length; i++)
            {
                byte byte_ = info[i];
                string FormatString = i == 0 ? "{0:x2}" : " {0:x2}";
                infoStr.AppendFormat(FormatString, byte_);
            }
            return infoStr.ToString();
        }
    }

    public bool isEnabled
    {
        get
        {
            return (
                AppInfobytes.ElementAt(0).Equals(0x02) || AppInfobytes.ElementAt(0).Equals(0x06)
            );
        }
    }
    
    public List<string> Params { 
    
        get
        {
            string? command = Command;
            List<string> cmdParams = new List<string>();
            if (command is null)
            {
                return cmdParams;
            }


            foreach (string param in Helpers.takewhile(cmdPart => cmdPart.EndsWith(".exe"), command.Split(' ').Reverse())) {
                if (param == string.Empty || param == "") {
                    continue;
                }
                cmdParams.Add(param);
            }

            return cmdParams;
        }
    }

    public string? ExecutableCmd
    {
        get
        {
            string? command = Command;
            if (command is null)
            {
                return null;
            }

            List<string> cmdParams = Params;
            List<string> execCommand = new List<string>();

            foreach (string execPart in Helpers.dropwhile(cmdPart => cmdPart.Split(' ').Any(part => part.EndsWith(".exe")), command.Split('\\'))) 
            {
                List<string> buf = new List<string>();
                foreach (string part in execPart.Split(' ')) {                  
                    if (cmdParams.Contains(part)) {
                        continue;
                    }
                    buf.Add(part);
                }

                string execPartStr = string.Join(' ', buf);
                bool isExecutablePart = execPartStr.Split(' ').Any(part => part.EndsWith(".exe"));
                if (isExecutablePart) {
                    int execIndex = execPartStr.IndexOf(".exe");
                    if (execIndex != -1)
                    {
                        execPartStr = execPart.Substring(0, execIndex) + ".exe";
                    }
                }
                /* Not rly needed in windows scope .-. */
                // if (execPartStr.Contains(' ')) {
                //     execPartStr = $"\"{execPartStr}\"";
                // }

                execCommand.Add(execPartStr);
            }
            return string.Join('\\', execCommand);
        }
    }

    public override string ToString()
    {
        return $"StartupAppInfo{{AppName: \"{AppName}\", isEnabled: {isEnabled}, Command: \"{Command}\"}}";
    }
}

public class Helpers
{
    public static IEnumerable<T> takewhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
    {
        foreach (var v in iterable)
        {
            if (predicate(v))
            {
                break;
            }
            yield return v;
        }
    }

    public static IEnumerable<T> dropwhile<T>(Func<T, bool> predicate, IEnumerable<T> iterable)
    {
        foreach (var v in iterable)
        {
            yield return v;
            if (predicate(v))
            {
                break;
            }
        }

    }
}