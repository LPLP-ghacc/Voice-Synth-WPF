using System.Windows.Input;
using ConseqConcatenation;

namespace VoiceSynthWPF;

public class SnippetModel : IConseqData
{
    public string Text { get; set; } = string.Empty;
    public Key ActivationKey { get; set; }
}