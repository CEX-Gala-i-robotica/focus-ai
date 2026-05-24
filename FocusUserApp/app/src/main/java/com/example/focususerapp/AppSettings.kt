package com.example.focususerapp

import android.content.Context

object AppSettings {
    private const val PREFS = "focus_app_settings"
    private const val KEY_THEME = "theme_mode"
    private const val KEY_LANGUAGE = "language"

    const val THEME_DARK = "dark"
    const val THEME_LIGHT = "light"
    const val LANG_EN = "en"
    const val LANG_RO = "ro"
    const val LANG_TR = "tr"

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
}
