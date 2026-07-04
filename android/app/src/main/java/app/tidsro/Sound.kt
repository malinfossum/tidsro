package app.tidsro

/** Same ordinals as the desktop app's SoundChoice, so the meaning of a saved value matches. */
enum class Sound(val label: String, val rawRes: Int?) {
    NONE("Silent", null),
    SOFT_CHIME("Soft chime", R.raw.soft_chime),
    MARIMBA("Marimba", R.raw.marimba),
    BELL("Bell", R.raw.bell),
    PIANO_JINGLE("Piano jingle", R.raw.piano_jingle),
    ELECTRIC_PIANO_JINGLE("Electric piano jingle", R.raw.electric_piano_jingle),
    BELL_JINGLE("Bell jingle", R.raw.bell_jingle);

    companion object {
        fun from(ordinal: Int): Sound {
            val all = values()
            return if (ordinal in all.indices) all[ordinal] else NONE
        }

        fun labels(): List<String> = values().map { it.label }
    }
}
