package com.colorvision.xcviewer;

import android.Manifest;
import android.app.Activity;
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
import android.widget.TextView;
import android.widget.Toast;

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
public class QrScanActivity extends Activity implements SurfaceHolder.Callback, Camera.PreviewCallback {
    public static final String EXTRA_QR_RESULT = "qr_result";

    private static final int REQUEST_CAMERA_PERMISSION = 2001;

    private SurfaceView surfaceView;
    private TextView statusText;
    private Camera camera;
    private SurfaceHolder surfaceHolder;
    private MultiFormatReader reader;
    private boolean surfaceReady;
    private boolean decoding;
    private boolean completed;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        reader = createReader();
        createContentView();
        if (hasCameraPermission()) {
            startCameraIfReady();
        } else {
            requestPermissions(new String[]{Manifest.permission.CAMERA}, REQUEST_CAMERA_PERMISSION);
        }
    }

    private void createContentView() {
        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);

        surfaceView = new SurfaceView(this);
        surfaceHolder = surfaceView.getHolder();
        surfaceHolder.addCallback(this);
        root.addView(surfaceView, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT));

        TextView backButton = new TextView(this);
        backButton.setText("←");
        backButton.setTextColor(Color.WHITE);
        backButton.setTextSize(34);
        backButton.setGravity(Gravity.CENTER);
        backButton.setBackgroundColor(Color.argb(96, 0, 0, 0));
        backButton.setOnClickListener(v -> finish());
        FrameLayout.LayoutParams backParams = new FrameLayout.LayoutParams(dp(58), dp(58), Gravity.TOP | Gravity.LEFT);
        backParams.setMargins(dp(12), dp(18), 0, 0);
        root.addView(backButton, backParams);

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
        statusParams.setMargins(dp(18), 0, dp(18), dp(28));
        root.addView(statusText, statusParams);

        setContentView(root);
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

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != REQUEST_CAMERA_PERMISSION) {
            return;
        }

        if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
            startCameraIfReady();
            return;
        }

        Toast.makeText(this, "没有相机权限，可返回手动输入连接地址", Toast.LENGTH_LONG).show();
        setResult(RESULT_CANCELED);
        finish();
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
            Toast.makeText(this, "相机启动失败，可手动输入连接地址", Toast.LENGTH_LONG).show();
            setResult(RESULT_CANCELED);
            finish();
        }
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
        }
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
