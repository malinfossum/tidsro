package app.tidsro

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build

object AlarmScheduler {
    const val ACTION_ALARM_FIRE = "app.tidsro.ALARM_FIRE"
    const val ACTION_ALARM_WARN = "app.tidsro.ALARM_WARN"
    const val ACTION_TIMER_FIRE = "app.tidsro.TIMER_FIRE"
    const val ACTION_SNOOZE = "app.tidsro.SNOOZE"
    const val ACTION_TIMER_PLUS5 = "app.tidsro.TIMER_PLUS5"
    const val ACTION_TIMER_RESTART = "app.tidsro.TIMER_RESTART"

    const val GRACE_MS = 5L * 60 * 1000
    const val WARN_LEAD_MS = 5L * 60 * 1000
    const val SNOOZE_MS = 5L * 60 * 1000

    private fun am(c: Context) = c.getSystemService(Context.ALARM_SERVICE) as AlarmManager

    private fun pi(c: Context, action: String, id: String): PendingIntent {
        val intent = Intent(c, AlarmReceiver::class.java).setAction(action).putExtra("id", id)
        val req = (action + id).hashCode()
        return PendingIntent.getBroadcast(
            c, req, intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )
    }

    private fun setExact(c: Context, at: Long, op: PendingIntent) {
        val mgr = am(c)
        try {
            if (Build.VERSION.SDK_INT < 31 || mgr.canScheduleExactAlarms()) {
                mgr.setAlarmClock(AlarmManager.AlarmClockInfo(at, Notifications.contentIntent(c)), op)
            } else {
                mgr.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, op)
            }
        } catch (e: SecurityException) {
            mgr.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, op)
        }
    }

    fun armAlarm(c: Context, a: Alarm) {
        cancelAlarm(c, a.id)
        if (!a.enabled) return
        setExact(c, a.nextFireAt, pi(c, ACTION_ALARM_FIRE, a.id))
        val warnAt = a.nextFireAt - WARN_LEAD_MS
        if (a.warnBefore && warnAt > System.currentTimeMillis()) {
            setExact(c, warnAt, pi(c, ACTION_ALARM_WARN, a.id))
        }
    }

    fun cancelAlarm(c: Context, id: String) {
        am(c).cancel(pi(c, ACTION_ALARM_FIRE, id))
        am(c).cancel(pi(c, ACTION_ALARM_WARN, id))
    }

    fun armTimer(c: Context, t: CountdownTimer) {
        cancelTimer(c, t.id)
        val ends = t.endsAt ?: return
        setExact(c, ends, pi(c, ACTION_TIMER_FIRE, t.id))
    }

    fun cancelTimer(c: Context, id: String) {
        am(c).cancel(pi(c, ACTION_TIMER_FIRE, id))
    }

    /**
     * On app start, boot, or clock change: fire anything missed within the 5-minute grace
     * window, advance or retire the rest, then arm everything that is still ahead.
     */
    fun launchPass(c: Context) {
        val now = System.currentTimeMillis()

        val alarms = Store.loadAlarms(c)
        val keep = mutableListOf<Alarm>()
        for (a in alarms) {
            if (a.enabled && a.nextFireAt <= now) {
                val withinGrace = now - a.nextFireAt <= GRACE_MS
                if (withinGrace) AlarmReceiver.showAlarmNotification(c, a)
                if (a.days == 0) {
                    if (withinGrace) continue      // a fired one-shot is done
                    a.enabled = false              // missed beyond grace: keep it, switched off
                } else {
                    a.nextFireAt = Rules.nextOccurrence(now, a.hour, a.minute, a.days)
                }
            }
            keep.add(a)
        }
        Store.saveAlarms(c, keep)
        keep.forEach { armAlarm(c, it) }

        val timers = Store.loadTimers(c)
        val keepTimers = mutableListOf<CountdownTimer>()
        for (t in timers) {
            val ends = t.endsAt
            if (ends != null && ends <= now) {
                AlarmReceiver.showTimerNotification(c, t)
                continue
            }
            keepTimers.add(t)
        }
        Store.saveTimers(c, keepTimers)
        keepTimers.forEach { if (it.endsAt != null) armTimer(c, it) }
    }
}
