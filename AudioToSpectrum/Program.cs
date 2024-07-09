using Microsoft.Win32;
using Newtonsoft.Json;
using System.Reflection;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Toolkit.Uwp.Notifications;

namespace AudioToSpectrum
{
    internal class Program
    {
        private static readonly Random random = new();
        // IMPORTANT INFORMATION ON CONFIG FILES
        // For some reason, when the program is run, the config file is created at the directory of the 1st argument.
        // For example, converting a file in your Downloads folder will create a config file there.
        // I don't know how to fix this. For now I am putting it in the LocalAppData folder.
        // Maybe a TODO: Get folder path from registry and put the config file there.
        private static JsonConfig? Configuration { get; set; } = new();
        static void Main(string[] args)
        {
            EnsureConfigFileExists();
            string ConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioToSpectrum\\config.json");

            // This is scuffed but bypasses windows' file locking system.
            if (File.Exists(ConfigFile))
            {
                Configuration = JsonConvert.DeserializeObject<JsonConfig>(File.ReadAllText(ConfigFile));
            }

            if (args.Length > 0)
            {
                string filePath = args[0];
                string runtimesDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "runtimes");
                string newFilePath = filePath.Replace(Path.GetExtension(filePath), ".mp4");
                int saturation = 1;

                if (File.Exists(Path.Combine(runtimesDirectory, "ffmpeg.exe")) == false)
                {
                    new ToastContentBuilder().AddText("AudioToSpectrum Error").AddText("FFmpeg is missing. Please put the ffmpeg.exe in the runtimes folder of AudioToSpectrum").Show();
                    return;
                }

                if (File.Exists(newFilePath) == true)
                {
                    newFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + $"_{RandomString(4)}.mp4");
                }

                if (Configuration!.MemorialServiceMode)
                {
                    Configuration.ResolutionW = "640";
                    Configuration.ResolutionH = "480";
                    Configuration.FrameRate = 20;
                    Configuration.BackgroundColor = "white";
                    Configuration.SpectrumColor = "fire";
                    saturation = 3;
                }
                
                string cdCommand = $"cd {runtimesDirectory}";

                // this looks horrible but it's much easier than trying to mess with escape characters.
                string ffmpCommand1 = $"ffmpeg.exe -i \"";
                string ffmpCommand2 = $"{filePath}\"";
                string ffmpCommand3 = $" -filter_complex \"color=s={Configuration!.ResolutionW}x{Configuration!.ResolutionH}:c={Configuration!.BackgroundColor}[bg]; [0:a]showspectrum=s={Configuration!.ResolutionW}x{Configuration!.ResolutionH}:slide=scroll:fscale=lin:saturation={saturation}:start=12:stop=4000:color={Configuration!.SpectrumColor}[v]; [bg][v]overlay=format=auto:shortest=1,format=yuv420p[v]; [v]drawtext=fontfile='C\\:/Windows/Fonts/ariblk.ttf':text='%{{pts\\:hms}}':x=(w-tw-50):y=(h-th-50):fontsize=18:fontcolor=white:box=1:boxborderw=3|5:boxcolor=black@1[vtxt]; [vtxt]format=yuv420p[vout]\" -map \"[vout]\" -map 0:a -r {Configuration.FrameRate} -c:v libx264 -c:a aac -b:a 192k \"";
                string ffmpCommand4 = $"{newFilePath}\"";
                string ffmpCommand = ffmpCommand1 + ffmpCommand2 + ffmpCommand3 + ffmpCommand4;

                // not converting an exe to mp4. nice try.
                if (!IsValidAudioFile(filePath))
                {
                    new ToastContentBuilder().AddText("AudioToSpectrum Error").AddText("I can only convert audio types \".wav\", \".mp3\", \".aac\", \".flac\", \".ogg\", and \".wma\" to \".mp4\"").Show();
                    return;
                }

                Process process = new() {
                    StartInfo = new()
                    {
                        FileName = "cmd.exe",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };

                process.Start();

                process.StandardInput.WriteLine(cdCommand);
                process.StandardInput.WriteLine(ffmpCommand);
                process.StandardInput.Flush();
                process.StandardInput.Close();

                process.WaitForExit();

                new ToastContentBuilder().AddText("AudioToSpectrum").AddText("Finished converting audio to video").Show();

                return;
            }

            if (!IsElevated)
            {
                // c# is scuffed and has no way to request elevation at runtime so instead of starting the program as
                // admin automatically through process.start and use runas, just request the user to do it
                new ToastContentBuilder().AddText("AudioToSpectrum Notice").AddText("To set up the directory where AudioToSpectrum is, you need to run it as admin.").Show();
                return;
            }

            string path = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");

            RegistryKey shellKey = Registry.ClassesRoot.CreateSubKey("*\\shell\\AudioToSpectrum");

            shellKey.SetValue("", "Convert Audio to Video");
            shellKey.SetValue("Icon", $"{path}");

            RegistryKey commandKey = shellKey.CreateSubKey("command");

            commandKey.SetValue("", $"\"{path}\" \"%1\"");

            new ToastContentBuilder().AddText("AudioToSpectrum").AddText("Successfully set the new directory path for AudioToSpectrum.").Show();
        }

        internal static void EnsureConfigFileExists()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioToSpectrum");

            if (File.Exists($"{appData}\\config.json")) return;

            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);

                // we have to do this because the directory will be locked by the process. i hope this fixes it. if not, gg.
                Thread.Sleep(250);
                File.WriteAllText($"{appData}\\config.json", JsonConvert.SerializeObject(new JsonConfig(), Formatting.Indented));
            }
        }

        public static bool IsValidAudioFile(string filePath)
        {
            // List of valid audio file extensions
            string[] validExtensions = new string[] { ".wav", ".mp3", ".aac", ".flac", ".ogg", ".wma" };

            // Get the file extension
            string extension = Path.GetExtension(filePath);

            // Check if the extension is in the list of valid extensions
            foreach (string validExtension in validExtensions)
            {
                if (extension.Equals(validExtension, StringComparison.OrdinalIgnoreCase)) return true;
            }

            // If the extension is not in the list, it's not a valid audio file
            return false;
        }

        public static bool IsElevated
        {
            get
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static string RandomString(int length) => new(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz0123456789", length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    internal class JsonConfig
    {
        public bool MemorialServiceMode { get; set; } = false;
        public string? ResolutionW { get; set; } = "1280";
        public string? ResolutionH { get; set; } = "720";
        public int FrameRate { get; set; } = 30;
        public string? BackgroundColor { get; set; } = "black";
        public string? SpectrumColor { get; set; } = "intensity";
    }
}