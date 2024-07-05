# Audio Spectrum Visualizer

This project is a tool for creating videos from audio files with spectrum visualizations using FFMPEG and C#/.NET. It also includes functionality to add a button to the Windows Explorer right-click menu for easy access.
<br><br>

# Users:

## How to Use

1. Download AudioToSpectrum from the releases on this GitHub repository

2. Extract AudioToSpectrum to whatever directory you would like

3. Run AudioToSpectrum.exe as administrator to add the right-click menu button

4. Find any audio file you wish to convert

5. Right-click the audio file and click "Convert Audio to Video"

6. Your video will be generated in the same directory as the audio file with the same name as the audio file. Conversion may take a while, so please be patient.
<br><br><br>

## How to Use from Command Prompt

1. Copy the path of your audio file

2. Run AudioToSpectrum.exe (Copied file path)

3. Your video will be the same name and in the same directory as the audio file.
<br><br><br><br>


# Developers

## How to Ship

1. **Clone the repository**: Clone this repository to your local machine using `git clone https://github.com/AyeItsAxi/AudioToSpectrum.git`.

2. **Build the project**: Use your IDE to build the project under the "Release" configuration.

3. **Include FFMPEG**: Copy your FFMPEG.exe file to the runtimes directory of the built program. I cannot include it with the git repository because it is 140 MB.

4. **Package the release**: Package the files in the directory of the built program (Typically `/AudioToSpectrum/bin/Release/net6.0-windows10.0.17763.0/`) into a .zip file or installer.

5. **Distribute the package**: Distribute the .zip file or installer to users. They can run the program by following the How to Use guide.

## Contributing

We welcome any contributions! If you notice any issues or have any feature requests, please create an issue in the Issues tab of this repository. 

If you have added anything to the source code, you are more than welcome to create a pull request.
