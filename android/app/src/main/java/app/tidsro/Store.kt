package app.tidsro

import android.content.Context
import org.json.JSONArray

/** JSON-in-SharedPreferences persistence; the lists are tiny. */
object Store {
    private const val PREFS = "tidsro"

    private fun prefs(c: Context) =
        c.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    fun timersRaw(c: Context): String = prefs(c).getString("timers", "") ?: ""
    fun alarmsRaw(c: Context): String = prefs(c).getString("alarms", "") ?: ""

    @Synchronized
    fun loadTimers(c: Context): MutableList<CountdownTimer> {
        val raw = prefs(c).getString("timers", null) ?: return mutableListOf()
        return try {
            val arr = JSONArray(raw)
            MutableList(arr.length()) { CountdownTimer.fromJson(arr.getJSONObject(it)) }
        } catch (e: Exception) {
            mutableListOf()
        }
    }

    @Synchronized
    fun saveTimers(c: Context, timers: List<CountdownTimer>) {
        val arr = JSONArray()
        timers.forEach { arr.put(it.toJson()) }
        prefs(c).edit().putString("timers", arr.toString()).apply()
    }

    @Synchronized
    fun loadAlarms(c: Context): MutableList<Alarm> {
        val raw = prefs(c).getString("alarms", null) ?: return mutableListOf()
        return try {
            val arr = JSONArray(raw)
            MutableList(arr.length()) { Alarm.fromJson(arr.getJSONObject(it)) }
        } catch (e: Exception) {
            mutableListOf()
        }
    }

    @Synchronized
    fun saveAlarms(c: Context, alarms: List<Alarm>) {
        val arr = JSONArray()
        alarms.forEach { arr.put(it.toJson()) }
        prefs(c).edit().putString("alarms", arr.toString()).apply()
    }

    fun defaultSound(c: Context): Int = prefs(c).getInt("defaultSound", 0)

    fun setDefaultSound(c: Context, value: Int) {
        prefs(c).edit().putInt("defaultSound", value).apply()
    }
}
