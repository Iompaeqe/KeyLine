using System.Media;

namespace MacroSpammer;

public partial class MainWindow
{
    private void PlayMacroSound()
    {
        if (!_settings.PlaySoundOnMacroStartStop)
            return;

        PlaySelectedSystemSound();
    }

    public void PreviewPlaybackSound()
    {
        PlaySelectedSystemSound();
    }

    private void PlaySelectedSystemSound()
    {
        try
        {
            var sound = _settings.PlaybackSoundName switch
            {
                "Asterisk" => SystemSounds.Asterisk,
                "Exclamation" => SystemSounds.Exclamation,
                "Hand" => SystemSounds.Hand,
                "Question" => SystemSounds.Question,
                _ => SystemSounds.Beep
            };

            sound.Play();
        }
        catch
        {
        }
    }
}
