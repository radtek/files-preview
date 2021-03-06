using Files.CommandLine;
using Microsoft.System;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace Files
{
    internal class Program1
    {
        private static async Task Main()
        {
            var args = Environment.GetCommandLineArgs();
            var proc = System.Diagnostics.Process.GetCurrentProcess();

            if (args.Length == 2)
            {
                var parsedCommands = CommandLineParser.ParseUntrustedCommands(args);

                if (parsedCommands != null && parsedCommands.Count > 0)
                {
                    foreach (var command in parsedCommands)
                    {
                        switch (command.Type)
                        {
                            case ParsedCommandType.ExplorerShellCommand:
                                await OpenShellCommandInExplorerAsync(command.Payload, proc.Id);
                                //Exit..

                                return;

                            default:
                                break;
                        }
                    }
                }
            }

            // TODO: Add Activation args with Reunion
            //if (!ApplicationData.Current.RoamingSettings.Values.Get("AlwaysOpenANewInstance", false))
            //{
            //    IActivatedEventArgs activatedArgs = AppInstance.GetActivatedEventArgs();

            //    if (AppInstance.RecommendedInstance != null)
            //    {
            //        AppInstance.RecommendedInstance.RedirectActivationTo();
            //        return;
            //    }
            //    else if (activatedArgs is LaunchActivatedEventArgs)
            //    {
            //        var launchArgs = activatedArgs as LaunchActivatedEventArgs;

            //        var activePid = ApplicationData.Current.LocalSettings.Values.Get("INSTANCE_ACTIVE", -1);
            //        var instance = AppInstance.FindOrRegisterInstanceForKey(activePid.ToString());
            //        if (!instance.IsCurrentInstance && !string.IsNullOrEmpty(launchArgs.Arguments))
            //        {
            //            instance.RedirectActivationTo();
            //            return;
            //        }
            //    }
            //}

            //AppInstance.FindOrRegisterInstanceForKey(proc.Id.ToString());
            ApplicationData.Current.LocalSettings.Values["INSTANCE_ACTIVE"] = proc.Id;
            Application.Start( p => new App());
        }

        public static async Task OpenShellCommandInExplorerAsync(string shellCommand, int pid)
        {
            // TODO: Implment shell commands in-project
            //System.Diagnostics.Debug.WriteLine("Launching shell command in FullTrustProcess");
            //ApplicationData.Current.LocalSettings.Values["ShellCommand"] = shellCommand;
            //ApplicationData.Current.LocalSettings.Values["Arguments"] = "ShellCommand";
            //ApplicationData.Current.LocalSettings.Values["pid"] = pid;
            //await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }
    }
}