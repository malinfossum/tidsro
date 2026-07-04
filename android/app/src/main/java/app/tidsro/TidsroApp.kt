package app.tidsro

import android.app.Application

class TidsroApp : Application() {
    override fun onCreate() {
        super.onCreate()
        Notifications.createChannels(this)
        AlarmScheduler.launchPass(this)
    }
}
