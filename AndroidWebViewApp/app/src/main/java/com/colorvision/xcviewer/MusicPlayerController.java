package com.colorvision.xcviewer;

import android.app.Activity;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

import androidx.media3.common.MediaItem;
import androidx.media3.common.PlaybackException;
import androidx.media3.common.Player;
import androidx.media3.exoplayer.ExoPlayer;

import java.util.Locale;

final class MusicPlayerController {
    private final Activity activity;
    private final AppPreferences preferences;
    private final Runnable chooseAudioAction;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable progressRunnable = new Runnable() {
        @Override
        public void run() {
            updateProgress();
            if (isPlaying()) {
                handler.postDelayed(this, 1000);
            }
        }
    };

    private TextView titleText;
    private TextView statusText;
    private Button playPauseButton;
    private Button stopButton;
    private ExoPlayer player;
    private boolean prepared;

    MusicPlayerController(Activity activity, AppPreferences preferences, Runnable chooseAudioAction) {
        this.activity = activity;
        this.preferences = preferences;
        this.chooseAudioAction = chooseAudioAction;
    }

    void bindViews(TextView titleText, TextView statusText, Button playPauseButton, Button stopButton) {
        this.titleText = titleText;
        this.statusText = statusText;
        this.playPauseButton = playPauseButton;
        this.stopButton = stopButton;
        updateControls();
    }

    String getSavedAudioTitle() {
        return preferences.getAudioTitle();
    }

    boolean hasSavedAudio() {
        return preferences.getAudioUri() != null;
    }

    void setAudio(Uri uri, String title, boolean autoPlay) {
        preferences.saveAudio(uri, title);
        if (titleText != null) {
            titleText.setText(title);
        }
        if (statusText != null) {
            statusText.setText("正在准备播放...");
        }
        prepare(uri, autoPlay);
    }

    void togglePlayback() {
        if (player != null && prepared) {
            if (isPlaying()) {
                player.pause();
                handler.removeCallbacks(progressRunnable);
                updateProgress();
            } else {
                start();
            }
            return;
        }

        Uri uri = preferences.getAudioUri();
        if (uri == null) {
            chooseAudioAction.run();
            return;
        }

        prepare(uri, true);
    }

    void stop() {
        if (player == null || !prepared) {
            return;
        }

        try {
            if (isPlaying()) {
                player.pause();
            }
            player.seekTo(0);
            handler.removeCallbacks(progressRunnable);
            updateProgress();
        } catch (IllegalStateException ignored) {
        }
    }

    void release() {
        handler.removeCallbacks(progressRunnable);
        if (player != null) {
            player.release();
            player = null;
        }
        prepared = false;
    }

    private void prepare(Uri uri, boolean autoPlay) {
        release();
        prepared = false;
        updateControls();
        if (statusText != null) {
            statusText.setText("正在准备播放...");
        }

        try {
            player = new ExoPlayer.Builder(activity).build();
            player.addListener(new Player.Listener() {
                @Override
                public void onPlaybackStateChanged(int playbackState) {
                    prepared = playbackState == Player.STATE_READY || playbackState == Player.STATE_ENDED;
                    if (playbackState == Player.STATE_READY) {
                        updateProgress();
                    } else if (playbackState == Player.STATE_BUFFERING && statusText != null) {
                        statusText.setText("正在缓冲...");
                    } else if (playbackState == Player.STATE_ENDED) {
                        handler.removeCallbacks(progressRunnable);
                        if (statusText != null && player != null) {
                            statusText.setText("播放完成 · " + formatTime(player.getDuration()));
                        }
                        updateControls();
                    }
                }

                @Override
                public void onIsPlayingChanged(boolean isPlaying) {
                    updateProgress();
                    handler.removeCallbacks(progressRunnable);
                    if (isPlaying) {
                        handler.postDelayed(progressRunnable, 1000);
                    }
                }

                @Override
                public void onPlayerError(PlaybackException error) {
                    release();
                    updateControls();
                    if (statusText != null) {
                        statusText.setText("播放失败，请换一首音乐或确认文件未损坏。");
                    }
                }
            });
            player.setMediaItem(MediaItem.fromUri(uri));
            player.setPlayWhenReady(autoPlay);
            player.prepare();
        } catch (Exception ex) {
            release();
            updateControls();
            if (statusText != null) {
                statusText.setText("无法打开这首音乐，请重新选择。");
            }
            Toast.makeText(activity, "无法打开这首音乐", Toast.LENGTH_LONG).show();
        }
    }

    private void start() {
        if (player == null || !prepared) {
            return;
        }

        try {
            if (player.getPlaybackState() == Player.STATE_ENDED) {
                player.seekTo(0);
            }
            player.play();
            updateProgress();
            handler.removeCallbacks(progressRunnable);
            handler.postDelayed(progressRunnable, 1000);
        } catch (IllegalStateException ex) {
            if (statusText != null) {
                statusText.setText("播放状态异常，请重新选择音乐。");
            }
        }
    }

    private void updateProgress() {
        if (playPauseButton != null) {
            playPauseButton.setAlpha(1f);
            playPauseButton.setText(isPlaying() ? "暂停" : "播放");
        }
        if (stopButton != null) {
            stopButton.setEnabled(player != null && prepared);
            stopButton.setAlpha(player != null && prepared ? 1f : 0.45f);
        }
        if (statusText == null || player == null || !prepared) {
            return;
        }

        try {
            String state = isPlaying() ? "正在播放" : "已暂停";
            statusText.setText(state + " · " + formatTime(player.getCurrentPosition()) + " / " + formatTime(player.getDuration()));
        } catch (IllegalStateException ignored) {
        }
    }

    private void updateControls() {
        boolean hasAudio = hasSavedAudio();
        if (titleText != null) {
            titleText.setText(getSavedAudioTitle());
        }
        if (statusText != null && (player == null || !prepared)) {
            statusText.setText(hasAudio ? "已选择，点击播放。" : "从手机选择一首音乐后播放。");
        }
        if (playPauseButton != null) {
            playPauseButton.setEnabled(hasAudio);
            playPauseButton.setAlpha(hasAudio ? 1f : 0.45f);
            playPauseButton.setText(isPlaying() ? "暂停" : "播放");
        }
        if (stopButton != null) {
            boolean canStop = player != null && prepared;
            stopButton.setEnabled(canStop);
            stopButton.setAlpha(canStop ? 1f : 0.45f);
        }
    }

    private boolean isPlaying() {
        try {
            return player != null && player.isPlaying();
        } catch (IllegalStateException ex) {
            return false;
        }
    }

    private String formatTime(long milliseconds) {
        if (milliseconds < 0) {
            milliseconds = 0;
        }

        long totalSeconds = milliseconds / 1000;
        long hours = totalSeconds / 3600;
        long minutes = (totalSeconds % 3600) / 60;
        long seconds = totalSeconds % 60;
        if (hours > 0) {
            return String.format(Locale.US, "%d:%02d:%02d", hours, minutes, seconds);
        }
        return String.format(Locale.US, "%02d:%02d", minutes, seconds);
    }
}
