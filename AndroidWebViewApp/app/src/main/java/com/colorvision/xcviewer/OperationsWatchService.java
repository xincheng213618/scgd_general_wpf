package com.colorvision.xcviewer;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.util.Log;

import androidx.core.app.NotificationCompat;
import androidx.core.app.ServiceCompat;
import androidx.core.content.ContextCompat;

import org.json.JSONObject;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class OperationsWatchService extends Service {
    private static final String ACTION_START =
            "com.colorvision.xcviewer.action.START_OPERATIONS_WATCH";
    private static final String NOTIFICATION_CHANNEL_ID = "operations_watch";
    private static final String ATTENTION_CHANNEL_ID = "operations_attention";
    private static final int NOTIFICATION_ID = 22023;
    private static final int ATTENTION_NOTIFICATION_ID = 22024;
    private static final String LOG_TAG = "CVOperationsWatch";

    private final Handler handler = new Handler(Looper.getMainLooper());
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private final Runnable scheduledCheck = this::runCheck;
    private AppPreferences preferences;
    private OperationsApiClient client;
    private String clientProfileKey = "";
    private boolean monitoring;
    private boolean checkInFlight;
    private int consecutiveFailures;
    private boolean lastCheckOnline;
    private boolean hasCompletedCheck;
    private String lastAttentionKey = "";

    static void start(Context context) {
        AppPreferences preferences = new AppPreferences(context);
        if (!preferences.hasOperationsProfile()) {
            return;
        }
        Intent intent = new Intent(context, OperationsWatchService.class).setAction(ACTION_START);
        ContextCompat.startForegroundService(context, intent);
    }

    static void stopForProfileRemoval(Context context) {
        context.stopService(new Intent(context, OperationsWatchService.class));
        NotificationManager manager = context.getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.cancel(ATTENTION_NOTIFICATION_ID);
        }
    }

    @Override
    public void onCreate() {
        super.onCreate();
        preferences = new AppPreferences(this);
        String persistedState = preferences.getOperationsWatchState();
        hasCompletedCheck = !persistedState.isEmpty();
        lastCheckOnline = OperationsWatchHistory.isOnlineState(persistedState);
        lastAttentionKey = OperationsWatchHistory.attentionKey(persistedState);
        createNotificationChannels();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (!preferences.hasOperationsProfile()) {
            stopMonitoring(true);
            return START_NOT_STICKY;
        }

        if (!monitoring) {
            monitoring = true;
            startInForeground("正在连接已配对主机…");
            handler.post(scheduledCheck);
        }
        return START_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDestroy() {
        monitoring = false;
        handler.removeCallbacks(scheduledCheck);
        executor.shutdownNow();
        super.onDestroy();
    }

    private void runCheck() {
        if (!monitoring || checkInFlight) {
            return;
        }
        if (!preferences.hasOperationsProfile()) {
            stopMonitoring(true);
            return;
        }

        checkInFlight = true;
        executor.execute(() -> {
            try {
                JSONObject response = getClient().get("/ops/v1/monitor");
                JSONObject snapshot = response.optJSONObject("data");
                if (snapshot == null) {
                    throw new IllegalStateException("incomplete_live_monitor_response");
                }
                String status = notificationStatus(snapshot);
                String attentionKey = notificationAttentionKey(snapshot);
                handler.post(() -> completeSuccessfulCheck(status, attentionKey));
            } catch (Exception ex) {
                String code = ex.getMessage() == null ? "" : ex.getMessage();
                handler.post(() -> completeFailedCheck(code));
            }
        });
    }

    private OperationsApiClient getClient() throws Exception {
        String profileKey = preferences.getOperationsEndpoint()
                + "\n" + preferences.getOperationsCertificatePin()
                + "\n" + preferences.getOperationsHostId();
        if (client == null || !profileKey.equals(clientProfileKey)) {
            OperationsDeviceIdentity identity =
                    new OperationsDeviceIdentity(preferences.getOperationsHostId());
            client = new OperationsApiClient(
                    preferences.getOperationsEndpoint(),
                    preferences.getOperationsCertificatePin(),
                    preferences.getOrCreateDeviceId(),
                    identity);
            clientProfileKey = profileKey;
        }
        return client;
    }

    private void completeSuccessfulCheck(String status, String attentionKey) {
        if (!monitoring) {
            return;
        }
        checkInFlight = false;
        boolean reconnected = hasCompletedCheck && !lastCheckOnline;
        hasCompletedCheck = true;
        consecutiveFailures = 0;
        if (!lastCheckOnline) {
            Log.i(LOG_TAG, "operations_watch_online");
        }
        lastCheckOnline = true;
        preferences.recordOperationsWatchState(
                attentionKey.isEmpty()
                        ? OperationsWatchHistory.STATE_ONLINE
                        : OperationsWatchHistory.attentionState(attentionKey),
                System.currentTimeMillis());
        updateNotification(OperationsWatchPolicy.successfulCheckNotification(status, reconnected), true);
        if (OperationsWatchPolicy.shouldPostAttention(attentionKey, lastAttentionKey)) {
            postAttentionNotification(attentionKey);
        } else if (attentionKey.isEmpty()) {
            clearAttentionNotification();
        }
        lastAttentionKey = attentionKey;
        scheduleNext(OperationsWatchPolicy.HEALTHY_CHECK_MILLISECONDS);
    }

    private void completeFailedCheck(String code) {
        if (!monitoring) {
            return;
        }
        checkInFlight = false;
        hasCompletedCheck = true;
        if (code.contains("unknown_or_revoked_device")) {
            preferences.recordOperationsWatchState(
                    OperationsWatchHistory.STATE_REVOKED, System.currentTimeMillis());
            preferences.markOperationsProfileRevoked();
            Log.w(LOG_TAG, "operations_watch_pairing_revoked");
            clearAttentionNotification();
            updateNotification("配对授权已失效 · 请打开应用重新配对", false);
            detachNotificationAndStop();
            return;
        }

        boolean notifyOffline = OperationsWatchPolicy.shouldPostOffline(
                hasCompletedCheck, lastCheckOnline, lastAttentionKey);
        consecutiveFailures++;
        long retryDelay = OperationsWatchPolicy.retryDelayMilliseconds(consecutiveFailures);
        if (lastCheckOnline || consecutiveFailures == 1) {
            Log.w(LOG_TAG, "operations_watch_offline retry_seconds=" + (retryDelay / 1000L));
        }
        lastCheckOnline = false;
        preferences.recordOperationsWatchState(
                OperationsWatchHistory.STATE_OFFLINE, System.currentTimeMillis());
        updateNotification("连接暂断 · " + (retryDelay / 1000L) + " 秒后重试", true);
        if (notifyOffline) {
            postAttentionNotification(OperationsWatchPolicy.ATTENTION_OFFLINE);
            lastAttentionKey = OperationsWatchPolicy.ATTENTION_OFFLINE;
        }
        client = null;
        clientProfileKey = "";
        scheduleNext(retryDelay);
    }

    private String notificationStatus(JSONObject snapshot) {
        JSONObject flow = snapshot.optJSONObject("flow");
        JSONObject performance = snapshot.optJSONObject("performance");
        JSONObject mainUi = performance == null ? null : performance.optJSONObject("mainUi");
        JSONObject alerts = snapshot.optJSONObject("alerts");
        JSONObject devices = snapshot.optJSONObject("devices");
        JSONObject messageChannel = snapshot.optJSONObject("messageChannel");
        return OperationsWatchPolicy.healthyStatus(
                mainUi == null ? "unavailable" : mainUi.optString("state", "unavailable"),
                flow != null && flow.optBoolean("isActive", false),
                alerts == null ? 0 : alerts.optInt("criticalCount", 0),
                alerts == null ? 0 : alerts.optInt("errorCount", 0),
                devices == null || !devices.optBoolean("available", false)
                        ? 0 : devices.optInt("attentionCount", 0),
                messageChannel != null
                        && messageChannel.optBoolean("available", false)
                        && messageChannel.optBoolean("attentionRequired", false));
    }

    private String notificationAttentionKey(JSONObject snapshot) {
        JSONObject performance = snapshot.optJSONObject("performance");
        JSONObject mainUi = performance == null ? null : performance.optJSONObject("mainUi");
        JSONObject alerts = snapshot.optJSONObject("alerts");
        JSONObject devices = snapshot.optJSONObject("devices");
        JSONObject messageChannel = snapshot.optJSONObject("messageChannel");
        return OperationsWatchPolicy.attentionKey(
                mainUi == null ? "unavailable" : mainUi.optString("state", "unavailable"),
                alerts == null ? 0 : alerts.optInt("criticalCount", 0),
                alerts == null ? 0 : alerts.optInt("errorCount", 0),
                devices == null || !devices.optBoolean("available", false)
                        ? 0 : devices.optInt("attentionCount", 0),
                messageChannel != null
                        && messageChannel.optBoolean("available", false)
                        && messageChannel.optBoolean("attentionRequired", false));
    }

    private void scheduleNext(long delayMilliseconds) {
        handler.removeCallbacks(scheduledCheck);
        if (monitoring) {
            handler.postDelayed(scheduledCheck, delayMilliseconds);
        }
    }

    private void startInForeground(String status) {
        int serviceType = Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q
                ? ServiceInfo.FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE
                : 0;
        ServiceCompat.startForeground(
                this,
                NOTIFICATION_ID,
                buildNotification(status, true),
                serviceType);
    }

    private void updateNotification(String status, boolean ongoing) {
        NotificationManager manager = getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.notify(NOTIFICATION_ID, buildNotification(status, ongoing));
        }
    }

    private Notification buildNotification(String status, boolean ongoing) {
        NotificationCompat.Builder builder = new NotificationCompat.Builder(this, NOTIFICATION_CHANNEL_ID)
                .setSmallIcon(R.drawable.ic_devices_24)
                .setContentTitle("ColorVision 运维守护")
                .setContentText(status)
                .setContentIntent(createOperationsPendingIntent(0, ""))
                .setCategory(NotificationCompat.CATEGORY_SERVICE)
                .setVisibility(NotificationCompat.VISIBILITY_PRIVATE)
                .setOnlyAlertOnce(true)
                .setOngoing(ongoing)
                .setShowWhen(true)
                .setWhen(System.currentTimeMillis())
                .setPriority(NotificationCompat.PRIORITY_LOW);

        return builder.build();
    }

    private void postAttentionNotification(String attentionKey) {
        String message = OperationsWatchPolicy.attentionMessage(attentionKey);
        if (message.isEmpty()) {
            return;
        }
        Notification notification = new NotificationCompat.Builder(this, ATTENTION_CHANNEL_ID)
                .setSmallIcon(R.drawable.ic_devices_24)
                .setContentTitle("ColorVision 需要关注")
                .setContentText(message)
                .setStyle(new NotificationCompat.BigTextStyle().bigText(message))
                .setContentIntent(createOperationsPendingIntent(
                        1, OperationsWatchPolicy.attentionDestination(attentionKey)))
                .setCategory(NotificationCompat.CATEGORY_ERROR)
                .setVisibility(NotificationCompat.VISIBILITY_PRIVATE)
                .setAutoCancel(true)
                .setOnlyAlertOnce(false)
                .setPriority(NotificationCompat.PRIORITY_DEFAULT)
                .build();
        NotificationManager manager = getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.notify(ATTENTION_NOTIFICATION_ID, notification);
            Log.w(LOG_TAG, "operations_watch_attention state=" + attentionKey);
        }
    }

    private void clearAttentionNotification() {
        NotificationManager manager = getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.cancel(ATTENTION_NOTIFICATION_ID);
        }
    }

    private PendingIntent createOperationsPendingIntent(int requestCode, String destination) {
        Intent openIntent = new Intent(this, OperationsActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        String safeDestination = OperationsWatchPolicy.normalizeDestination(destination);
        if (!safeDestination.isEmpty()) {
            openIntent.putExtra(OperationsActivity.EXTRA_OPEN_DESTINATION, safeDestination);
        }
        return PendingIntent.getActivity(
                this,
                requestCode,
                openIntent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    private void createNotificationChannels() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return;
        }
        NotificationChannel watchChannel = new NotificationChannel(
                NOTIFICATION_CHANNEL_ID,
                "ColorVision 运维守护",
                NotificationManager.IMPORTANCE_LOW);
        watchChannel.setDescription("显示已配对 ColorVision 主机的后台连接状态");
        watchChannel.setLockscreenVisibility(Notification.VISIBILITY_PRIVATE);
        NotificationChannel attentionChannel = new NotificationChannel(
                ATTENTION_CHANNEL_ID,
                "ColorVision 运维提醒",
                NotificationManager.IMPORTANCE_DEFAULT);
        attentionChannel.setDescription("仅在已配对主机进入新的异常状态时提醒一次");
        attentionChannel.setLockscreenVisibility(Notification.VISIBILITY_PRIVATE);
        NotificationManager manager = getSystemService(NotificationManager.class);
        if (manager != null) {
            manager.createNotificationChannel(watchChannel);
            manager.createNotificationChannel(attentionChannel);
        }
    }

    private void stopMonitoring(boolean removeNotification) {
        monitoring = false;
        handler.removeCallbacks(scheduledCheck);
        stopForegroundCompat(removeNotification);
        stopSelf();
    }

    private void detachNotificationAndStop() {
        monitoring = false;
        handler.removeCallbacks(scheduledCheck);
        stopForegroundCompat(false);
        stopSelf();
    }

    @SuppressWarnings("deprecation")
    private void stopForegroundCompat(boolean removeNotification) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            stopForeground(removeNotification ? STOP_FOREGROUND_REMOVE : STOP_FOREGROUND_DETACH);
        } else {
            stopForeground(removeNotification);
        }
    }
}
