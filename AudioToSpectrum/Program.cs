using Microsoft.Win32;
using Newtonsoft.Json;
using System.Reflection;
using System.Diagnostics;
using Windows.UI.Notifications;
using System.Security.Principal;
using System.Text.RegularExpressions;
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

        // IMPORTANT: For easier debugging, set the following string to a file path of media that you want to use to debug the program
        internal static string DebugFilePath = "C:\\Users\\shawn\\Documents\\ATSTester.wav";
        static void Main(string[] args)
        {
            Log("Main thread");

            var assemblyConfigurationAttribute = typeof(Program).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
            var buildConfigurationName = assemblyConfigurationAttribute?.Configuration;
            var isDebug = buildConfigurationName!.ToLower().Contains("debug");

            Log("ensuring config file exists");
            EnsureConfigFileExists();
            string ConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioToSpectrum\\config.json");

            // This is scuffed but bypasses windows' file locking system.
            if (File.Exists(ConfigFile))
            {
                Configuration = JsonConvert.DeserializeObject<JsonConfig>(File.ReadAllText(ConfigFile));
                Log("Set Configuration variable to ConfigFile contents");
            }

            
            if (isDebug || args.Length > 0)
            {
                Log("Argument passed through");
                string filePath = isDebug ? DebugFilePath : args[0];
                string runtimesDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "runtimes");
                string newFilePath = filePath.Replace(Path.GetExtension(filePath), ".mp4");
                int saturation = 1;

                if (File.Exists(Path.Combine(runtimesDirectory, "ffmpeg.exe")) == false)
                {
                    Log("FFmpeg is missing");
                    new ToastContentBuilder().AddText("AudioToSpectrum Error").AddText("FFmpeg is missing. Please put the ffmpeg.exe in the runtimes folder of AudioToSpectrum").Show();
                    Log("Returning");
                    return;
                }

                if (File.Exists(newFilePath) == true)
                {
                    Log("File already exists");
                    newFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + $"_{RandomString(4)}.mp4");
                }

                if (Configuration!.MemorialServiceMode)
                {
                    Log("Memorial Service Mode enabled");
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

                // do quick maths to get expected amount of frames based on audio duration and configured framerate
                double AudioDuration = Convert.ToDouble(GetAudioDurationInMilliseconds(filePath));
                Log($"Successfully got audio duration: {AudioDuration}");
                double msPerFrame = 1000 / (double)Configuration.FrameRate;
                Log($"Successfully got ms per frame: {msPerFrame}");
                double estimatedFrameCount = (double)Math.Round((double)(AudioDuration / msPerFrame));
                Log($"Successfully got estimated frame count: {estimatedFrameCount}");


                // not converting an exe to mp4. nice try.
                if (!IsValidAudioFile(filePath))
                {
                    Log("Invalid audio file");
                    new ToastContentBuilder().AddText("AudioToSpectrum Error").AddText("I can only convert audio types \".wav\", \".mp3\", \".aac\", \".flac\", \".ogg\", and \".wma\" to \".mp4\"").Show();
                    return;
                }

                // assign tag names for later
                string toastTag = "atsconversion";
                string toastGroup = "conversionprogress";

                // create toast that we modify for progress
                var progressToastContent = new ToastContentBuilder()
                    .AddText("AudioToSpectrum - Converting")
                    .AddVisualChild(new AdaptiveProgressBar()
                    {
                        Title = filePath,
                        Value = new BindableProgressBarValue("progressValue"),
                        ValueStringOverride = new BindableString("progressValueString"),
                        Status = new BindableString("progressStatus")
                    })
                    .GetToastContent();

                // set initial values
                var toast = new ToastNotification(progressToastContent.GetXml())
                {
                    Tag = toastTag,
                    Group = toastGroup,

                    Data = new NotificationData()
                };
                toast.Data.Values["progressValue"] = "0";
                toast.Data.Values["progressValueString"] = "0%";
                toast.Data.Values["progressStatus"] = $"0/{estimatedFrameCount} (0 fps)";

                // set sequence to 0 initially and show progress toast
                toast.Data.SequenceNumber = 0;
                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);

                // cmd window we use for ffmpeg
                Process process = new()
                {
                    StartInfo = new()
                    {
                        FileName = "cmd.exe",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };

                Log("Starting process");

                // timer that is used for the update interval of the toast notification
                System.Timers.Timer timer = new System.Timers.Timer(250);
                string outputBuffer = "";

                // i dont rly think this is necessary since all normal output goes through the
                // error received function instead of this one. Weird.
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        // Append the new data to the buffer.
                        outputBuffer += e.Data + Environment.NewLine;
                    }
                };

                // run every 250ms
                timer.Elapsed += (s, e) =>
                {
                    // Process the buffer and update the notification.
                    if (!string.IsNullOrWhiteSpace(outputBuffer))
                    {
                        // Extract progress data from the buffer.

                        if (outputBuffer.Contains("frame="))
                        {
                            // get percentage completed
                            int frameCount = int.Parse(outputBuffer.Split("frame= ")[1].Split(" fps")[0]);
                            int framesPerSecond = int.Parse(outputBuffer.Split("fps=")[1].Split(" q")[0]);
                            double progress = Math.Min((double)frameCount / estimatedFrameCount, 1.0);
                            int progressPercentage = (int)(progress * 100);

                            // set new toast values
                            toast.Data.Values["progressValue"] = progress.ToString("0.##");
                            toast.Data.Values["progressValueString"] = progressPercentage + "%";
                            toast.Data.Values["progressStatus"] = $"{frameCount}/{estimatedFrameCount} frames ({framesPerSecond} fps)";
                            toast.Data.SequenceNumber++;

                            // update existing toast notification
                            ToastNotificationManagerCompat.CreateToastNotifier().Update(toast.Data, toastTag, toastGroup);

                            // Clear processed data from the buffer to avoid reprocessing.
                            outputBuffer = string.Empty;
                        }
                    }
                };

                // ffmpeg's output
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuffer = e.Data;
                    }
                };

                // self explanatory
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                timer.Start();

                // self explanatory
                process.StandardInput.WriteLine(cdCommand);
                process.StandardInput.WriteLine(ffmpCommand);
                process.StandardInput.Flush();
                process.StandardInput.Close();

                // self explanatory
                process.WaitForExit();
                timer.Stop();
                timer.Dispose();

                Log("Finished process");

                // set progress to 100 after ffmpeg is done
                toast.Data.Values["progressValue"] = "1";
                toast.Data.Values["progressValueString"] = "100%";
                toast.Data.Values["progressStatus"] = "Finished processing";
                toast.Data.SequenceNumber++;

                // update toast
                ToastNotificationManagerCompat.CreateToastNotifier().Update(toast.Data, toastTag, toastGroup);

                // immediately hide progressbar toast
                ToastNotificationManagerCompat.CreateToastNotifier().Hide(toast);

                // show new toast that process is completed
                new ToastContentBuilder().AddText("AudioToSpectrum").AddText("Finished converting audio to video").Show();

                Log("Returning");
                return;
            }

            if (!IsElevated)
            {
                // c# is scuffed and has no way to request elevation at runtime so instead of starting the program as
                // admin automatically through process.start and use runas, just request the user to do it
                Log("Not ran as admin");
                new ToastContentBuilder().AddText("AudioToSpectrum Notice").AddText("To set up the directory where AudioToSpectrum is, you need to run it as admin.").Show();
                Log("Returning");
                return;
            }

            string path = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");

            Log("Creating subkey in registry");
            RegistryKey shellKey = Registry.ClassesRoot.CreateSubKey("*\\shell\\AudioToSpectrum");

            Log("Setting values in registry");
            shellKey.SetValue("", "Convert Audio to Video");
            shellKey.SetValue("Icon", $"{path}");

            Log("Creating command subkey in registry");
            RegistryKey commandKey = shellKey.CreateSubKey("command");

            Log("Setting value for command in registry");
            commandKey.SetValue("", $"\"{path}\" \"%1\"");

            new ToastContentBuilder().AddText("AudioToSpectrum").AddText("Successfully set the new directory path for AudioToSpectrum.").Show();
            Log("Completed main");
        }

        internal static void EnsureConfigFileExists()
        {
            Log("EnsureConfigFileExists");
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioToSpectrum");

            if (File.Exists($"{appData}\\config.json"))
            {
                Log("Config file exists");
                return;
            }

            if (!Directory.Exists(appData))
            {
                Log("Config file does not exist");
                Directory.CreateDirectory(appData);
                Log("Created directory");

                // we have to do this because the directory will be locked by the process. i hope this fixes it. if not, gg.
                Thread.Sleep(250);
                File.WriteAllText($"{appData}\\config.json", JsonConvert.SerializeObject(new JsonConfig(), Formatting.Indented));
                Log("Created config file");
            }

            Log("Finished EnsureConfigFileExists");
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

        internal static void Log(string content)
        {
#if DEBUG
            File.AppendAllText("programOutput.txt", Environment.NewLine + content);
#endif
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

        public static string GetAudioDurationInMilliseconds(string filePath)
        {
            Log($"GADIM: FILEPATH: {filePath}");
            // Command to get metadata of the audio file
            string ffmpegCommand = $"ffmpeg.exe -i \"{filePath}\"";

            Log($"GADIM: FFMPEGCOMMAND: {ffmpegCommand}");
            // Process to run ffmpeg and capture the output
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {ffmpegCommand}",
                    WorkingDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "runtimes"),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            string output = "";

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output += e.Data + Environment.NewLine;
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output += e.Data + Environment.NewLine;
                }
            };

            // Start the process
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            Log($"GADIM: ffmpeg is done, output: {output}");

            // Regex to extract the duration from ffmpeg output
            var durationMatch = Regex.Match(output, @"Duration: (\d{2}):(\d{2}):(\d{2})\.(\d{2})");

            if (durationMatch.Success)
            {
                Log($"GADIM: regex successfully matched");
                // Parse the duration components
                int hours = int.Parse(durationMatch.Groups[1].Value);
                int minutes = int.Parse(durationMatch.Groups[2].Value);
                int seconds = int.Parse(durationMatch.Groups[3].Value);
                int milliseconds = int.Parse(durationMatch.Groups[4].Value) * 10;

                // Convert the duration to milliseconds
                TimeSpan duration = new TimeSpan(hours, minutes, seconds);
                long totalMilliseconds = (long)duration.TotalMilliseconds + milliseconds;

                Log($"GADIM: MILLISECONDS: {milliseconds}");

                Log($"GADIM: TM: {totalMilliseconds}");

                return totalMilliseconds.ToString(); // Return duration in milliseconds
            }

            return "Duration not found"; // Return a fallback message if duration is not found
        }

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