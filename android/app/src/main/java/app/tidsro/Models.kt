package app.tidsro

import org.json.JSONObject
import java.util.UUID

class CountdownTimer(
    val id: String = UUID.randomUUID().toString(),
    var label: String? = null,
    var originalMs: Long,
    var endsAt: Long? = null,            // epoch millis; set while running
    var pausedRemainingMs: Long? = null, // set while paused
    var sound: Int = 0
) {
    val running: Boolean get() = endsAt != null

    fun remainingMs(now: Long): Long =
        pausedRemainingMs ?: (((endsAt ?: now) - now).coerceAtLeast(0))

    fun toJson(): JSONObject = JSONObject().apply {
        put("id", id)
        put("label", label ?: JSONObject.NULL)
        put("originalMs", originalMs)
        put("endsAt", endsAt ?: JSONObject.NULL)
        put("pausedRemainingMs", pausedRemainingMs ?: JSONObject.NULL)
        put("sound", sound)
    }

    companion object {
        fun fromJson(o: JSONObject): CountdownTimer = CountdownTimer(
            id = o.getString("id"),
            label = if (o.isNull("label")) null else o.getString("label"),
            originalMs = o.getLong("originalMs"),
            endsAt = if (o.isNull("endsAt")) null else o.getLong("endsAt"),
            pausedRemainingMs = if (o.isNull("pausedRemainingMs")) null else o.getLong("pausedRemainingMs"),
            sound = o.optInt("sound", 0)
        )
    }
}

/** One schedule entry. days == 0 means a one-shot; otherwise a weekday bitmask (Mon=1 .. Sun=64). */
class Alarm(
    val id: String = UUID.randomUUID().toString(),
    var hour: Int,
    var minute: Int,
    var days: Int = 0,
    var label: String? = null,
    var sound: Int = 0,
    var warnBefore: Boolean = false,
    var enabled: Boolean = true,
    var nextFireAt: Long = 0
) {
    fun toJson(): JSONObject = JSONObject().apply {
        put("id", id)
        put("hour", hour)
        put("minute", minute)
        put("days", days)
        put("label", label ?: JSONObject.NULL)
        put("sound", sound)
        put("warnBefore", warnBefore)
        put("enabled", enabled)
        put("nextFireAt", nextFireAt)
    }

    companion object {
        fun fromJson(o: JSONObject): Alarm = Alarm(
            id = o.getString("id"),
            hour = o.getInt("hour"),
            minute = o.getInt("minute"),
            days = o.optInt("days", 0),
            label = if (o.isNull("label")) null else o.getString("label"),
            sound = o.optInt("sound", 0),
            warnBefore = o.optBoolean("warnBefore", false),
            enabled = o.optBoolean("enabled", true),
            nextFireAt = o.getLong("nextFireAt")
        )
    }
}
