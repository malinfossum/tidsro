package app.tidsro

import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationManagerCompat
import java.time.Instant
import java.time.ZoneId
import java.time.ZonedDateTime

class AlarmReceiver : BroadcastReceiver() {

    override fun onReceive(context: Context, intent: Intent) {
        when (intent.action) {
            AlarmScheduler.ACTION_ALARM_FIRE -> onAlarmFire(context, intent.getStringExtra("id") ?: return)
            AlarmScheduler.ACTION_ALARM_WARN -> onAlarmWarn(context, intent.getStringExtra("id") ?: return)
            AlarmScheduler.ACTION_TIMER_FIRE -> onTimerFire(context, intent.getStringExtra("id") ?: return)
            AlarmScheduler.ACTION_SNOOZE -> onSnooze(context, intent)
            AlarmScheduler.ACTION_TIMER_PLUS5 -> onTimerAgain(context, intent, AlarmScheduler.SNOOZE_MS)
            AlarmScheduler.ACTION_TIMER_RESTART -> onTimerAgain(context, intent, null)
        }
    }

    private fun onAlarmFire(c: Context, id: String) {
        val alarms = Store.loadAlarms(c)
        val a = alarms.find { it.id == id } ?: return
        if (!a.enabled) return
        showAlarmNotification(c, a)
        if (a.days == 0) {
            alarms.removeAll { it.id == id }
        } else {
            a.nextFireAt = Rules.nextOccurrence(System.currentTimeMillis(), a.hour, a.minute, a.days)
            AlarmScheduler.armAlarm(c, a)
        }
        Store.saveAlarms(c, alarms)
    }

    private fun onAlarmWarn(c: Context, id: String) {
        val a = Store.loadAlarms(c).find { it.id == id } ?: return
        if (!a.enabled || !a.warnBefore) return
        val text = String.format("%02d:%02d", a.hour, a.minute) + (a.label?.let { " - $it" } ?: "")
        val n = Notifications.baseBuilder(c, Notifications.CHANNEL_WARN)
            .setContentTitle(c.getString(R.string.warn_title))
            .setContentText(text)
            .build()
        Notifications.notify(c, ("warn" + id).hashCode(), n)
    }

    private fun onTimerFire(c: Context, id: String) {
        val timers = Store.loadTimers(c)
        val t = timers.find { it.id == id } ?: return
        showTimerNotification(c, t)
        timers.removeAll { it.id == id }
        Store.saveTimers(c, timers)
    }

    /** Snooze +5: a fresh one-shot five minutes out, keeping the label and sound. */
    private fun onSnooze(c: Context, intent: Intent) {
        NotificationManagerCompat.from(c).cancel(intent.getIntExtra("notifId", 0))
        val fireAt = System.currentTimeMillis() + AlarmScheduler.SNOOZE_MS
        val z: ZonedDateTime = ZonedDateTime.ofInstant(Instant.ofEpochMilli(fireAt), ZoneId.systemDefault())
        val a = Alarm(
            hour = z.hour,
            minute = z.minute,
            days = 0,
            label = intent.getStringExtra("label"),
            sound = intent.getIntExtra("sound", 0),
            enabled = true,
            nextFireAt = fireAt
        )
        val alarms = Store.loadAlarms(c)
        alarms.add(a)
        Store.saveAlarms(c, alarms)
        AlarmScheduler.armAlarm(c, a)
    }

    /** +5 min arms a fresh 5-minute countdown; Restart re-runs the original duration. */
    private fun onTimerAgain(c: Context, intent: Intent, fixedMs: Long?) {
        NotificationManagerCompat.from(c).cancel(intent.getIntExtra("notifId", 0))
        val ms = fixedMs ?: intent.getLongExtra("originalMs", AlarmScheduler.SNOOZE_MS)
        val t = CountdownTimer(
            label = intent.getStringExtra("label"),
            originalMs = ms,
            endsAt = System.currentTimeMillis() + ms,
            sound = intent.getIntExtra("sound", 0)
        )
        val timers = Store.loadTimers(c)
        timers.add(t)
        Store.saveTimers(c, timers)
        AlarmScheduler.armTimer(c, t)
    }

    companion object {
        fun showAlarmNotification(c: Context, a: Alarm) {
            val notifId = ("alarm" + a.id).hashCode()
            val snooze = Intent(c, AlarmReceiver::class.java)
                .setAction(AlarmScheduler.ACTION_SNOOZE)
                .putExtra("id", a.id)
                .putExtra("label", a.label)
                .putExtra("sound", a.sound)
                .putExtra("notifId", notifId)
            val snoozePi = PendingIntent.getBroadcast(
                c, ("snooze" + a.id).hashCode(), snooze,
                PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
            )
            val text = String.format("%02d:%02d", a.hour, a.minute) +
                if (a.days != 0) " - " + Rules.cadenceLabel(a.days) else ""
            val n = Notifications.baseBuilder(c, Notifications.channelFor(a.sound))
                .setContentTitle(a.label ?: c.getString(R.string.alarm_title))
                .setContentText(text)
                .addAction(0, c.getString(R.string.action_snooze), snoozePi)
                .build()
            Notifications.notify(c, notifId, n)
        }

        fun showTimerNotification(c: Context, t: CountdownTimer) {
            val notifId = ("timer" + t.id).hashCode()

            fun action(actionName: String, req: String): PendingIntent {
                val i = Intent(c, AlarmReceiver::class.java)
                    .setAction(actionName)
                    .putExtra("id", t.id)
                    .putExtra("label", t.label)
                    .putExtra("sound", t.sound)
                    .putExtra("originalMs", t.originalMs)
                    .putExtra("notifId", notifId)
                return PendingIntent.getBroadcast(
                    c, (req + t.id).hashCode(), i,
                    PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
                )
            }

            val title = t.label ?: c.getString(R.string.timer_finished)
            val text = if (t.label != null)
                c.getString(R.string.timer_finished) + " - " + Rules.formatMs(t.originalMs)
            else
                Rules.formatMs(t.originalMs)
            val n = Notifications.baseBuilder(c, Notifications.channelFor(t.sound))
                .setContentTitle(title)
                .setContentText(text)
                .addAction(0, c.getString(R.string.action_plus5), action(AlarmScheduler.ACTION_TIMER_PLUS5, "plus5"))
                .addAction(0, c.getString(R.string.action_restart), action(AlarmScheduler.ACTION_TIMER_RESTART, "restart"))
                .build()
            Notifications.notify(c, notifId, n)
        }
    }
}
