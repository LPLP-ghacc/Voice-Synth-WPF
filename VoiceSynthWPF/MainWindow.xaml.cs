using System.IO;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoiceSynthWPF;

public class Settings(string voiceInput, int voiceSpeed, int voiceVolume, int stdDelay, string readerName)
{
    public string VoiceInput { get; init; } = voiceInput; // "CABLE Input"
    public int VoiceSpeed { get; init; } = voiceSpeed; // 0
    public int VoiceVolume { get; init; } = voiceVolume; // 100
    public int StdDelay { get; init; } = stdDelay; // 10
    public string ReaderName { get; init; } = readerName;

    public static Settings Default { get; } = new("CABLE Input", 0, 100, 10, "Microsoft Irina");

    public async Task Save(string path, string fileName)
    {
        var json = JsonSerializer.Serialize(this);
        
        await File.WriteAllTextAsync(Path.Combine(path, fileName), json);
    }

    public static Settings Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Settings>(json) == null ? Default : JsonSerializer.Deserialize<Settings>(json)!;
    }
}

public partial class MainWindow
{
    public static MainWindow? Instance;
    
    private static Settings? _settings;

    private SpeechSynthesizer? _synth;
    private MMDevice? _cableDevice;
    
    public MainWindow()
    {
        InitializeComponent();
        Instance = this;

        Loaded += async (_, _) => await InitAsync();

        Action<string> onMessageSend = async void (text) =>
        {
            try
            {
                InputBox.Text = string.Empty;
                await SynthAsync(text);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Log(e.Message);
            }
        };
        
        InputBox.KeyDown += (_, e) =>
        {
            if (e.Key.Equals(Key.Enter) && !string.IsNullOrEmpty(InputBox.Text.Trim()))
            {
                onMessageSend.Invoke(InputBox.Text.Trim());
            }
        };
    }

    private Task InitAsync()
    {
        _settings = Settings.Load(Path.Combine(Environment.CurrentDirectory, "settings.json"));

        // https://vb-audio.com/Cable/
        const string target = "CABLE Input";

        var enumerator = new MMDeviceEnumerator();
        _cableDevice = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(d => d.FriendlyName.Contains(target))!;

#pragma warning disable CA1416
        
        _synth = new SpeechSynthesizer();
        _synth.Rate = _settings.VoiceSpeed;
        _synth.Volume = _settings.VoiceVolume;
        
        foreach (var item in _synth.GetInstalledVoices())
        {
            Log(item.VoiceInfo.Name);
        }
        
        var ruVoice = _synth.GetInstalledVoices().FirstOrDefault(v => v.VoiceInfo.Name == _settings.ReaderName);
        if (ruVoice != null) _synth.SelectVoice(_settings.ReaderName);
#pragma warning restore CA1416
        
        return Task.CompletedTask;
    }
    
    private async Task SynthAsync(string text)
    {
        var tcs = new TaskCompletionSource();

        Log($"=> {text}");

        using var ms = new MemoryStream();

#pragma warning disable CA1416
        _synth!.SetOutputToWaveStream(ms);
        await Task.Run(() => _synth.Speak(text));
#pragma warning restore CA1416

        ms.Position = 0;

        var reader = new WaveFileReader(ms);
        var wasapiOut = new WasapiOut(_cableDevice, AudioClientShareMode.Shared, false, 100);

        wasapiOut.Init(reader);

        wasapiOut.PlaybackStopped += (_, _) =>
        {
            wasapiOut.Dispose();
            reader.Dispose();
            tcs.TrySetResult();
        };

        wasapiOut.Play();

        await tcs.Task;
    }

    private void Log(string message)
    {
        OutputBox.Text += message + Environment.NewLine;
    }

    private void OutputBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        InputBox.Focus();
    }
}