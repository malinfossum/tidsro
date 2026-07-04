package app.tidsro

import java.time.DayOfWeek
import java.time.Instant
import java.time.ZoneId
import java.time.ZonedDateTime

/** Ports of the desktop app's CountdownRules, ClockTimeRules, and RecurrenceRules. */
object Rules {
    const val MAX_DURATION_MS = 24L * 60 * 60 * 1000
    const val ALL_DAYS = 127
    const val WEEKDAYS = 31
    const val WEEKENDS = 96

    data class ParsedDuration(val ms: Long?, val error: String?)
    data class ParsedTime(val hour: Int, val minute: Int)
    data class ParsedClock(val time: ParsedTime?, val error: String?)

    /** "25" = minutes, "MM:SS", or "H:MM:SS". */
    fun parseDuration(input: String?): ParsedDuration {
        if (input.isNullOrBlank()) return ParsedDuration(null, "Enter a duration.")
        val parts = input.trim().split(":").map { it.trim() }
        if (parts.size > 3) return ParsedDuration(null, "Use minutes, MM:SS, or H:MM:SS.")
        val nums = parts.map { it.toIntOrNull() ?: return ParsedDuration(null, "Use only numbers and colons.") }
        val h: Int
        val m: Int
        val s: Int
        when (nums.size) {
            1 -> { h = 0; m = nums[0]; s = 0 }
            2 -> { h = 0; m = nums[0]; s = nums[1] }
            else -> { h = nums[0]; m = nums[1]; s = nums[2] }
        }
        if (h < 0 || m < 0 || s < 0 || s > 59 || (nums.size == 3 && m > 59))
            return ParsedDuration(null, "Minutes and seconds must be 0-59.")
        val ms = ((h * 3600L) + (m * 60L) + s) * 1000L
        if (ms <= 0) return ParsedDuration(null, "Duration must be greater than zero.")
        if (ms > MAX_DURATION_MS) return ParsedDuration(null, "Duration can be at most 24 hours.")
        return ParsedDuration(ms, null)
    }

    /** "14:30", or bare digits: "9" -> 09:00, "930" -> 09:30, "1430" -> 14:30. */
    fun parseClockTime(input: String?): ParsedClock {
        if (input.isNullOrBlank()) return ParsedClock(null, "Enter a time as HH:MM.")
        val trimmed = input.trim()
        if (trimmed.contains(":")) {
            val parts = trimmed.split(":").map { it.trim() }
            if (parts.size != 2) return ParsedClock(null, "Use HH:MM, e.g. 14:30.")
            val hour = parts[0].toIntOrNull() ?: return ParsedClock(null, "Use only numbers, e.g. 14:30.")
            val minute = parts[1].toIntOrNull() ?: return ParsedClock(null, "Use only numbers, e.g. 14:30.")
            return validated(hour, minute)
        }
        if (trimmed.any { it !in '0'..'9' }) return ParsedClock(null, "Use HH:MM, e.g. 14:30.")
        return when (trimmed.length) {
            1, 2 -> validated(trimmed.toInt(), 0)
            3 -> validated(trimmed.substring(0, 1).toInt(), trimmed.substring(1).toInt())
            4 -> validated(trimmed.substring(0, 2).toInt(), trimmed.substring(2).toInt())
            else -> ParsedClock(null, "Use HH:MM, e.g. 14:30.")
        }
    }

    private fun validated(hour: Int, minute: Int): ParsedClock {
        if (hour !in 0..23) return ParsedClock(null, "Hour must be 0-23.")
        if (minute !in 0..59) return ParsedClock(null, "Minute must be 0-59.")
        return ParsedClock(ParsedTime(hour, minute), null)
    }

    fun dayFlag(d: DayOfWeek): Int = when (d) {
        DayOfWeek.MONDAY -> 1
        DayOfWeek.TUESDAY -> 2
        DayOfWeek.WEDNESDAY -> 4
        DayOfWeek.THURSDAY -> 8
        DayOfWeek.FRIDAY -> 16
        DayOfWeek.SATURDAY -> 32
        DayOfWeek.SUNDAY -> 64
    }

    private fun zoned(nowMs: Long): ZonedDateTime =
        ZonedDateTime.ofInstant(Instant.ofEpochMilli(nowMs), ZoneId.systemDefault())

    /** The next time HH:MM occurs: today if still ahead, else tomorrow. */
    fun nextOnceFireAt(nowMs: Long, hour: Int, minute: Int): Long {
        val now = zoned(nowMs)
        var c = now.withHour(hour).withMinute(minute).withSecond(0).withNano(0)
        if (!c.isAfter(now)) c = c.plusDays(1)
        return c.toInstant().toEpochMilli()
    }

    /** The soonest matching weekday at HH:MM strictly after now. */
    fun nextOccurrence(nowMs: Long, hour: Int, minute: Int, days: Int): Long {
        val now = zoned(nowMs)
        for (add in 0..7) {
            val c = now.plusDays(add.toLong()).withHour(hour).withMinute(minute).withSecond(0).withNano(0)
            if (c.isAfter(now) && (days and dayFlag(c.dayOfWeek)) != 0) return c.toInstant().toEpochMilli()
        }
        return nextOnceFireAt(nowMs, hour, minute) // unreachable with a non-empty day set
    }

    fun nextFireAtFor(alarm: Alarm, nowMs: Long): Long =
        if (alarm.days == 0) nextOnceFireAt(nowMs, alarm.hour, alarm.minute)
        else nextOccurrence(nowMs, alarm.hour, alarm.minute, alarm.days)

    fun cadenceLabel(days: Int): String = when (days) {
        0 -> "once"
        ALL_DAYS -> "Daily"
        WEEKDAYS -> "Weekdays"
        WEEKENDS -> "Weekends"
        else -> listOf("Mon" to 1, "Tue" to 2, "Wed" to 4, "Thu" to 8, "Fri" to 16, "Sat" to 32, "Sun" to 64)
            .filter { (days and it.second) != 0 }
            .joinToString(" ") { it.first }
    }

    fun formatMs(ms: Long): String {
        val total = (ms + 999) / 1000
        val h = total / 3600
        val m = (total % 3600) / 60
        val s = total % 60
        return if (h > 0) String.format("%d:%02d:%02d", h, m, s) else String.format("%02d:%02d", m, s)
    }
}
