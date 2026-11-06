using System;

/// <summary>
/// Rule-based career teaser shown at the end of the tutorial. NO ML, NO real
/// data - it mirrors the framing of the real Career Result screen but produces a
/// generalized, clearly-fake professional suggestion from the single skill the
/// player chose. Renders through the reusable vc_DialogPanel, so it needs no UI
/// assets of its own.
/// </summary>
public static class vc_TutorialEndScreen
{
    private struct Profile
    {
        public string Trait;    // RIASEC trait name
        public string Careers;  // generalized, real-world career titles
        public Profile(string trait, string careers) { Trait = trait; Careers = careers; }
    }

    private static Profile Lookup(string letter)
    {
        switch ((letter ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "R": return new Profile("Realistic",      "Engineer, Builder, or Technician");
            case "I": return new Profile("Investigative",  "Scientist, Researcher, or Doctor");
            case "A": return new Profile("Artistic",       "Designer, Writer, or Artist");
            case "S": return new Profile("Social",         "Teacher, Nurse, or Counselor");
            case "E": return new Profile("Enterprising",   "Entrepreneur, Manager, or Lawyer");
            case "C": return new Profile("Conventional",   "Accountant, Analyst, or Administrator");
            default:  return new Profile("Undecided",      "many different paths");
        }
    }

    public static void Show(string riasecLetter, string skillName, Action onDone)
    {
        Profile p = Lookup(riasecLetter);
        string name = string.IsNullOrEmpty(skillName) ? "that skill" : skillName;

        // Kept short (UI V2): the path, why, and that it's not a real result.
        string body =
            $"Maybe: {p.Careers}.\n\n" +
            $"You picked {name} ({p.Trait}). Just one idea, not your result.\n\n" +
            "Play the full game to find your fit.";

        if (vc_DialogPanel.Instance != null)
            vc_DialogPanel.Instance.ShowMessage("A possible path", body, () => ShowLoops(onDone));
        else
            onDone?.Invoke();
    }

    /// <summary>The last tutorial page: what carries over from one playthrough to the next.</summary>
    private static void ShowLoops(Action onDone)
    {
        const string body =
            "Each playthrough is a loop. Each loop ends in a disaster.\n\n" +
            "Go Back to play again. You keep remembered skills and keepsakes.";

        if (vc_DialogPanel.Instance != null)
            vc_DialogPanel.Instance.ShowMessage("Loops", body, onDone);
        else
            onDone?.Invoke();
    }
}
