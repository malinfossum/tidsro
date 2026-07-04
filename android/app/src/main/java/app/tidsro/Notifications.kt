package app.tidsro

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.media.AudioAttributes
import android.net.Uri
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat

object Notifications {
    const val CHANNEL_WARN = "warn"

    fun channelFor(sound: Int): String = "fire_" + Sound.from(sound).name.lowercase()

    fun createChannels(c: Context) {
        val nm = c.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        val attrs = AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_ALARM)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build()
        for (s in Sound.values()) {
            val ch = NotificationChannel(
                channelFor(s.ordinal),
                "Alarms - " + s.label,
                NotificationManager.IMPORTANCE_HIGH
            )
            if (s.rawRes != null) {
                ch.setSound(Uri.parse("android.resource://" + c.packageName + "/" + s.rawRes), attrs)
            } else {
                ch.setSound(null, null)
            }
            ch.enableVibration(true)
            nm.createNotificationChannel(ch)
        }
        val warn = NotificationChannel(
            CHANNEL_WARN,
            "5-minute warnings",
            NotificationManager.IMPORTANCE_DEFAULT
        )
        warn.setSound(null, null)
        nm.createNotificationChannel(warn)
    }

    fun contentIntent(c: Context): PendingIntent =
        PendingIntent.getActivity(
            c, 0, Intent(c, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

    fun baseBuilder(c: Context, channel: String): NotificationCompat.Builder =
        NotificationCompat.Builder(c, channel)
            .setSmallIcon(R.drawable.ic_notification)
            .setColor(0xFFE3B341.toInt())
            .setCategory(NotificationCompat.CATEGORY_ALARM)
            .setAutoCancel(true)
            .setContentIntent(contentIntent(c))

    fun notify(c: Context, id: Int, n: Notification) {
        try {
            NotificationManagerCompat.from(c).notify(id, n)
        } catch (e: SecurityException) {
            // Notifications permission not granted; nothing sensible to do here.
        }
    }
}
