package com.example.focususerapp

import android.content.Context

object AppSettings {
    private const val PREFS = "focus_app_settings"
    private const val KEY_THEME = "theme_mode"
    private const val KEY_LANGUAGE = "language"
    private const val KEY_CUSTOM_THEME = "custom_theme_enabled"
    private const val KEY_PRESET = "custom_theme_preset"
    private const val KEY_ROOT = "custom_root"
    private const val KEY_SURFACE = "custom_surface"
    private const val KEY_SURFACE_ALT = "custom_surface_alt"
    private const val KEY_TEXT = "custom_text"
    private const val KEY_MUTED = "custom_muted"
    private const val KEY_ACCENT = "custom_accent"
    private const val KEY_ACCENT_END = "custom_accent_end"
    private const val KEY_POSITIVE = "custom_positive"
    private const val KEY_NEGATIVE = "custom_negative"
    private const val KEY_DIVIDER = "custom_divider"

    const val THEME_DARK = "dark"
    const val THEME_LIGHT = "light"
    const val LANG_EN = "en"
    const val LANG_RO = "ro"
    const val LANG_TR = "tr"

    data class CustomTheme(
        val preset: String,
        val root: Int,
        val surface: Int,
        val surfaceAlt: Int,
        val text: Int,
        val muted: Int,
        val accent: Int,
        val accentEnd: Int,
        val positive: Int,
        val negative: Int,
        val divider: Int
    )

    data class ThemePreset(
        val id: String,
        val label: String,
        val root: Int,
        val surface: Int,
        val surfaceAlt: Int,
        val text: Int,
        val muted: Int,
        val accent: Int,
        val accentEnd: Int,
        val positive: Int,
        val negative: Int,
        val divider: Int
    )

    val presets = listOf(
        ThemePreset("neon", "Neon Pulse", 0xFF10121C.toInt(), 0xFF191D2B.toInt(), 0xFF22283A.toInt(), 0xFFFFFFFF.toInt(), 0xFF9AA3B8.toInt(), 0xFF00E5FF.toInt(), 0xFF0055FF.toInt(), 0xFF00FF66.toInt(), 0xFFFF3366.toInt(), 0xFF2A3248.toInt()),
        ThemePreset("matrix", "Matrix", 0xFF06100B.toInt(), 0xFF0C1C13.toInt(), 0xFF132A1B.toInt(), 0xFFEFFFF5.toInt(), 0xFF8ACCA3.toInt(), 0xFF00FF66.toInt(), 0xFF00B85C.toInt(), 0xFF94FFB0.toInt(), 0xFFFF4D6D.toInt(), 0xFF1E3B28.toInt()),
        ThemePreset("sunset", "Sunset", 0xFF190F18.toInt(), 0xFF261723.toInt(), 0xFF382034.toInt(), 0xFFFFF7F0.toInt(), 0xFFD6A8B8.toInt(), 0xFFFF7A59.toInt(), 0xFFFFC857.toInt(), 0xFF72F2A1.toInt(), 0xFFFF477E.toInt(), 0xFF4B2B3E.toInt()),
        ThemePreset("ocean", "Ocean", 0xFF061821.toInt(), 0xFF0B2430.toInt(), 0xFF123545.toInt(), 0xFFEAFBFF.toInt(), 0xFF91B8C8.toInt(), 0xFF00B8D9.toInt(), 0xFF006DFF.toInt(), 0xFF2DFFB3.toInt(), 0xFFFF4B6E.toInt(), 0xFF1B4658.toInt()),
        ThemePreset("mono", "Mono Pro", 0xFFF4F6FA.toInt(), 0xFFFFFFFF.toInt(), 0xFFE5E9F2.toInt(), 0xFF101423.toInt(), 0xFF667085.toInt(), 0xFF111827.toInt(), 0xFF475467.toInt(), 0xFF00A86B.toInt(), 0xFFE42355.toInt(), 0xFFD0D5DD.toInt())
    )

    fun theme(context: Context): String {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString(KEY_THEME, THEME_DARK) ?: THEME_DARK
    }

    fun isDark(context: Context): Boolean = theme(context) == THEME_DARK

    fun setTheme(context: Context, theme: String) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putString(KEY_THEME, theme)
            .apply()
    }

    fun language(context: Context): String {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString(KEY_LANGUAGE, LANG_EN) ?: LANG_EN
    }

    fun isRomanian(context: Context): Boolean = language(context) == LANG_RO

    fun setLanguage(context: Context, language: String) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putString(KEY_LANGUAGE, language)
            .apply()
    }

    fun customThemeEnabled(context: Context): Boolean {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getBoolean(KEY_CUSTOM_THEME, false)
    }

    fun setCustomThemeEnabled(context: Context, enabled: Boolean) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putBoolean(KEY_CUSTOM_THEME, enabled)
            .apply()
    }

    fun customTheme(context: Context): CustomTheme {
        val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        val fallback = presets.first()
        return CustomTheme(
            preset = prefs.getString(KEY_PRESET, fallback.id) ?: fallback.id,
            root = prefs.getInt(KEY_ROOT, fallback.root),
            surface = prefs.getInt(KEY_SURFACE, fallback.surface),
            surfaceAlt = prefs.getInt(KEY_SURFACE_ALT, fallback.surfaceAlt),
            text = prefs.getInt(KEY_TEXT, fallback.text),
            muted = prefs.getInt(KEY_MUTED, fallback.muted),
            accent = prefs.getInt(KEY_ACCENT, fallback.accent),
            accentEnd = prefs.getInt(KEY_ACCENT_END, fallback.accentEnd),
            positive = prefs.getInt(KEY_POSITIVE, fallback.positive),
            negative = prefs.getInt(KEY_NEGATIVE, fallback.negative),
            divider = prefs.getInt(KEY_DIVIDER, fallback.divider)
        )
    }

    fun applyPreset(context: Context, presetId: String) {
        val preset = presets.firstOrNull { it.id == presetId } ?: presets.first()
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putBoolean(KEY_CUSTOM_THEME, true)
            .putString(KEY_PRESET, preset.id)
            .putInt(KEY_ROOT, preset.root)
            .putInt(KEY_SURFACE, preset.surface)
            .putInt(KEY_SURFACE_ALT, preset.surfaceAlt)
            .putInt(KEY_TEXT, preset.text)
            .putInt(KEY_MUTED, preset.muted)
            .putInt(KEY_ACCENT, preset.accent)
            .putInt(KEY_ACCENT_END, preset.accentEnd)
            .putInt(KEY_POSITIVE, preset.positive)
            .putInt(KEY_NEGATIVE, preset.negative)
            .putInt(KEY_DIVIDER, preset.divider)
            .apply()
    }

    fun setCustomColor(context: Context, role: String, color: Int) {
        val key = when (role) {
            "root" -> KEY_ROOT
            "surface" -> KEY_SURFACE
            "surfaceAlt" -> KEY_SURFACE_ALT
            "text" -> KEY_TEXT
            "muted" -> KEY_MUTED
            "accent" -> KEY_ACCENT
            "accentEnd" -> KEY_ACCENT_END
            "positive" -> KEY_POSITIVE
            "negative" -> KEY_NEGATIVE
            "divider" -> KEY_DIVIDER
            else -> return
        }
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putBoolean(KEY_CUSTOM_THEME, true)
            .putString(KEY_PRESET, "custom")
            .putInt(key, color)
            .apply()
    }

    fun resetCustomTheme(context: Context) {
        applyPreset(context, presets.first().id)
    }
}
