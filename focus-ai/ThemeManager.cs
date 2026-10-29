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
            var resources = Application.Current.Resources;
            if (dark)
                ApplyDark(resources);
            else
                ApplyLight(resources);
        }

        private static void ApplyDark(ResourceDictionary resources)
        {
            Set(resources, "BgRoot", "#0B0F19");
            Set(resources, "BgSidebar", "#0D1220");
            Set(resources, "BgMain", "#0B0F19");
            Set(resources, "BgCard", "#151C2C");
            Set(resources, "BgCardHover", "#1B2740");
            Set(resources, "BgNavActive", "#18253C");
            Set(resources, "BgNavHover", "#151D2F");
            Set(resources, "BgUserBar", "#101827");
            Set(resources, "BgSep", "#273247");
            Set(resources, "BgToggle", "#151D2F");
            Set(resources, "BgSection", "#101827");
            Set(resources, "BgInput", "#0D1422");
            Set(resources, "BgInputBorder", "#273247");
            Set(resources, "BgStatBlue", "#151C2C");
            Set(resources, "BgStatGreen", "#151C2C");
            Set(resources, "BgStatPurple", "#151C2C");
            Set(resources, "BgStatOrange", "#151C2C");
            Set(resources, "GameCard1Bg", "#151C2C");
            Set(resources, "GameCard2Bg", "#151C2C");
            Set(resources, "GameCard3Bg", "#151C2C");
            Set(resources, "GameCard4Bg", "#151C2C");
            Set(resources, "GameCard5Bg", "#151C2C");
            Set(resources, "GameCardLockBg", "#101827");
            Set(resources, "TxtPrimary", "#F5F7FC");
            Set(resources, "TxtSecondary", "#A6B0C2");
            Set(resources, "TxtMuted", "#7E8AA0");
            Set(resources, "TxtVersion", "#7E8AA0");
            Set(resources, "TxtNavActive", "#FFFFFF");
            Set(resources, "TxtOnline", "#22C55E");
            Set(resources, "TxtStatBlue", "#91B9FF");
            Set(resources, "TxtStatGreen", "#8CCFB2");
            Set(resources, "TxtStatPurple", "#B5ACF8");
            Set(resources, "TxtStatOrange", "#DAB58D");
            Set(resources, "AccentPrimary", "#3B82F6");
            Set(resources, "AccentHover", "#2563EB");
            Set(resources, "AccentSecBg", "#17243B");
            Set(resources, "AccentSecFg", "#8BB7FF");
            Set(resources, "AccentSecHov", "#202F4A");
            Set(resources, "RowBg", "#00000000");
            Set(resources, "RowHover", "#18243A");
            Set(resources, "RowBorder", "#273247");
            Set(resources, "RowNumBg", "#1B263B");
        }

        private static void ApplyLight(ResourceDictionary resources)
        {
            Set(resources, "BgRoot", "#F4F6FA");
            Set(resources, "BgSidebar", "#FBFCFE");
            Set(resources, "BgMain", "#F4F6FA");
            Set(resources, "BgCard", "#FFFFFF");
            Set(resources, "BgCardHover", "#F0F4FB");
            Set(resources, "BgNavActive", "#EAF1FC");
            Set(resources, "BgNavHover", "#F5F7FB");
            Set(resources, "BgUserBar", "#F8FAFD");
            Set(resources, "BgSep", "#E1E6EF");
            Set(resources, "BgToggle", "#FFFFFF");
            Set(resources, "BgSection", "#F7F9FC");
            Set(resources, "BgInput", "#F8FAFC");
            Set(resources, "BgInputBorder", "#DDE4EE");
            Set(resources, "BgStatBlue", "#FFFFFF");
            Set(resources, "BgStatGreen", "#FFFFFF");
            Set(resources, "BgStatPurple", "#FFFFFF");
            Set(resources, "BgStatOrange", "#FFFFFF");
            Set(resources, "GameCard1Bg", "#FFFFFF");
            Set(resources, "GameCard2Bg", "#FFFFFF");
            Set(resources, "GameCard3Bg", "#FFFFFF");
            Set(resources, "GameCard4Bg", "#FFFFFF");
            Set(resources, "GameCard5Bg", "#FFFFFF");
            Set(resources, "GameCardLockBg", "#EEF2F7");
            Set(resources, "TxtPrimary", "#101827");
            Set(resources, "TxtSecondary", "#536176");
            Set(resources, "TxtMuted", "#7E8AA0");
            Set(resources, "TxtVersion", "#7E8AA0");
            Set(resources, "TxtNavActive", "#173B78");
            Set(resources, "TxtOnline", "#16835D");
            Set(resources, "TxtStatBlue", "#2B65C8");
            Set(resources, "TxtStatGreen", "#28765B");
            Set(resources, "TxtStatPurple", "#6658B5");
            Set(resources, "TxtStatOrange", "#986B37");
            Set(resources, "AccentPrimary", "#2868D7");
            Set(resources, "AccentHover", "#1F55B4");
            Set(resources, "AccentSecBg", "#EAF1FC");
            Set(resources, "AccentSecFg", "#2459AF");
            Set(resources, "AccentSecHov", "#DBE7FA");
            Set(resources, "RowBg", "#FFFFFF");
            Set(resources, "RowHover", "#F4F7FC");
            Set(resources, "RowBorder", "#E1E6EF");
            Set(resources, "RowNumBg", "#EEF2F7");
        }

        private static void Set(ResourceDictionary resources, string key, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }
    }
}
