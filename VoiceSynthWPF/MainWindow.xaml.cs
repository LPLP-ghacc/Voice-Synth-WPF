using System.IO;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Input;
using ConseqConcatenation;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoiceSynthWPF;

public class Settings: IConseqData
{
    public string VoiceInput { get; init; }
    public int VoiceSpeed { get; init; }
    public int VoiceVolume { get; init; }
    public int StdDelay { get; init; }
    public string ReaderName { get; init; }

    private Settings(string voiceInput, int voiceSpeed, int voiceVolume, int stdDelay, string readerName) 
    {
        VoiceInput = voiceInput;
        VoiceSpeed = voiceSpeed;
        VoiceVolume = voiceVolume;
        StdDelay = stdDelay;
        ReaderName = readerName;
    }

    private static Settings Default { get; } = new("CABLE Input", 0, 100, 10, "Microsoft Irina");

    public async Task Save(string path, string fileName)
    {
        var conseq = this.Conqsequalize();
        
        await File.WriteAllTextAsync(Path.Combine(path, fileName), conseq);
    }

    public static async Task<Settings> Load(string path)
    {
        if (!Path.Exists(path))
        {
            var settings = Default;
            var conseqSave = settings.Conqsequalize();
            await File.WriteAllTextAsync(Path.Combine(Environment.CurrentDirectory, "settings.cc"), conseqSave);
            
            return settings;
        }
        
        var conseq = await File.ReadAllTextAsync(path);
        
        try
        {
            return Conseq.Deconqsequalize<Settings>(conseq);
        }
        catch
        {
            return Default;
        }
    }
}

public partial class MainWindow
{
    public static MainWindow? Instance;
    private static Settings? _settings;
    private const string SnippetsFile = "snippets.cc";
    private SpeechSynthesizer? _synth;
    private MMDevice? _cableDevice;

    private readonly Action<string> _synthHandler; 
    
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
                Scroll.ScrollToEnd();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Log(e.Message);
            }
        };

        _synthHandler = async void (text) =>
        {
            try
            {
                await SynthAsync(text);
                Scroll.ScrollToEnd();
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
        
        GlobalKeyboardHook.Start();
        GlobalKeyboardHook.KeyPressed += OnGlobalKeyPressed;
    }
    
    private async Task InitAsync()
    {
        _settings = await Settings.Load(Path.Combine(Environment.CurrentDirectory, "settings.cc"));

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
        
        await LoadSnippets();
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

    public void Log(string message) => OutputBox.Text += message + Environment.NewLine;

    private void OutputBox_OnGotFocus(object sender, RoutedEventArgs e) => InputBox.Focus();

    private void CreateSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new SnippetCreationWind(
            "Создание сниппета",
            string.Empty
        )
        {
            Owner = GetWindow(this)
        };

        if (window.ShowDialog() != true) return;
        
        var nb = new NumButton();
        if (window.ResultText1 != null) nb.SetText(window.ResultText1);
        
        nb.ActivationKey = window.SelectedKey;

        nb.KeyHandler.Text = nb.ActivationKey.ToString();

        Snippets.Children.Add(nb);
    }
    
    private void OnGlobalKeyPressed(Key key)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var button in Snippets.Children.OfType<NumButton>())
            {
                if (button.ActivationKey == key)
                {
                    _synthHandler?.Invoke(button.FullText);
                }
            }
        });
    }
    
    public async Task SaveSnippetsAsync()
    {
        var models = Snippets.Children
            .OfType<NumButton>()
            .Select(b => new SnippetModel
            {
                Text = b.FullText,
                ActivationKey = b.ActivationKey
            })
            .ToList();

        var text = Conseq.Conqsequalize(models, ConseqFormat.Readable);
        await File.WriteAllTextAsync(
            Path.Combine(Environment.CurrentDirectory, SnippetsFile),
            text);
    }
    
    private async Task LoadSnippets()
    {
        var path = Path.Combine(Environment.CurrentDirectory, SnippetsFile);

        if (!File.Exists(path))
            return;

        var text = await File.ReadAllTextAsync(path);

        try
        {
            var models = Conseq.Deconqsequalize<List<SnippetModel>>(text);
            
            foreach (var model in models)
            {
                var nb = new NumButton();

                nb.SetText(model.Text);
                nb.ActivationKey = model.ActivationKey;
                nb.KeyHandler.Text = model.ActivationKey.ToString();

                Snippets.Children.Add(nb);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            await SaveSnippetsAsync();
            base.OnClosing(e);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Log(ex.Message);
        }
    }
}