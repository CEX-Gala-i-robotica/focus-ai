using System.Windows;
using System.Windows.Media;

namespace focus_ai
{
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; } = true;

        public static void Apply(bool dark)
        {
            IsDark = dark;
            var r = Application.Current.Resources;
            if (dark) ApplyDark(r);
            else      ApplyLight(r);
        }

        private static void ApplyDark(ResourceDictionary r)
        {
            // Fonduri
            S(r, "BgRoot",          "#0B0F19");
            S(r, "BgSidebar",       "#0D1220");
            S(r, "BgMain",          "#0B0F19");
            S(r, "BgCard",          "#1A2035");
            S(r, "BgCardHover",     "#222C46");
            S(r, "BgNavActive",     "#182036");
            S(r, "BgNavHover",      "#151C2E");
            S(r, "BgUserBar",       "#111827");
            S(r, "BgSep",           "#252D41");
            S(r, "BgToggle",        "#171F33");
            S(r, "BgSection",       "#111827");
            S(r, "BgInput",         "#111827");
            S(r, "BgInputBorder",   "#252D41");

            // Stat cards
            S(r, "BgStatBlue",      "#1A2035");
            S(r, "BgStatGreen",     "#1A2035");
            S(r, "BgStatPurple",    "#1A2035");
            S(r, "BgStatOrange",    "#1A2035");

            // Game cards
            S(r, "GameCard1Bg",     "#1A2035");
            S(r, "GameCard2Bg",     "#1A2035");
            S(r, "GameCard3Bg",     "#1A2035");
            S(r, "GameCard4Bg",     "#1A2035");
            S(r, "GameCard5Bg",     "#1A2035");
            S(r, "GameCardLockBg",  "#111827");

            // Texte
            S(r, "TxtPrimary",      "#F8FAFC");
            S(r, "TxtSecondary",    "#A0AEC0");
            S(r, "TxtMuted",        "#718096");
            S(r, "TxtVersion",      "#718096");
            S(r, "TxtNavActive",    "#FFFFFF");
            S(r, "TxtOnline",       "#22C55E");

            // Stat labels
            S(r, "TxtStatBlue",     "#60A5FA");
            S(r, "TxtStatGreen",    "#4ADE80");
            S(r, "TxtStatPurple",   "#A78BFA");
            S(r, "TxtStatOrange",   "#FB923C");

            // Accent
            S(r, "AccentPrimary",   "#3B82F6");
            S(r, "AccentHover",     "#2563EB");
            S(r, "AccentSecBg",     "#172036");
            S(r, "AccentSecFg",     "#60A5FA");
            S(r, "AccentSecHov",    "#222C46");

            // Tabel
            S(r, "RowBg",           "#00000000");
            S(r, "RowHover",        "#222C46");
            S(r, "RowBorder",       "#252D41");
            S(r, "RowNumBg",        "#161B2C");
        }

        private static void ApplyLight(ResourceDictionary r)
        {
            // Fonduri (culori armonizate cu Dashboard)
            S(r, "BgRoot",          "#F4F6FB");
            S(r, "BgSidebar",       "#FFFFFF");
            S(r, "BgMain",          "#F4F6FB");
            S(r, "BgCard",          "#FFFFFF");
            S(r, "BgCardHover",     "#EDF2FF");
            S(r, "BgNavActive",     "#EDF2FF");
            S(r, "BgNavHover",      "#F5F7FF");
            S(r, "BgUserBar",       "#F5F7FF");
            S(r, "BgSep",           "#E2E8F0");
            S(r, "BgToggle",        "#DBEAFE");
            S(r, "BgSection",       "#FFFFFF");
            S(r, "BgInput",         "#F8FAFC");
            S(r, "BgInputBorder",   "#CBD5E1");

            // Stat cards
            S(r, "BgStatBlue",      "#DBEAFE");
            S(r, "BgStatGreen",     "#DCFCE7");
            S(r, "BgStatPurple",    "#EDE9FE");
            S(r, "BgStatOrange",    "#FFEDD5");

            // Game cards
            S(r, "GameCard1Bg",     "#DBEAFE");
            S(r, "GameCard2Bg",     "#DCFCE7");
            S(r, "GameCard3Bg",     "#EDE9FE");
            S(r, "GameCard4Bg",     "#FFEDD5");
            S(r, "GameCard5Bg",     "#F1F5F9");
            S(r, "GameCardLockBg",  "#E2E8F0");

            // Texte
            S(r, "TxtPrimary",      "#0F172A");
            S(r, "TxtSecondary",    "#475569");
            S(r, "TxtMuted",        "#94A3B8");
            S(r, "TxtVersion",      "#94A3B8");
            S(r, "TxtNavActive",    "#1E40AF");
            S(r, "TxtOnline",       "#16A34A");

            // Stat labels
            S(r, "TxtStatBlue",     "#1D4ED8");
            S(r, "TxtStatGreen",    "#15803D");
            S(r, "TxtStatPurple",   "#6D28D9");
            S(r, "TxtStatOrange",   "#C2410C");

            // Accent
            S(r, "AccentPrimary",   "#2563EB");
            S(r, "AccentHover",     "#1D4ED8");
            S(r, "AccentSecBg",     "#DBEAFE");
            S(r, "AccentSecFg",     "#1D4ED8");
            S(r, "AccentSecHov",    "#BFDBFE");

            // Tabel
            S(r, "RowBg",           "#FFFFFF");
            S(r, "RowHover",        "#F5F7FF");
            S(r, "RowBorder",       "#E2E8F0");
            S(r, "RowNumBg",        "#F1F5F9");
        }

        private static void S(ResourceDictionary r, string key, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            r[key] = brush;
        }
    }
}
