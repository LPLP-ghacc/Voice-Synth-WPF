using System.Windows.Input;

namespace VoiceSynthWPF;

public class SnippetModel
{
    public string Text { get; set; } = string.Empty;
    public Key ActivationKey { get; set; }
}