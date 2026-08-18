package com.colorvision.xcviewer;

import android.Manifest;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.hardware.Camera;
import android.os.Bundle;
import android.view.Gravity;
import android.view.SurfaceHolder;
import android.view.SurfaceView;
import android.view.View;
import android.widget.FrameLayout;
import android.widget.ImageButton;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.view.WindowInsetsControllerCompat;

import com.google.zxing.BarcodeFormat;
import com.google.zxing.BinaryBitmap;
import com.google.zxing.DecodeHintType;
import com.google.zxing.MultiFormatReader;
import com.google.zxing.NotFoundException;
import com.google.zxing.PlanarYUVLuminanceSource;
import com.google.zxing.Result;
import com.google.zxing.common.HybridBinarizer;

import java.util.Arrays;
import java.util.EnumMap;
import java.util.List;
import java.util.Map;

@SuppressWarnings("deprecation")
public class QrScanActivity extends AppCompatActivity implements SurfaceHolder.Callback, Camera.PreviewCallback {
    public static final String EXTRA_QR_RESULT = "qr_result";
    public static final String EXTRA_SCAN_FAILURE = "qr_scan_failure";

    private static final int REQUEST_CAMERA_PERMISSION = 2001;

    private SurfaceView surfaceView;
    private TextView statusText;
    private FrameLayout root;
    private ImageButton torchButton;
    private Camera camera;
    private SurfaceHolder surfaceHolder;
    private MultiFormatReader reader;
    private boolean surfaceReady;
    private boolean decoding;
    private boolean completed;
    private boolean torchEnabled;
    private final RuntimePermissionDialogState cameraPermissionDialogState =
            new RuntimePermissionDialogState();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        configureSystemBars();
        reader = createReader();
        createContentView();
        if (hasCameraPermission()) {
            startCameraIfReady();
        } else {
            requestCameraPermission();
        }
    }

    private void configureSystemBars() {
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        getWindow().setStatusBarColor(Color.BLACK);
        getWindow().setNavigationBarColor(Color.BLACK);
        WindowInsetsControllerCompat controller = WindowCompat.getInsetsController(
                getWindow(), getWindow().getDecorView());
        controller.setAppearanceLightStatusBars(false);
        controller.setAppearanceLightNavigationBars(false);
    }

    private void createContentView() {
        root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);

        surfaceView = new SurfaceView(this);
        surfaceHolder = surfaceView.getHolder();
        surfaceHolder.addCallback(this);
        root.addView(surfaceView, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT));

        ImageButton backButton = new ImageButton(this);
        backButton.setImageResource(R.drawable.ic_arrow_back_24);
        backButton.setColorFilter(Color.WHITE);
        backButton.setContentDescription("返回");
        backButton.setBackgroundColor(Color.argb(96, 0, 0, 0));
        backButton.setOnClickListener(v -> finish());
        FrameLayout.LayoutParams backParams = new FrameLayout.LayoutParams(
                dp(58), dp(58), Gravity.TOP | Gravity.START);
        backParams.setMargins(dp(12), dp(12), 0, 0);
        root.addView(backButton, backParams);

        torchButton = new ImageButton(this);
        torchButton.setImageResource(R.drawable.ic_flash_on_24);
        torchButton.setColorFilter(Color.WHITE);
        torchButton.setContentDescription("开启补光灯");
        torchButton.setBackgroundColor(Color.argb(96, 0, 0, 0));
        torchButton.setVisibility(View.GONE);
        torchButton.setOnClickListener(v -> toggleTorch());
        FrameLayout.LayoutParams torchParams = new FrameLayout.LayoutParams(
                dp(58), dp(58), Gravity.TOP | Gravity.END);
        torchParams.setMargins(0, dp(12), dp(12), 0);
        root.addView(torchButton, torchParams);

        statusText = new TextView(this);
        statusText.setText("将电脑端二维码放入取景框");
        statusText.setTextColor(Color.WHITE);
        statusText.setTextSize(16);
        statusText.setGravity(Gravity.CENTER);
        statusText.setPadding(dp(18), dp(10), dp(18), dp(10));
        statusText.setBackgroundColor(Color.argb(132, 0, 0, 0));
        FrameLayout.LayoutParams statusParams = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.WRAP_CONTENT,
                Gravity.BOTTOM);
        statusParams.setMargins(dp(18), 0, dp(18), dp(16));
        root.addView(statusText, statusParams);

        ViewCompat.setOnApplyWindowInsetsListener(root, (view, windowInsets) -> {
            Insets statusBars = windowInsets.getInsets(WindowInsetsCompat.Type.statusBars());
            Insets displayCutout = windowInsets.getInsets(WindowInsetsCompat.Type.displayCutout());
            Insets navigationBars = windowInsets.getInsets(WindowInsetsCompat.Type.navigationBars());
            backParams.topMargin = AppWindowInsetsPolicy.topContentInset(
                    statusBars.top, displayCutout.top) + dp(12);
            torchParams.topMargin = backParams.topMargin;
            statusParams.bottomMargin = Math.max(
                    navigationBars.bottom, displayCutout.bottom) + dp(16);
            backButton.setLayoutParams(backParams);
            torchButton.setLayoutParams(torchParams);
            statusText.setLayoutParams(statusParams);
            return windowInsets;
        });
        setContentView(root);
        ViewCompat.requestApplyInsets(root);
    }

    private MultiFormatReader createReader() {
        MultiFormatReader formatReader = new MultiFormatReader();
        Map<DecodeHintType, Object> hints = new EnumMap<>(DecodeHintType.class);
        hints.put(DecodeHintType.POSSIBLE_FORMATS, Arrays.asList(BarcodeFormat.QR_CODE));
        hints.put(DecodeHintType.CHARACTER_SET, "UTF-8");
        formatReader.setHints(hints);
        return formatReader;
    }

    private boolean hasCameraPermission() {
        return checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED;
    }

    private void requestCameraPermission() {
        int requestGeneration = cameraPermissionDialogState.begin();
        requestPermissions(new String[]{Manifest.permission.CAMERA}, REQUEST_CAMERA_PERMISSION);
        root.postDelayed(() -> cameraPermissionDialogState.observe(
                        requestGeneration, hasCameraPermission(), hasWindowFocus()),
                RuntimePermissionDialogState.OBSERVE_DELAY_MILLISECONDS);
        root.postDelayed(() -> recoverBlockedCameraPermissionRequest(requestGeneration),
                RuntimePermissionDialogState.NO_DIALOG_RECOVERY_DELAY_MILLISECONDS);
    }

    private void recoverBlockedCameraPermissionRequest(int requestGeneration) {
        if (completed || !cameraPermissionDialogState.shouldRecoverAsBlocked(
                requestGeneration, hasCameraPermission(), hasWindowFocus())) {
            return;
        }
        finishWithFailure(QrScanFailurePresentation.CAMERA_PERMISSION_BLOCKED);
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != REQUEST_CAMERA_PERMISSION) {
            return;
        }

        boolean granted = grantResults.length > 0
                && grantResults[0] == PackageManager.PERMISSION_GRANTED;
        if (granted) {
            cameraPermissionDialogState.completeFromSystemResult(true);
            startCameraIfReady();
            return;
        }
        if (cameraPermissionDialogState.completeFromSystemResult(false)) {
            finishWithFailure(QrScanFailurePresentation.CAMERA_PERMISSION_DENIED);
        }
    }

    @Override
    public void surfaceCreated(SurfaceHolder holder) {
        surfaceReady = true;
        startCameraIfReady();
    }

    @Override
    public void surfaceChanged(SurfaceHolder holder, int format, int width, int height) {
    }

    @Override
    public void surfaceDestroyed(SurfaceHolder holder) {
        surfaceReady = false;
        releaseCamera();
    }

    private void startCameraIfReady() {
        if (!surfaceReady || !hasCameraPermission() || camera != null) {
            return;
        }

        try {
            camera = Camera.open();
            configureCamera(camera);
            camera.setPreviewDisplay(surfaceHolder);
            camera.setPreviewCallback(this);
            camera.startPreview();
        } catch (Exception ex) {
            releaseCamera();
            finishWithFailure(QrScanFailurePresentation.CAMERA_UNAVAILABLE);
        }
    }

    private void finishWithFailure(String reason) {
        completed = true;
        Intent result = new Intent();
        result.putExtra(EXTRA_SCAN_FAILURE, reason);
        setResult(RESULT_CANCELED, result);
        finish();
    }

    private void configureCamera(Camera targetCamera) {
        targetCamera.setDisplayOrientation(90);
        Camera.Parameters parameters = targetCamera.getParameters();
        List<String> focusModes = parameters.getSupportedFocusModes();
        if (focusModes != null) {
            if (focusModes.contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE)) {
                parameters.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE);
            } else if (focusModes.contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_VIDEO)) {
                parameters.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_VIDEO);
            } else if (focusModes.contains(Camera.Parameters.FOCUS_MODE_AUTO)) {
                parameters.setFocusMode(Camera.Parameters.FOCUS_MODE_AUTO);
            }
        }
        targetCamera.setParameters(parameters);
        updateTorchAvailability(parameters);
    }

    private void updateTorchAvailability(Camera.Parameters parameters) {
        List<String> flashModes = parameters.getSupportedFlashModes();
        boolean torchSupported = flashModes != null
                && flashModes.contains(Camera.Parameters.FLASH_MODE_TORCH);
        torchButton.setVisibility(torchSupported ? View.VISIBLE : View.GONE);
        if (!torchSupported) {
            torchEnabled = false;
            updateTorchButton();
        }
    }

    private void toggleTorch() {
        if (camera == null) {
            return;
        }
        try {
            Camera.Parameters parameters = camera.getParameters();
            List<String> flashModes = parameters.getSupportedFlashModes();
            if (flashModes == null || !flashModes.contains(Camera.Parameters.FLASH_MODE_TORCH)) {
                torchButton.setVisibility(View.GONE);
                return;
            }
            torchEnabled = !torchEnabled;
            parameters.setFlashMode(torchEnabled
                    ? Camera.Parameters.FLASH_MODE_TORCH
                    : Camera.Parameters.FLASH_MODE_OFF);
            camera.setParameters(parameters);
            updateTorchButton();
        } catch (Exception ignored) {
            torchEnabled = false;
            updateTorchButton();
        }
    }

    private void updateTorchButton() {
        if (torchButton == null) {
            return;
        }
        torchButton.setColorFilter(torchEnabled ? Color.BLACK : Color.WHITE);
        torchButton.setBackgroundColor(torchEnabled
                ? Color.WHITE : Color.argb(96, 0, 0, 0));
        torchButton.setContentDescription(torchEnabled ? "关闭补光灯" : "开启补光灯");
    }

    @Override
    public void onPreviewFrame(byte[] data, Camera sourceCamera) {
        if (completed || decoding || data == null || sourceCamera == null) {
            return;
        }

        Camera.Size size = sourceCamera.getParameters().getPreviewSize();
        if (size == null) {
            return;
        }

        decoding = true;
        byte[] frame = Arrays.copyOf(data, data.length);
        int width = size.width;
        int height = size.height;

        new Thread(() -> {
            String text = decodeFrame(frame, width, height);
            runOnUiThread(() -> {
                decoding = false;
                if (text != null && !text.isEmpty()) {
                    completed = true;
                    Intent result = new Intent();
                    result.putExtra(EXTRA_QR_RESULT, text);
                    setResult(RESULT_OK, result);
                    finish();
                }
            });
        }, "ColorVisionQrDecode").start();
    }

    private String decodeFrame(byte[] frame, int width, int height) {
        String rotated = decodeLuminance(rotateYPlane90(frame, width, height), height, width);
        if (rotated != null) {
            return rotated;
        }
        return decodeLuminance(frame, width, height);
    }

    private String decodeLuminance(byte[] data, int width, int height) {
        try {
            PlanarYUVLuminanceSource source = new PlanarYUVLuminanceSource(
                    data,
                    width,
                    height,
                    0,
                    0,
                    width,
                    height,
                    false);
            BinaryBitmap bitmap = new BinaryBitmap(new HybridBinarizer(source));
            Result result = reader.decodeWithState(bitmap);
            return result == null ? null : result.getText();
        } catch (NotFoundException ex) {
            return null;
        } catch (Exception ex) {
            return null;
        } finally {
            reader.reset();
        }
    }

    private byte[] rotateYPlane90(byte[] data, int width, int height) {
        byte[] rotated = new byte[width * height];
        int index = 0;
        for (int x = 0; x < width; x++) {
            for (int y = height - 1; y >= 0; y--) {
                rotated[index++] = data[y * width + x];
            }
        }
        return rotated;
    }

    @Override
    protected void onPause() {
        releaseCamera();
        super.onPause();
    }

    private void releaseCamera() {
        if (camera == null) {
            return;
        }

        try {
            camera.setPreviewCallback(null);
            camera.stopPreview();
            camera.release();
        } catch (Exception ignored) {
        } finally {
            camera = null;
            torchEnabled = false;
            updateTorchButton();
            torchButton.setVisibility(View.GONE);
        }
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
