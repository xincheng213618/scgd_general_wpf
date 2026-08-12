package com.colorvision.xcviewer;

import android.content.Context;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.os.Build;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.HashSet;
import java.util.Locale;
import java.util.Set;

final class AndroidUpdateClient {
    interface ProgressListener {
        void onProgress(int percent);
    }

    static final class Release {
        final String version;
        final String filename;
        final long size;
        final String sha256;
        final String downloadPath;

        Release(String version, String filename, long size, String sha256, String downloadPath) {
            this.version = version;
            this.filename = filename;
            this.size = size;
            this.sha256 = sha256.toLowerCase(Locale.ROOT);
            this.downloadPath = downloadPath;
        }
    }

    private final Context context;

    AndroidUpdateClient(Context context) {
        this.context = context.getApplicationContext();
    }

    Release check() throws Exception {
        HttpURLConnection connection = open(AndroidUpdatePolicy.manifestUrl(), "application/json");
        try {
            requireStatus(connection, 200, "android_update_manifest_http_");
            String contentType = connection.getContentType();
            if (contentType == null || !contentType.toLowerCase(Locale.ROOT).startsWith("application/json")) {
                throw new IOException("android_update_manifest_type_rejected");
            }
            byte[] bytes = readBounded(connection.getInputStream(), AndroidUpdatePolicy.MAX_MANIFEST_BYTES);
            JSONObject manifest = new JSONObject(new String(bytes, StandardCharsets.UTF_8));
            if (manifest.optInt("schemaVersion", 0) != 1) {
                throw new IOException("android_update_manifest_schema_rejected");
            }
            if (!manifest.optBoolean("available", false)) {
                return null;
            }
            JSONObject release = manifest.optJSONObject("release");
            if (release == null) {
                throw new IOException("android_update_manifest_release_missing");
            }
            String version = release.optString("version", "");
            String filename = release.optString("filename", "");
            long size = release.optLong("size", 0L);
            String sha256 = release.optString("sha256", "");
            String downloadPath = release.optString("downloadUrl", "");
            if (!AndroidUpdatePolicy.isValidRelease(version, filename, size, sha256, downloadPath)) {
                throw new IOException("android_update_manifest_release_rejected");
            }
            AndroidUpdatePolicy.validatedDownloadUrl(downloadPath);
            return new Release(version, filename, size, sha256, downloadPath);
        } finally {
            connection.disconnect();
        }
    }

    File downloadAndVerify(Release release, ProgressListener listener) throws Exception {
        URL url = AndroidUpdatePolicy.validatedDownloadUrl(release.downloadPath);
        HttpURLConnection connection = open(url, "application/vnd.android.package-archive");
        File directory = new File(context.getCacheDir(), "android-update");
        if (!directory.isDirectory() && !directory.mkdirs()) {
            throw new IOException("android_update_cache_unavailable");
        }
        File partial = new File(directory, "update.partial");
        File verified = new File(directory, "ColorVision-Android-update.apk");
        partial.delete();
        verified.delete();
        try {
            requireStatus(connection, 200, "android_update_download_http_");
            if (!AndroidUpdatePolicy.isApkContentType(connection.getContentType())) {
                throw new IOException("android_update_download_type_rejected");
            }
            long contentLength = contentLength(connection);
            if (contentLength != release.size) {
                throw new IOException("android_update_download_length_rejected");
            }

            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            long received = 0L;
            byte[] buffer = new byte[64 * 1024];
            try (InputStream input = connection.getInputStream();
                 FileOutputStream output = new FileOutputStream(partial)) {
                int count;
                while ((count = input.read(buffer)) != -1) {
                    received += count;
                    if (received > release.size || received > AndroidUpdatePolicy.MAX_APK_BYTES) {
                        throw new IOException("android_update_download_size_rejected");
                    }
                    output.write(buffer, 0, count);
                    digest.update(buffer, 0, count);
                    listener.onProgress((int) Math.min(100L, received * 100L / release.size));
                }
                output.getFD().sync();
            }
            if (received != release.size) {
                throw new IOException("android_update_download_incomplete");
            }
            if (!release.sha256.equals(hex(digest.digest()))) {
                throw new IOException("android_update_download_hash_mismatch");
            }
            verifyPackage(partial, release);
            if (!partial.renameTo(verified)) {
                throw new IOException("android_update_cache_finalize_failed");
            }
            return verified;
        } catch (Exception ex) {
            partial.delete();
            verified.delete();
            throw ex;
        } finally {
            connection.disconnect();
        }
    }

    private void verifyPackage(File apk, Release release) throws Exception {
        PackageManager manager = context.getPackageManager();
        PackageInfo archive = packageArchiveInfo(manager, apk);
        PackageInfo installed = packageInfo(manager, context.getPackageName());
        if (archive == null || !context.getPackageName().equals(archive.packageName)) {
            throw new IOException("android_update_package_name_mismatch");
        }
        if (!release.version.equals(archive.versionName)) {
            throw new IOException("android_update_package_version_mismatch");
        }
        if (longVersionCode(archive) <= longVersionCode(installed)) {
            throw new IOException("android_update_package_not_newer");
        }
        Set<String> archiveCertificates = signingCertificates(archive);
        Set<String> installedCertificates = signingCertificates(installed);
        if (archiveCertificates.isEmpty()
                || installedCertificates.isEmpty()
                || !archiveCertificates.equals(installedCertificates)) {
            throw new IOException("android_update_package_signature_mismatch");
        }
    }

    @SuppressWarnings("deprecation")
    private static PackageInfo packageArchiveInfo(PackageManager manager, File apk) {
        int flags = Build.VERSION.SDK_INT >= Build.VERSION_CODES.P
                ? PackageManager.GET_SIGNING_CERTIFICATES : PackageManager.GET_SIGNATURES;
        return manager.getPackageArchiveInfo(apk.getAbsolutePath(), flags);
    }

    @SuppressWarnings("deprecation")
    private static PackageInfo packageInfo(PackageManager manager, String packageName) throws PackageManager.NameNotFoundException {
        int flags = Build.VERSION.SDK_INT >= Build.VERSION_CODES.P
                ? PackageManager.GET_SIGNING_CERTIFICATES : PackageManager.GET_SIGNATURES;
        return manager.getPackageInfo(packageName, flags);
    }

    @SuppressWarnings("deprecation")
    private static Signature[] signatures(PackageInfo info) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            return info.signingInfo == null ? new Signature[0] : info.signingInfo.getApkContentsSigners();
        }
        return info.signatures == null ? new Signature[0] : info.signatures;
    }

    private static Set<String> signingCertificates(PackageInfo info) throws Exception {
        Set<String> certificates = new HashSet<>();
        for (Signature signature : signatures(info)) {
            certificates.add(hex(MessageDigest.getInstance("SHA-256").digest(signature.toByteArray())));
        }
        return certificates;
    }

    @SuppressWarnings("deprecation")
    private static long longVersionCode(PackageInfo info) {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.P ? info.getLongVersionCode() : info.versionCode;
    }

    private static HttpURLConnection open(URL url, String accept) throws IOException {
        HttpURLConnection connection = (HttpURLConnection) url.openConnection();
        connection.setConnectTimeout(7_000);
        connection.setReadTimeout(30_000);
        connection.setInstanceFollowRedirects(false);
        connection.setRequestMethod("GET");
        connection.setRequestProperty("Accept", accept);
        connection.setRequestProperty("Accept-Encoding", "identity");
        return connection;
    }

    private static void requireStatus(HttpURLConnection connection, int expected, String prefix) throws IOException {
        int status = connection.getResponseCode();
        if (status != expected) {
            throw new IOException(prefix + status);
        }
    }

    private static long contentLength(HttpURLConnection connection) {
        try {
            return Long.parseLong(String.valueOf(connection.getHeaderField("Content-Length")));
        } catch (NumberFormatException ignored) {
            return -1L;
        }
    }

    private static byte[] readBounded(InputStream input, int maximumBytes) throws IOException {
        try (InputStream stream = input; ByteArrayOutputStream output = new ByteArrayOutputStream()) {
            byte[] buffer = new byte[8 * 1024];
            int count;
            while ((count = stream.read(buffer)) != -1) {
                if (output.size() + count > maximumBytes) {
                    throw new IOException("android_update_manifest_too_large");
                }
                output.write(buffer, 0, count);
            }
            return output.toByteArray();
        }
    }

    private static String hex(byte[] bytes) {
        StringBuilder value = new StringBuilder(bytes.length * 2);
        for (byte item : bytes) {
            value.append(String.format(Locale.ROOT, "%02x", item & 0xff));
        }
        return value.toString();
    }
}
