using Microsoft.Win32;
using System.Reflection;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Toolkit.Uwp.Notifications;

namespace AudioToSpectrum
{
    internal class Program
    {
        private static Random random = new();
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                string filePath = args[0];
                string runtimesDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "runtimes");
                string newFilePath = filePath.Replace(Path.GetExtension(filePath), ".mp4");

                if (File.Exists(Path.Combine(runtimesDirectory, "ffmpeg.exe")) == false)
                {
                    new ToastContentBuilder().AddText("AudioToSpectrum Error").AddText("FFmpeg is missing. Please put the ffmpeg.exe in the runtimes folder of AudioToSpectrum").Show();
                    return;
                }

                if (File.Exists(newFilePath) == true)
                {
                    newFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + $"_{RandomString(4)}.mp4");
                }

                // this looks horrible but it's much easier than trying to mess with escape characters.
                string cdCommand = $"cd {runtimesDirectory}";
                string ffmpCommand1 = $"ffmpeg.exe -i \"";
                string ffmpCommand2 = $"{filePath}\"";

                // TODO: maybe maybe add some configuration json or something to allow the user to change resolution and frame rate
                string ffmpCommand3 = $" -filter_complex \"[0:a]showspectrum=s=1280x720:slide=scroll:color=intensity[v]; [v]drawtext=fontfile='C\\:/Windows/Fonts/ariblk.ttf':text='%{{pts\\:hms}}':x=(w-tw-10):y=(h-th-10):fontsize=24:fontcolor=white:box=1:boxcolor=black@1[vtxt]; [vtxt]format=yuv420p[vout]\" -map \"[vout]\" -map 0:a -r 30 -c:v libx264 -c:a aac -b:a 192k \"";
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
}