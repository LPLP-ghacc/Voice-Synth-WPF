using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Input;
using NAudio.CoreAudioApi;

namespace VoiceSynthWPF;

public partial class SettingsWindow : Window
{
    public Settings? ResultSettings { get; private set; }

    private readonly Settings _current;

    public SettingsWindow(Settings current)
    {
        InitializeComponent();
        _current = current;

        PopulateDevices();
        PopulateVoices();

        SpeedSlider.Value = current.VoiceSpeed;
        VolumeSlider.Value = current.VoiceVolume;
        HotKeyBringToFront.Text = current.HotKeyBringToFront.ToString();
    }

    private void PopulateDevices()
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(d => d.FriendlyName)
                .ToList();

            DeviceComboBox.ItemsSource = devices;

            var match = devices.FirstOrDefault(d => d == _current.VoiceInput)
                        ?? devices.FirstOrDefault(d => d.Contains(_current.VoiceInput))
                        ?? devices.FirstOrDefault();

            DeviceComboBox.SelectedItem = match;
        }
        catch (Exception ex)
        {
            DeviceComboBox.ItemsSource = new[] { $"Ошибка: {ex.Message}" };
        }
    }

    private void PopulateVoices()
    {
        try
        {
#pragma warning disable CA1416
            using var synth = new SpeechSynthesizer();
            var voices = synth.GetInstalledVoices()
                .Select(v => v.VoiceInfo.Name)
                .ToList();
#pragma warning restore CA1416

            VoiceComboBox.ItemsSource = voices;

            var match = voices.FirstOrDefault(v => v == _current.ReaderName)
                        ?? voices.FirstOrDefault();

            VoiceComboBox.SelectedItem = match;
        }
        catch (Exception ex)
        {
            VoiceComboBox.ItemsSource = new[] { $"Ошибка: {ex.Message}" };
        }
    }

    private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) 
        => SpeedLabel?.Text = ((int)e.NewValue).ToString();

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) 
        => VolumeLabel?.Text = ((int)e.NewValue).ToString();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var device = DeviceComboBox.SelectedItem as string ?? string.Empty;
        var voice = VoiceComboBox.SelectedItem as string ?? string.Empty;

        ResultSettings = new Settings(
            voiceInput: device,
            voiceSpeed: (int)SpeedSlider.Value,
            voiceVolume: (int)VolumeSlider.Value,
            stdDelay: _current.StdDelay,
            readerName: voice,
            Settings.StringToKey(HotKeyBringToFront.Text)
        );

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var selectedKey = e.Key;
        HotKeyBringToFront.Text = selectedKey.ToString();
    }
}
