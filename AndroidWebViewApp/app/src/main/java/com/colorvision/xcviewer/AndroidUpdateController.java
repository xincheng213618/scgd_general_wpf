package com.colorvision.xcviewer;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import android.widget.Toast;

import androidx.core.content.FileProvider;

import com.google.android.material.dialog.MaterialAlertDialogBuilder;

import java.io.File;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

final class AndroidUpdateController {
    static final int REQUEST_INSTALL_PERMISSION = 2504;

    private final Activity activity;
    private final Host host;
    private final ExecutorService executor = Executors.newSingleThreadExecutor();
    private boolean inFlight;
    private File pendingInstallFile;

    AndroidUpdateController(Activity activity, Host host) {
        this.activity = activity;
        this.host = host;
    }

    String currentVersionName() {
        try {
            return activity.getPackageManager()
                    .getPackageInfo(activity.getPackageName(), 0)
                    .versionName;
        } catch (Exception ex) {
            return "--";
        }
    }

    void checkForUpdate() {
        if (inFlight) {
            return;
        }
        inFlight = true;
        host.onWorkStarted("正在检查安全更新…", false);
        executor.execute(() -> {
            try {
                AndroidUpdateClient.Release release =
                        new AndroidUpdateClient(activity).check();
                String currentVersion = currentVersionName();
                activity.runOnUiThread(() -> {
                    finishWork();
                    if (release == null || !AndroidUpdatePolicy.isNewerVersion(
                            release.version, currentVersion)) {
                        showMessage(
                                "已经是最新版本",
                                "当前版本 " + currentVersion + "，暂无更高版本。");
                        return;
                    }
                    showAvailableUpdate(release, currentVersion);
                });
            } catch (Exception ex) {
                activity.runOnUiThread(() -> {
                    finishWork();
                    showFailure(
                            "检查更新失败",
                            AndroidUpdateFailurePresentation.message(ex));
                });
            }
        });
    }

    boolean handleActivityResult(int requestCode) {
        if (requestCode != REQUEST_INSTALL_PERMISSION) {
            return false;
        }
        File verifiedApk = pendingInstallFile;
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O
                || activity.getPackageManager().canRequestPackageInstalls()) {
            launchPackageInstaller(verifiedApk);
        } else {
            Toast.makeText(
                    activity,
                    "系统尚未允许安装更新；已保留校验后的安装包",
                    Toast.LENGTH_LONG).show();
        }
        return true;
    }

    void shutdown() {
        executor.shutdownNow();
    }

    private void showAvailableUpdate(
            AndroidUpdateClient.Release release,
            String currentVersion) {
        String size = String.format(Locale.CHINA, "%.1f MB", release.size / 1024d / 1024d);
        new MaterialAlertDialogBuilder(activity)
                .setTitle("发现 ColorVision Android " + release.version)
                .setMessage("当前 " + currentVersion + " · 安装包 " + size
                        + "\n\n下载完成后会在交给系统安装前校验文件长度、SHA-256、应用包名、版本和签名。")
                .setNegativeButton("稍后", null)
                .setPositiveButton(
                        "下载并安装",
                        (dialog, which) -> downloadAndInstall(release))
                .show();
    }

    private void downloadAndInstall(AndroidUpdateClient.Release release) {
        if (inFlight) {
            return;
        }
        inFlight = true;
        host.onWorkStarted(
                activity.getString(R.string.app_update_downloading, release.version),
                true);
        executor.execute(() -> {
            try {
                File verified = new AndroidUpdateClient(activity).downloadAndVerify(
                        release,
                        percent -> activity.runOnUiThread(
                                () -> host.onProgress(percent)));
                activity.runOnUiThread(() -> {
                    finishWork();
                    requestInstall(verified);
                });
            } catch (Exception ex) {
                activity.runOnUiThread(() -> {
                    finishWork();
                    showFailure(
                            "更新包已阻止",
                            AndroidUpdateFailurePresentation.message(ex));
                });
            }
        });
    }

    private void requestInstall(File verifiedApk) {
        pendingInstallFile = verifiedApk;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                && !activity.getPackageManager().canRequestPackageInstalls()) {
            try {
                Intent settings = new Intent(
                        Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                        Uri.parse("package:" + activity.getPackageName()));
                Toast.makeText(
                        activity,
                        "请在系统页允许 ColorVision 安装本次更新",
                        Toast.LENGTH_LONG).show();
                activity.startActivityForResult(settings, REQUEST_INSTALL_PERMISSION);
            } catch (Exception ex) {
                showMessage(
                        "无法打开系统安装授权",
                        "更新包已经安全校验，可稍后从应用更新入口重试。");
            }
            return;
        }
        launchPackageInstaller(verifiedApk);
    }

    private void launchPackageInstaller(File verifiedApk) {
        if (verifiedApk == null || !verifiedApk.isFile()) {
            showMessage("更新包不可用", "请重新检查安全更新。");
            return;
        }
        try {
            Uri uri = FileProvider.getUriForFile(
                    activity,
                    activity.getPackageName() + ".fileprovider",
                    verifiedApk);
            Intent install = new Intent(Intent.ACTION_INSTALL_PACKAGE);
            install.setDataAndType(uri, "application/vnd.android.package-archive");
            install.setClipData(ClipData.newRawUri("ColorVision Android 更新", uri));
            install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            activity.startActivity(install);
            pendingInstallFile = null;
        } catch (Exception ex) {
            showMessage(
                    "无法启动系统安装器",
                    "更新包已经安全校验，请稍后重试。");
        }
    }

    private void finishWork() {
        inFlight = false;
        host.onWorkFinished();
    }

    private void showMessage(String title, String message) {
        if (activity.isFinishing()) {
            return;
        }
        new MaterialAlertDialogBuilder(activity)
                .setTitle(title)
                .setMessage(message)
                .setPositiveButton("知道了", null)
                .show();
    }

    private void showFailure(String title, String message) {
        if (activity.isFinishing()) {
            return;
        }
        new MaterialAlertDialogBuilder(activity)
                .setTitle(title)
                .setMessage(message)
                .setNegativeButton("稍后", null)
                .setPositiveButton(
                        "重新检查",
                        (dialog, which) -> checkForUpdate())
                .show();
    }

    interface Host {
        void onWorkStarted(String status, boolean determinate);

        void onProgress(int percent);

        void onWorkFinished();
    }
}
