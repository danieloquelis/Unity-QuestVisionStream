package com.xreducation.questvisionstreamplugin

import android.app.Activity
import android.util.Log
import android.widget.Toast

class QuestVisionStreamManager(private val activity: Activity) {
    fun showToast(message: String) {
        Log.i("QuestCameraStreamPlugin", "showToast called with: $message")

        // Try showing a Toast (may not show in VR)
        activity.runOnUiThread {
            Toast.makeText(activity, message, Toast.LENGTH_LONG).show()
        }

        // Send message back into Unity by reflection
        try {
            val unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer")
            val unityPlayerField = unityPlayerClass.getDeclaredField("currentActivity")
            val unityActivity = unityPlayerField.get(null) as Activity

            val method = unityPlayerClass.getMethod(
                "UnitySendMessage", String::class.java, String::class.java, String::class.java
            )
            method.invoke(null, "PluginReceiver", "OnPluginMessage", "Hello Unity! Toast called with: $message")
        } catch (e: Exception) {
            Log.e("QuestCameraStreamPlugin", "Failed to send Unity message", e)
        }
    }
}
