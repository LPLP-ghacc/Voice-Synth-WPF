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

    public Settings(string voiceInput, int voiceSpeed, int voiceVolume, int stdDelay, string readerName) 
    {
        VoiceInput = voiceInput;
        VoiceSpeed = voiceSpeed;
        VoiceVolume = voiceVolume;
        StdDelay = stdDelay;
        ReaderName = readerName;
    }

    public static Settings Default { get; } = new("CABLE Input", 0, 100, 10, "Microsoft Irina");

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
        await ApplySettingsAsync(_settings);
        await LoadSnippets();
    }

    /// <summary>
    /// Применяет настройки: выбирает аудиоустройство и синтезатор.
    /// Возвращает false если аудиоустройство не найдено.
    /// </summary>
    private async Task<bool> ApplySettingsAsync(Settings settings)
    {
        // Dispose old synth if exists
        _synth?.Dispose();
        _synth = null;

        var enumerator = new MMDeviceEnumerator();
        var allDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

        // Ищем точное совпадение, затем частичное
        _cableDevice = allDevices.FirstOrDefault(d => d.FriendlyName == settings.VoiceInput)
                    ?? allDevices.FirstOrDefault(d => d.FriendlyName.Contains(settings.VoiceInput));

        if (_cableDevice == null)
        {
            var available = string.Join(", ", allDevices.Select(d => d.FriendlyName));
            Log($"[ОШИБКА] Аудиоустройство '{settings.VoiceInput}' не найдено.");
            Log($"Доступные устройства: {available}");
            Log("Откройте Настройки и выберите нужное устройство.");
            return false;
        }

#pragma warning disable CA1416
        _synth = new SpeechSynthesizer();
        _synth.Rate = settings.VoiceSpeed;
        _synth.Volume = settings.VoiceVolume;

        var voices = _synth.GetInstalledVoices().ToList();
        Log($"Найдено голосов: {voices.Count}");
        foreach (var item in voices)
            Log($"  • {item.VoiceInfo.Name}");

        var selectedVoice = voices.FirstOrDefault(v => v.VoiceInfo.Name == settings.ReaderName);
        if (selectedVoice != null)
        {
            _synth.SelectVoice(settings.ReaderName);
            Log($"Выбран голос: {settings.ReaderName}");
        }
        else if (voices.Count > 0)
        {
            var fallback = voices[0].VoiceInfo.Name;
            _synth.SelectVoice(fallback);
            Log($"[ПРЕДУПРЕЖДЕНИЕ] Голос '{settings.ReaderName}' не найден, используется: {fallback}");
        }
        else
        {
            Log("[ОШИБКА] Не найдено ни одного установленного голоса TTS.");
            _synth.Dispose();
            _synth = null;
            return false;
        }
#pragma warning restore CA1416

        await Task.CompletedTask;
        return true;
    }

    public async Task OpenSettingsAsync()
    {
        var current = _settings ?? Settings.Default;
        var window = new SettingsWindow(current) { Owner = this };

        if (window.ShowDialog() != true || window.ResultSettings == null)
            return;

        _settings = window.ResultSettings;

        var ok = await ApplySettingsAsync(_settings);
        if (ok)
            Log("Настройки применены.");

        await _settings.Save(Environment.CurrentDirectory, "settings.cc");
    }
    
    private async Task SynthAsync(string text)
    {
        if (_synth == null)
        {
            Log("[ОШИБКА] Синтезатор речи не инициализирован. Проверьте настройки.");
            return;
        }

        if (_cableDevice == null)
        {
            Log("[ОШИБКА] Аудиоустройство не найдено. Откройте Настройки и выберите устройство.");
            return;
        }

        var tcs = new TaskCompletionSource();

        Log($"=> {text}");

        using var ms = new MemoryStream();

#pragma warning disable CA1416
        _synth.SetOutputToWaveStream(ms);
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