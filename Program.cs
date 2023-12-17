using Microsoft.Win32;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Security.Principal;

namespace Aquc.EasiNoteSplashEditor;

public class EasiNoteSplashEditor
{
    public static readonly Logger logger = new Func<Logger>(() =>
    {
        var args = Environment.GetCommandLineArgs();
        var loggerconfig = new LoggerConfiguration()
            .WriteTo.File($"log/{DateTime.Now:yyyyMMdd}.log");
        if (!args.Contains("--no-log"))
            loggerconfig.WriteTo.Console();
        loggerconfig.WriteTo.Sentry(o =>
        {
            // Debug and higher are stored as breadcrumbs (default os Information)
            o.MinimumBreadcrumbLevel = LogEventLevel.Debug;
            // Error and higher is sent as event (default is Error)
            o.MinimumEventLevel = LogEventLevel.Error;
            // If DSN is not set, the SDK will look for an environment variable called SENTRY_DSN. If nothing is found, SDK is disabled.
            o.Dsn = "https://91338a6d5a58ad50bba6c50738353e48@o4505418205364224.ingest.sentry.io/4506048915570688";
            o.AttachStacktrace = true;
            // send PII like the username of the user logged in to the device
            o.SendDefaultPii = true;
            // Optional Serilog text formatter used to format LogEvent to string. If TextFormatter is set, FormatProvider is ignored.
            // Other configuration
            o.AutoSessionTracking = true;

            // This option is recommended for client applications only. It ensures all threads use the same global scope.
            // If you're writing a background service of any kind, you should remove this.
            o.IsGlobalModeEnabled = false;

            // This option will enable Sentry's tracing features. You still need to start transactions and spans.
            o.EnableTracing = true;
            o.Debug = args.Contains("--sentrylog");
        });
        loggerconfig.MinimumLevel.Verbose();
        return loggerconfig.CreateLogger();
    }).Invoke();
    private string[] args;
    public EasiNoteSplashEditor(string[] args)
    {
        this.args = args;
    }


    public string GetSeewoPath()
    {
        var wow6432Node = Registry.LocalMachine.OpenSubKey("SOFTWARE")?.OpenSubKey("WOW6432Node")!;
        var seewoNode = wow6432Node.OpenSubKey("Seewo")?.OpenSubKey("EasiNote5");
        if (seewoNode == null)
        {
            throw new ArgumentNullException("Seewo/EasiNote5 is missing.");
        }
        else
        {
            return Path.GetDirectoryName(seewoNode.GetValue("ActualExePath")!.ToString()!)!;
        }
    }
    public string GetSeewoBannerImagePath()
    {
        return Path.Combine(GetSeewoPath(), "Assets", "SplashScreen.png");
    }
    private bool CheckAdminPermission()
    {
        var id = WindowsIdentity.GetCurrent();
        var p = new WindowsPrincipal(id);
        return p.IsInRole(WindowsBuiltInRole.Administrator);
    }
    public void SetSecurity()
    {
        if (!CheckAdminPermission())
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    Arguments = string.Join(" ", args),
                    Verb = "runas",
                    WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute=true
                }
            };
            logger.Warning("No administrator permission! Request permission and retry.");
            process.Start();
            Environment.Exit(1829);
        }

        var f = new FileInfo(GetSeewoBannerImagePath());
        var fs = f.GetAccessControl();
        fs.SetAccessRule(new System.Security.AccessControl.FileSystemAccessRule("Users", System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
        f.SetAccessControl(fs);

        logger.Information("Get permission successfully!");
    }
    public void SetReadonly()
    {
        File.SetAttributes(GetSeewoBannerImagePath(), FileAttributes.ReadOnly);
    }
    public void RemoveReadonly()
    {
        File.SetAttributes(GetSeewoBannerImagePath(), FileAttributes.Normal);
    }
    public void ChangeBannerImage()
    {
        //SetSecurity();
        RemoveReadonly();
        File.Copy(Path.Combine(Directory.GetCurrentDirectory(), "images", "EditedSplashBanner.png"), GetSeewoBannerImagePath(), true);
        SetReadonly();

        logger.Information($"Changed seewo banner image to {Path.Combine(Directory.GetCurrentDirectory(), "images", "EditedSplashBanner.png")}");

    }
    public void ChangeBannerImage(string imagePath)
    {
        RemoveReadonly();
        File.Copy(imagePath, GetSeewoBannerImagePath(), true);
        SetReadonly();

        logger.Information($"Changed seewo banner image to {imagePath}");
    }
    public void ChangeDefaultBannerImage()
    {

        //SetSecurity();
        RemoveReadonly();
        File.Copy(Path.Combine(Directory.GetCurrentDirectory(), "images", "DefaultSplashScreen.png"), GetSeewoBannerImagePath(), true);
        SetReadonly();

        logger.Information($"Changed seewo banner image to {Path.Combine(Directory.GetCurrentDirectory(), "images", "DefaultSplashScreen.png")}");
    }

    public static void Main(string[] args)
    {
        var program = new EasiNoteSplashEditor(args);

        if (args.Length == 1)
        {
            if (args[0] == "default") program.ChangeDefaultBannerImage();
            else if (args[0] == "edit") program.ChangeBannerImage();
            else if (args[0] == "admin") program.SetSecurity();
        }
        if (args.Length == 2)
        {
            if (args[0] == "edit") program.ChangeBannerImage(args[1]);
        }
    }
}